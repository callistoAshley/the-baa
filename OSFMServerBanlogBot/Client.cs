using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Discord.Commands;
using System.Reflection;

namespace OSFMServerBanlogBot
{
    public static class Client
    {
        // the client - this is how we log in and hook the right events
        public static DiscordSocketClient client = new DiscordSocketClient();
        public static CommandService commandService = new CommandService();

        public static async Task Login()
        {
            // we get the token from a plaintext file in the current working directory called "token.txt"
            string token = File.ReadAllText(Directory.GetCurrentDirectory() + "/token.txt");

            // log in!
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            Console.WriteLine("successfully logged in!");

            // now install the commands
            await InstallCommands();
            Console.WriteLine("successfully installed commands!");

            // add events
            client.UserBanned += LoggerManager.UserBanned;
            client.UserUnbanned += LoggerManager.UserUnbanned;
            client.Disconnected += Disconnected;

            // display the version as the game
            await client.SetGameAsync($"VERSION: {Constants.VERSION}");

            //await Task.Delay(-1);
        }

        // pls check out https://docs.stillu.cc/guides/commands/intro.html
        private static async Task InstallCommands()
        {
            // Hook the MessageReceived event into our command handler
            client.MessageReceived += HandleCommand;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await commandService.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: null);
        }

        private static async Task HandleCommand(SocketMessage message)
        {
            // create the context
            // don't handle the command if it's a system message or sent by a bot
            if (message is SocketSystemMessage || message.Author.IsBot) return;

            SocketCommandContext context = new SocketCommandContext(client, message as SocketUserMessage);

            try
            {
                // check whether the command has the right prefix
                int prefixPos = 0;
                if (!(message as SocketUserMessage).HasStringPrefix("baa ", ref prefixPos)) return;

                Console.WriteLine($"{message.Author} executing command {message.Content}");

                // check for valid permissions
                foreach (RequiredPermissionsAttribute perms in 
                    commandService.Search(context, prefixPos).Commands[0].Command.Attributes.Select(
                    x => x as RequiredPermissionsAttribute))
                {
                    if (perms != null && !perms.Valid((SocketGuildUser)context.User) 
                        && !(perms.devOverride && context.User.Id == 521073234301550632))
                    {
                        await context.Channel.SendMessageAsync(
                            "Invalid permissions. You need all of the following permissions to run that command:\n" +
                            $"{Enum.Format(typeof(GuildPermission), perms.requiredPermissions, "g")}"
                        );
                        if (perms.devOverride) 
                            await context.Channel.SendMessageAsync("You can also run this command if you are my developer.");
                        return;
                    }
                }

                // then execute the command
                await commandService.ExecuteAsync(context: context, argPos: prefixPos, services: null);
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, context.Guild);
            }
        }

        private static async Task Disconnected(Exception ex)
        {
            Console.WriteLine($"\n\nDisconnected! Exception:\n==============\n{ex}\n==============\nAttempting to reconnect.\n");
            await client.StopAsync();
            await Login();
        }
    }
}
