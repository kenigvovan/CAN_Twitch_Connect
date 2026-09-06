using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class KillPlayersAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;

        public KillPlayersAction(ICoreServerAPI sapi, string[] playerNames)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;
            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                player.Entity?.Die();
            }
        }
    }
}
