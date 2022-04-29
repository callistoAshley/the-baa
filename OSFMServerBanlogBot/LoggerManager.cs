using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using Discord.Net;
using Newtonsoft.Json;
using System.IO;
using Discord.Rest;
using Discord.Commands;

namespace OSFMServerBanlogBot
{
    public static class LoggerManager
    {
        public static Dictionary<ulong, List<BanlogEntry>> serverBanlogs = new Dictionary<ulong, List<BanlogEntry>>();
        public static Dictionary<ulong, ServerConfig> serverConfigs = new Dictionary<ulong, ServerConfig>();

        public static void Init()
        {
            // add server banlogs to the dictionary
            foreach (FileInfo f in new DirectoryInfo(Directory.GetCurrentDirectory() + "/banlogs").GetFiles())
            {
                serverBanlogs.Add(ulong.Parse(f.Name.Replace(f.Extension, string.Empty)), (List<BanlogEntry>)JsonManager.DeserializeJson<List<BanlogEntry>>(f.FullName));
            }

            // then add server configs
            foreach (FileInfo f in new DirectoryInfo(Directory.GetCurrentDirectory() + "/servers").GetFiles())
            {
                serverConfigs.Add(ulong.Parse(f.Name.Replace(f.Extension, string.Empty)), (ServerConfig)JsonManager.DeserializeJson<ServerConfig>(f.FullName));
            }
        }

        public static async Task UserBanned(IUser user, SocketGuild guild)
            => await NewLogEntry(user, guild, ActionType.Ban);

        public static async Task UserUnbanned(IUser user, SocketGuild guild)
            => await NewLogEntry(user, guild, ActionType.Unban);

        public static async Task NewLogEntry(IUser user, SocketGuild guild, ActionType action)
        {
            try
            {
                Console.WriteLine($"new log entry: {user}, {guild}, {action}");

                // make sure the server's banlogs list exists
                if (!serverBanlogs.ContainsKey(guild.Id))
                    serverBanlogs.Add(guild.Id, new List<BanlogEntry>());

                // first, get the channel that the server is configured to log bans to
                ISocketMessageChannel logChannel = guild.GetTextChannel(serverConfigs[guild.Id].logChannel);

                // then get the associated audit log entry
                // i'm so sorry this is horrifying to look at
                RestAuditLogEntry auditLogEntry = guild.GetAuditLogsAsync(1, actionType: action).Select(x => x.ElementAt(0)).ElementAtAsync(0).Result;

                // add the new banlog entry to the server banlog
                serverBanlogs[guild.Id].Add(new BanlogEntry(user.Id, action, auditLogEntry.Reason, auditLogEntry.User.Id));

                // then log it to the banlog channel
                RestUserMessage msg = await logChannel.SendMessageAsync(
                    // the case is the number of entries in the server's banlogs after adding the new entry
                    $"**{action}** | Case {serverBanlogs[guild.Id].Count}\n" +
                    $"**User:** {user} (<@!{user.Id}>)\n" +
                    $"**Reason:** {auditLogEntry.Reason}\n" +
                    $"**Responsible Moderator:** {auditLogEntry.User}");
                // set the new banlog entry's associated message to the msg's id and set the case number
                serverBanlogs[guild.Id].Last().associatedMessage = msg.Id;
                serverBanlogs[guild.Id].Last().caseNumber = serverBanlogs[guild.Id].Count;
                // also set the names of the users responsible
                // this is used in ChangeCaseReason as a fallback in case GetUser returns null
                serverBanlogs[guild.Id].Last().userName = user.ToString();
                serverBanlogs[guild.Id].Last().responsibleModeratorName = auditLogEntry.User.ToString();

                // serialize the server's banlog entries again
                ReserializeBanlogs(guild);
            }
            catch (KeyNotFoundException ex)
            {
                // their server must not have used serverconfig yet, just ignore it
                // still log it to the console though
                Console.WriteLine($"KeyNotFoundException in {guild}: {ex}");
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, guild);
            }
        }

        public static bool UsedServerConfig(SocketGuild guild)
        {
            return serverConfigs.ContainsKey(guild.Id);
        }

        public static void ReserializeBanlogs(SocketGuild guild)
        {
            JsonManager.SerializeJson(Directory.GetCurrentDirectory() + $"/banlogs/{guild.Id}.json", serverBanlogs[guild.Id]);
        }

        public static async Task ChangeCaseReason(SocketGuild guild, int caseNum, string reason, SocketCommandContext context = null)
        {
            // error handling
            if (!UsedServerConfig(guild) || !serverBanlogs.ContainsKey(guild.Id))
            {
                await context.Message.ReplyAsync($"{guild} doesn't have any cases!");
                return;
            }

            if (caseNum > serverBanlogs[guild.Id].Count)
            {
                await context.Message.ReplyAsync($"{caseNum} is out of range.");
                return;
            }

            // get the associated banlog entry
            BanlogEntry banlogEntry = serverBanlogs[guild.Id][caseNum - 1];

            // change the reason and edit the associated message to reflect it
            banlogEntry.reason = reason;

            var txt = guild.GetTextChannel(serverConfigs[guild.Id].logChannel);
            var msg = txt.GetMessageAsync(banlogEntry.associatedMessage).Result;

            await guild.GetTextChannel(serverConfigs[guild.Id].logChannel).ModifyMessageAsync(banlogEntry.associatedMessage,
                x => x.Content = 
                    $"**{banlogEntry.action}** | Case {banlogEntry.caseNumber}\n" +
                    $"**User:** {banlogEntry.userName} (<@!{banlogEntry.user}>)\n" +
                    $"**Reason:** {reason}\n" +
                    $"**Responsible Moderator:** {/*Client.client.GetUser(banlogEntry.responsibleModerator).ToString() ?? */banlogEntry.responsibleModeratorName}");

            // reserialize banlogs
            ReserializeBanlogs(guild);
        }
    }
}
