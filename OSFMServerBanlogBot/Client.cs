using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
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
        private static CommandService commandService = new CommandService();

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

            // init json data
            LoggerManager.Init();

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

                // return here if the user running the command isn't me or isn't the owner of the server
                // i'd check if the user has admin too but i forgot how to iterate through the priviliges
                if (message.Author.Id != 521073234301550632 && message.Author.Id != context.Guild.OwnerId)
                {
                    await context.Channel.SendMessageAsync("Sorry, you need to be the developer of the bot or the owner of the server to use its commands.");
                    return;
                }

                Console.WriteLine($"{message.Author} executing command {message.Content}");

                // then execute the command
                await commandService.ExecuteAsync(context: context, argPos: prefixPos, services: null);

                // catch my idiocy
                if (DateTime.Now >= new DateTime(2022, 2, 1))
                {
                    await context.Channel.SendMessageAsync("**FOR MY FUTURE SELF:**\n" +
                        "Discord will require verification for message content intent after April 30, 2022. Because that's stinky, **check if" +
                        "this has been merged: https://github.com/discord-net/Discord.Net/pull/1717**");
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.LogException(ex, context.Guild);
            }
        }
    }
}
