using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;

namespace OSFMServerBanlogBot
{
    public class RequiredPermissionsAttribute : Attribute
    {
        public GuildPermission requiredPermissions;
        public bool devOverride; // if true, allows myself to bypass the permissions check

        public RequiredPermissionsAttribute(GuildPermission requiredPermissions, bool devOverride = false)
        {
            this.requiredPermissions = requiredPermissions;
            this.devOverride = devOverride;
        }

        public bool Valid(SocketGuildUser user)
        {
            return user.GuildPermissions.Has(requiredPermissions);
        }
    }
}
