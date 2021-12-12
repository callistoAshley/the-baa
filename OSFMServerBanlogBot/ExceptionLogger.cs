using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace OSFMServerBanlogBot
{
    public static class ExceptionLogger
    {
        public static async void LogException(Exception exception, SocketGuild originatingServer = null)
        {
            try
            {
                // write the exception to the console
                Console.WriteLine($"An exception was encountered:\n{exception}");

                // log the exception to my test server first
                // this can't find the guild for no reason
                /*
                SocketTextChannel testServerLog = Client.client.GetGuild(796238499871195167).GetTextChannel(847373998157070336);

                await testServerLog.SendMessageAsync($"An exception was encountered in {originatingServer.ToString() ?? "null"} " +
                    $"({originatingServer.Id.ToString() ?? "null"})" +
                    $"\n===========\n{exception}\n===========");*/

                // change the bot's status to the exception message so everyone can laugh at me
                await Client.client.SetGameAsync($"{exception.GetType().Name}: {exception.Message}");

                // only log the exception to the server's exception channel if an originating server was provided and the server has used serverconfig
                if (originatingServer is null || !LoggerManager.serverConfigs.ContainsKey(originatingServer.Id)) return;

                // log the exception to the server's exception channel
                await originatingServer.GetTextChannel(LoggerManager.serverConfigs[originatingServer.Id].exceptionLogChannel).SendMessageAsync(
                    $"An exception was encountered:\n===========\n{exception}\n===========");
            }
            catch (Exception ex)
            {
                // absolutely not dealing with that
                Console.WriteLine($"An exception was encountered while handling an exception.\nOriginal exception:{exception}\nException:{ex}");
                Environment.Exit(0);
            }
        }
    }
}
