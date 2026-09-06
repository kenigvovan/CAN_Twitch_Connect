using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class CallChatCommandAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string commandToCall;
        private readonly string[] allowedCommands;

        public CallChatCommandAction(ICoreServerAPI sapi, string commandToCall, string[] allowedCommands)
        {
            this.sapi = sapi;
            this.commandToCall = commandToCall;
            this.allowedCommands = allowedCommands ?? Array.Empty<string>();
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;
            if (string.IsNullOrWhiteSpace(commandToCall)) return;
            if (allowedCommands.Length == 0) return;

            var head = commandToCall.TrimStart('/').Split(' ');
            if (head.Length == 0) return;
            if (Array.FindIndex(allowedCommands,
                    c => string.Equals(c, head[0], StringComparison.OrdinalIgnoreCase)) < 0) return;

            var caller = new Caller
            {
                Type = EnumCallerType.Console,
                CallerRole = "admin",
                CallerPrivileges = new[] { "*" },
                FromChatGroupId = GlobalConstants.ConsoleGroup
            };
            sapi.ChatCommands.ExecuteUnparsed(commandToCall, new TextCommandCallingArgs { Caller = caller });
        }
    }
}
