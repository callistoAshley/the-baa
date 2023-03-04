using System;
using System.Threading.Tasks;
using System.IO;
using Discord.Net;

namespace OSFMServerBanlogBot
{
    class Program
    {
        static void Main(string[] args)
        {
            ILeaveMyCodeAloneFor6MonthsAndByWhimOfCosmicIronyItStopsCompiling(args).GetAwaiter();
        }

        static async Task ILeaveMyCodeAloneFor6MonthsAndByWhimOfCosmicIronyItStopsCompiling(string[] args)
        {
            try
            {
                // log in
                Client.Login().GetAwaiter().GetResult();

                // then check args
                for (int i = 0; i < args.Length; i++)
                {
                    Console.WriteLine(args[i]);
                    // "--post-update" just informs the server that called the update command that the update was successful
                    if (args[i] == "--post-update")
                    {
                        // the following two arguments should be the respective ids of the guild and text channel that called the update command
                        await Client.client.GetGuild(ulong.Parse(args[i + 1])).GetTextChannel(ulong.Parse(args[i + 2]))
                            .SendMessageAsync("Successfully updated.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"an exception was encountered:\n{ex}\nexiting..... :(");
                // then just exit
            }
            Console.ReadLine();
        }
    }
}
