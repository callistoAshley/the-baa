using System;
using System.Collections.Generic;
using System.Text;
using Discord.WebSocket;

namespace OSFMServerBanlogBot
{
    // represents a server's config data
    public class ServerConfig
    {
        // ids of an ISocketMessageChannel

        // the channel to log bans into
        public ulong logChannel;
        // the channel to log to if an exception is encountered
        public ulong exceptionLogChannel;

        public ServerConfig(ulong logChannel, ulong exceptionLogChannel)
        {
            this.logChannel = logChannel;
            this.exceptionLogChannel = exceptionLogChannel;
        }
    }
}
