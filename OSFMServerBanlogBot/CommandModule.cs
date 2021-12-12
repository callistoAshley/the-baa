using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;
using Discord;
using Discord.Net;
using System.Threading.Tasks;
using System.IO;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace OSFMServerBanlogBot
{
    // the library this bot uses, Discord.NET, reflects through the assembly and looks for types
    // that derive from ModuleBase when it's looking for commands
    // *then* it selects the methods in those types that have the Command attribute
    // which is pretty neat i think
    public class CommandModule : ModuleBase<SocketCommandContext>
    {
        // i can't host this bot 24/7 so this command is here in case someone gets banned overnight
        // it just goes through the audit log and selects entries it missed
        [Command("resync")]
        public async Task Resync(int limit = 100)
        {
            await foreach (var v in Context.Guild.GetAuditLogsAsync(100))
            {
                foreach (var logEntry in v)
                {
                    if (logEntry.Action == ActionType.Ban)
                    {
                        
                    }
                }
            }
        }

        [Command("reason")]
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
        public async Task ServerConfig(ulong logChannel, ulong exceptionLogChannel)
        {
            LoggerManager.serverConfigs.Add(Context.Guild.Id, new ServerConfig(logChannel, exceptionLogChannel));
            
            JsonManager.SerializeJson(Directory.GetCurrentDirectory() + $"/servers/{Context.Guild.Id}.json", LoggerManager.serverConfigs[Context.Guild.Id]);

            await Context.Channel.SendMessageAsync("Success!");
        }

        [Command("invite")]
        public async Task Invite()
        {
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
    }
}
