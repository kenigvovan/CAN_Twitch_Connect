using System;
using cantwitchconnect.Network;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class TossPlayerAction : IVoteAction
    {
        private const double Gravity = 0.04; // blocks per tick²

        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly int[] heights;
        private readonly IServerNetworkChannel channel;

        public TossPlayerAction(ICoreServerAPI sapi, string[] playerNames, int[] heights,
                                IServerNetworkChannel channel)
        {
            this.sapi        = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.heights     = heights ?? Array.Empty<int>();
            this.channel     = channel;
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex <= 0) return;
            if (channel == null) return;
            int heightIdx = winnerIndex - 1;
            if (heightIdx >= heights.Length) return;

            double velocity = Math.Sqrt(2.0 * Gravity * heights[heightIdx]);

            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                if (player.Entity == null) continue;
                if (player is not IServerPlayer sp) continue;
                channel.SendPacket(new ForceMotionMessage { MotionY = velocity }, sp);
            }
        }
    }
}
