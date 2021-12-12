using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;

namespace OSFMServerBanlogBot
{
    public class BanlogEntry
    {
        public ulong user;
        public ActionType action;
        public string reason;
        public ulong responsibleModerator;

        // assigned manually
        public ulong associatedMessage; // the message in the banlog channel associated with the ban case
        public int caseNumber;
        public string userName;
        public string responsibleModeratorName;

        public BanlogEntry(ulong user, ActionType action, string reason, ulong responsibleModerator)
        {
            this.user = user;
            this.action = action;
            this.reason = reason;
            this.responsibleModerator = responsibleModerator;
        }
    }
}
