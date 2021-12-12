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
            try
            {
                // log in
                Client.Login().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"an exception was encountered:\n{ex}");
                // then just exit
            }
            Console.ReadLine();
        }
    }
}
