using System;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using Discord.Commands;
using Discord;
using Discord.Rest;
using Octokit;
using Discord.WebSocket;

namespace OSFMServerBanlogBot
{
    public class CommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        public async Task Help(string command = "")
        {
            if (command == string.Empty)
            {
                await Context.Channel.SendMessageAsync($"**Commands:\n|** {string.Join("**\n|** ", Client.commandService.Commands.Select((CommandInfo c) => c.Name))}");
            }
            else
            {
                foreach (MethodInfo m in typeof(CommandModule).GetMethods())
                {
                    CommandAttribute c = (CommandAttribute)m.GetCustomAttribute(typeof(CommandAttribute));
                    if (c != null)
                    {
                        CommandHelpAttribute help = (CommandHelpAttribute)m.GetCustomAttribute(typeof(CommandHelpAttribute));

                        if (m != null) continue;
                    }
                }
            }
        }

        // this just goes through the audit log and selects entries it missed, in the case that it crashed overnight or something
        [Command("resync")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.BanMembers | GuildPermission.KickMembers)]
        public async Task Resync(int cases = 100)
        {
            try
            {
                int addedCases = 0;

                await foreach (var v in Context.Guild.GetAuditLogsAsync(cases))
                {
                    foreach (var logEntry in v.Where((RestAuditLogEntry r) => r.Action == ActionType.Ban || r.Action == ActionType.Unban
                        || r.Action == ActionType.Kick))
                    {
                        object data = null;
                        IUser target = null;

                        switch (logEntry.Action)
                        {
                            case ActionType.Ban:
                                data = logEntry.Data as BanAuditLogData;
                                target = ((BanAuditLogData)data).Target;
                                break;
                            case ActionType.Kick:
                                data = logEntry.Data as KickAuditLogData;
                                target = ((BanAuditLogData)data).Target;
                                break;
                            case ActionType.Unban:
                                data = logEntry.Data as UnbanAuditLogData;
                                target = ((UnbanAuditLogData)data).Target;
                                break;
                        }

                        if (!LoggerManager.serverBanlogs[Context.Guild.Id].Exists(
                            (BanlogEntry b) => b.user == target.Id))
                        {
                            await LoggerManager.NewLogEntry(target, Context.Guild, logEntry.Action);
                            addedCases++;
                        }
                    }
                }

                await Context.Message.Channel.SendMessageAsync(
                        $"Found {addedCases} bans/unbans in the audit log that didn't have a matching target in the server's banlogs");
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("reason")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.BanMembers | GuildPermission.KickMembers)]
        public async Task Reason(string caseNumber, string reason)
        {
            try
            {
                // error handling
                if (!LoggerManager.UsedServerConfig(Context.Guild) || !LoggerManager.serverBanlogs.ContainsKey(Context.Guild.Id))
                {
                    await Context.Message.ReplyAsync($"{Context.Guild} doesn't have any cases!");
                    return;
                }

                // first, check whether we're changing the reasons on a range of cases
                // the syntax for this is (case number)-(case number) (eg baa reason 12-17 "don't really like them honestly")
                Regex rangeMatch = new Regex(@"^\d+\-\d+");
                if (rangeMatch.IsMatch(caseNumber))
                {
                    // this might take a while...
                    using (IDisposable typingState = Context.Channel.EnterTypingState())
                    {
                        // get the start of the range by replacing everything after - with an empty string
                        int caseStart = int.Parse(caseNumber.Substring(0, caseNumber.IndexOf('-')));
                        // then get the end of the range by doing the opposite
                        int caseEnd = int.Parse(new Regex(@"^.*?-").Replace(caseNumber, string.Empty));

                        int editedCases = 0;
                        for (int i = caseStart; i <= caseEnd; i++)
                        {
                            // edit all of the cases between the range
                            await LoggerManager.ChangeCaseReason(Context.Guild, i - LoggerManager.serverConfigs[Context.Guild.Id].caseOffset,
                                reason, Context);
                            editedCases++;
                        }

                        // done!
                        await Context.Channel.SendMessageAsync($"Edited {editedCases} cases.");
                    }
                }
                else
                {
                    if (!int.TryParse(caseNumber, out int c))
                    {
                        await Context.Channel.SendMessageAsync($"Failed to parse \"{caseNumber}\" to an integer. " +
                            $"Ensure there are only numbers in the case identifier.");
                        return;
                    }

                    // edit the case
                    if (await LoggerManager.ChangeCaseReason(Context.Guild, 
                        c - LoggerManager.serverConfigs[Context.Guild.Id].caseOffset, 
                        reason, 
                        Context))
                    {
                        await Context.Channel.SendMessageAsync($"Edited case {caseNumber}.");
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("serverconfig")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        public async Task ServerConfig(ulong logChannel, ulong exceptionLogChannel, int caseOffset = 0)
        {
            try
            {
                // remove the server config at the guild's id if it exists (allows a server to use serverconfig more than once)
                if (LoggerManager.serverConfigs.ContainsKey(Context.Guild.Id)) 
                    LoggerManager.serverConfigs.Remove(Context.Guild.Id);
                // then add it
                LoggerManager.serverConfigs.Add(Context.Guild.Id, new ServerConfig(logChannel, exceptionLogChannel, caseOffset));

                JsonManager.SerializeJson(Directory.GetCurrentDirectory() + $"/servers/{Context.Guild.Id}.json", LoggerManager.serverConfigs[Context.Guild.Id]);

                await Context.Channel.SendMessageAsync("Success!\n" +
                    $"Log Channel: #{Context.Guild.GetChannel(logChannel).Name}\n" +
                    $"Error Logging Channel: #{Context.Guild.GetChannel(exceptionLogChannel).Name}\n" +
                    $"Case Offset: {caseOffset}"
                );
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("serverconfig")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        // this serverconfig just says the server's config data rather than configuring it
        public async Task ServerConfig()
        {
            try
            {
                if (!LoggerManager.serverConfigs.ContainsKey(Context.Guild.Id))
                    await Context.Channel.SendMessageAsync("Your server hasn't used serverconfig yet. " +
                        "The correct syntax is `baa serverconfig (logging channel id) (error logging channel id) ([case offset])` (excluding the brackets.)");

                await Context.Channel.SendMessageAsync(
                    $"Logging Channel: {LoggerManager.serverConfigs[Context.Guild.Id].logChannel}\n" +
                    $"Exception Logging Channel: {LoggerManager.serverConfigs[Context.Guild.Id].exceptionLogChannel}\n" +
                    $"Case Offset: {LoggerManager.serverConfigs[Context.Guild.Id].caseOffset}"
                );
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("iddump")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.BanMembers | GuildPermission.KickMembers)]
        public async Task IdDump(string caseRange)
        {
            try
            {
                // error handling
                if (!LoggerManager.UsedServerConfig(Context.Guild) || !LoggerManager.serverBanlogs.ContainsKey(Context.Guild.Id))
                {
                    await Context.Message.ReplyAsync($"{Context.Guild} doesn't have any cases!");
                    return;
                }

                Regex rangeMatch = new Regex(@"^\d+\-\d+");
                if (rangeMatch.IsMatch(caseRange))
                {
                    // get the start of the range by replacing everything after - with an empty string
                    int caseStart = int.Parse(caseRange.Substring(0, caseRange.IndexOf('-')));
                    // then get the end of the range by doing the opposite
                    int caseEnd = int.Parse(new Regex(@"^.*?-").Replace(caseRange, string.Empty));

                    // get the banlogs between caseStart and caseEnd
                    IEnumerable<BanlogEntry> banlogEntries = LoggerManager.serverBanlogs[Context.Guild.Id].Where(x =>
                        x.caseNumber >= caseStart - LoggerManager.serverConfigs[Context.Guild.Id].caseOffset
                        && x.caseNumber <= caseEnd - LoggerManager.serverConfigs[Context.Guild.Id].caseOffset
                    );

                    // create a temporary file to write the ids to, then send that file.
                    using (StreamWriter stream = File.CreateText(Directory.GetCurrentDirectory() + "/temp-iddump.txt"))
                    {
                        foreach (BanlogEntry entry in banlogEntries)
                        {
                            stream.WriteLine(entry.user);
                        }
                    }
                    await Context.Channel.SendFileAsync(new FileAttachment(Directory.GetCurrentDirectory() + "/temp-iddump.txt"),
                            $"Here are the ids for cases {caseStart} to {caseEnd}.");
                    // finally, delete the file
                    File.Delete(Directory.GetCurrentDirectory() + "/temp-iddump.txt");
                }
                else
                {
                    await Context.Channel.SendMessageAsync("Invalid range format (must be `baa iddump x-y`)");
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("fileban")]
        [RequiredPermissions(GuildPermission.BanMembers)]
        public async Task FileBan()
        {
            try
            {
                if (Context.Message.Attachments.Count == 0)
                {
                    await Context.Channel.SendMessageAsync("Expected an attachment (a url will not suffice)");
                    return;
                }

                using (Context.Channel.EnterTypingState())
                {
                    IEnumerable<string> ids;
                    using (HttpClient client = new HttpClient())
                    {
                        HttpResponseMessage response = await client.GetAsync(Context.Message.Attachments.ElementAt(0).Url);
                        ids = response.Content.ReadAsStringAsync().Result.Split("\n").Where(x => ulong.TryParse(x, out _));
                    }
                    await Context.Channel.SendMessageAsync($"Downloaded {ids.Count()} ids. We might be here for a while...");
                    int usersBanned = 0;
                    foreach (string id in ids)
                    {
                        await Context.Guild.AddBanAsync(ulong.Parse(id), reason: $"Fileban command issued by {Context.Message.Author.Username}");
                        usersBanned++;
                    }
                    await Context.Channel.SendMessageAsync($"Banned {usersBanned} users.");
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("invite")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        public async Task Invite()
        {
            if (Context.User.Id != 521073234301550632)
                await Context.Channel.SendMessageAsync("https://tenor.com/view/chicken-nuggets-flush-flushed-gif-9918572");
            else
                await Context.Channel.SendMessageAsync(File.ReadAllText(Directory.GetCurrentDirectory() + "/invite.txt"));
        }

        [Command("update")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        public async Task Update()
        {
            try
            {
                // first ensure that the user calling the command is either myself or the bot host
                if (Context.User.Id != 521073234301550632 || Context.User.Id != 351871502460649485)
                {
                    await Context.Channel.SendMessageAsync("https://tenor.com/view/toad-toad-rip-toad-rocket-toad-super-mario-toad-mario-gif-22448762");
                    return;
                }

                bool restart = false; // set to true if an update is ready to be installed
                using (Context.Channel.EnterTypingState())
                {
                    GitHubClient client = new GitHubClient(new ProductHeaderValue("samelgamedev-the-baa"));
                    Release latestRelease = client.Repository.Release.GetAll("samelgamedev", "the-baa").Result[0];
                    if (latestRelease == null)
                        await Context.Channel.SendMessageAsync("Couldn't download latest release; I must be ratelimited.");
                    await Context.Channel.SendMessageAsync($"Latest release tag: {latestRelease.TagName}");
                    if (latestRelease.CreatedAt.DateTime > new FileInfo(AppDomain.CurrentDomain.FriendlyName).CreationTimeUtc)
                    {
                        restart = true;
                        await Context.Channel.SendMessageAsync("The latest release is newer than my own binary. " +
                            "I'll be down for a few minutes while I install it.");
                        // .... then start the python script
                    }
                    else
                    {
                        await Context.Channel.SendMessageAsync("The latest release is older than my own binary. " +
                            "Nothing to do.");
                    }

                    // warn about rate limit info, just in case
                    RateLimit rateLimitInfo = client.GetLastApiInfo().RateLimit;
                    await Context.Channel.SendMessageAsync($"Warning: I only have {rateLimitInfo.Remaining} uses of the GitHub API remaining." +
                        $"My uses will reset at {rateLimitInfo.Reset.DateTime} (UTC)");
                }
                if (restart) Environment.Exit(0);
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }

        [Command("wipe")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        public async Task Wipe()
        {
            try
            {
                if (!File.Exists($"banlogs/{Context.Guild.Id}.json"))
                {
                    await Context.Channel.SendMessageAsync("This server has no cases.");
                    return;
                }

                await Context.Channel.SendMessageAsync("This will delete ALL of the cases associated with this server. " +
                    "Send the name of this server within 30 seconds to confirm.");
                bool sendTimeoutMessage = true;

                async Task MessageSent(SocketMessage message)
                {
                    if (message.Channel.Id != Context.Channel.Id) return;
                    if (message.Content == Context.Guild.Name)
                    {
                        Console.WriteLine($"wiping cases in {Context.Guild.Id}");
                        File.Delete($"banlogs/{Context.Guild.Id}.json");
                        sendTimeoutMessage = false;
                        await Context.Channel.SendMessageAsync("Deleted cases.");
                    }
                }

                Client.client.MessageReceived += MessageSent;
                new Thread(async () => 
                {
                    Thread.Sleep(new TimeSpan(0, 0, 30));
                    Client.client.MessageReceived -= MessageSent;
                    if (sendTimeoutMessage) await Context.Channel.SendMessageAsync("Timed out.");
                }).Start();
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, Context.Guild);
            }
        }
        
        // odd bits and bobs: some easter eggs, some jokes, some debugging stuff
        [Command("servers")]
        public async Task Servers()
        {
            await Context.Channel.SendMessageAsync($"I am in **{Client.client.Guilds.Count}** servers:\n" +
                $"{string.Join("\n", Client.client.Guilds)}");
        }

        [Command("ping")]
        public async Task Ping()
        {
            int time = (DateTimeOffset.UtcNow - Context.Message.Timestamp).Milliseconds;
            await Context.Channel.SendMessageAsync($"Pong from the land down under! {time}ms");
        }

        private class CommandHelpAttribute : Attribute
        {
            public string helpText;
            public CommandHelpAttribute(string helpText)
            {
                this.helpText = helpText;
            }
        }
    }
}
