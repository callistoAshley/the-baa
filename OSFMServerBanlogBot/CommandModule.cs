using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Discord.Commands;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;

namespace OSFMServerBanlogBot
{
    // the library this bot uses, Discord.NET, reflects through the assembly and looks for types
    // that derive from ModuleBase when it's looking for commands
    // *then* it selects the methods in those types that have the Command attribute
    // which is pretty neat i think
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

        // i can't host this bot 24/7 so this command is here in case someone gets banned overnight
        // it just goes through the audit log and selects entries it missed
        // emoji created as default filter; it just checks for this and does nothing
        [Command("resync")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.BanMembers | GuildPermission.KickMembers)]
        public async Task Resync(int limit = 100, ActionType filter = ActionType.EmojiCreated)
        {
            int addedCases = 0;

            // TEMPORARY WORKAROUND change this later
            if (filter == ActionType.EmojiCreated)
            {
                await foreach (var v in Context.Guild.GetAuditLogsAsync(limit))
                {
                    foreach (var logEntry in v.Where((RestAuditLogEntry r) => r.Action == ActionType.Ban || r.Action == ActionType.Unban))
                    {
                        object data = null;
                        IUser target = null;

                        switch (logEntry.Action)
                        {
                            case ActionType.Ban:
                                data = logEntry.Data as BanAuditLogData;
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
            }
            else
            {
                await foreach (var v in Context.Guild.GetAuditLogsAsync(limit, actionType: filter))
                {
                    foreach (var logEntry in v.Where((RestAuditLogEntry r) => r.Action == ActionType.Ban || r.Action == ActionType.Unban))
                    {
                        object data = null;
                        IUser target = null;

                        switch (logEntry.Action)
                        {
                            case ActionType.Ban:
                                data = logEntry.Data as BanAuditLogData;
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
            }

            await Context.Message.Channel.SendMessageAsync(
                $"Found {addedCases} bans/unbans in the audit log that didn't have a matching target in the server's banlogs");
        }

        [Command("reason")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.BanMembers | GuildPermission.KickMembers)]
        public async Task Reason(string caseNumber, string reason)
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
                // get the start of the range by replacing everything after - with an empty string
                int caseStart = int.Parse(caseNumber.Substring(0, caseNumber.IndexOf('-')));//int.Parse(new Regex(@"\-.*?").Replace(caseNumber, string.Empty));
                // then get the end of the range by doing the opposite
                int caseEnd = int.Parse(new Regex(@"^.*?-").Replace(caseNumber, string.Empty));

                int editedCases = 0;
                for (int i = caseStart; i <= caseEnd; i++)
                {
                    // edit all of the cases between the range
                    await LoggerManager.ChangeCaseReason(Context.Guild, i, reason, Context);
                    editedCases++;
                }

                // done!
                await Context.Channel.SendMessageAsync($"Edited {editedCases} cases.");
            }
            else
            {
                // edit the case
                await LoggerManager.ChangeCaseReason(Context.Guild, int.Parse(caseNumber), reason, Context);

                await Context.Channel.SendMessageAsync($"Edited case {caseNumber}.");
            }
        }

        [Command("serverconfig")]
        [RequiredPermissions(GuildPermission.Administrator, true)]
        public async Task ServerConfig(ulong logChannel, ulong exceptionLogChannel)
        {
            LoggerManager.serverConfigs.Add(Context.Guild.Id, new ServerConfig(logChannel, exceptionLogChannel));
            
            JsonManager.SerializeJson(Directory.GetCurrentDirectory() + $"/servers/{Context.Guild.Id}.json", LoggerManager.serverConfigs[Context.Guild.Id]);

            await Context.Channel.SendMessageAsync("Success!");
        }

        [Command("invite")]
        [RequiredPermissions(GuildPermission.Administrator)]
        public async Task Invite()
        {
            if (Context.User.Id != 521073234301550632)
                await Context.Channel.SendMessageAsync("https://tenor.com/view/chicken-nuggets-flush-flushed-gif-9918572");
            else
                await Context.Channel.SendMessageAsync(File.ReadAllText(Directory.GetCurrentDirectory() + "/invite.txt"));
        }
        
        // odd bits and bobs - some easter eggs, some joke stuff, some debugging stuff
        [Command("samelcave")]
        public async Task Samelcave()
        {
            await Context.Channel.SendMessageAsync((Client.client.GetGuild(796238499871195167) is null).ToString());
        }

        [Command("servers")]
        public async Task Servers()
        {
            await Context.Channel.SendMessageAsync($"I am in **{Client.client.Guilds.Count}** servers:\n" +
                $"{string.Join("\n", Client.client.Guilds)}");
        }

        [Command("you_need_some_permissions_to_run_this_command")]
        [RequiredPermissions(GuildPermission.ViewAuditLog | GuildPermission.ModerateMembers | GuildPermission.DeafenMembers)]
        public async Task YouNeedSomePermissionsToRunThisCommand()
        {
            await Context.Channel.SendMessageAsync("https://tenor.com/view/yippee-happy-celebration-joy-confetti-gif-25557730");
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
