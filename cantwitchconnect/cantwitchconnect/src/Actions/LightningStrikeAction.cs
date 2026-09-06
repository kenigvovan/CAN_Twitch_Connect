using System;
using cantwitchconnect.Voting;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace cantwitchconnect.Actions
{
    public sealed class LightningStrikeAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly int radius;
        private readonly Random random = new();

        public LightningStrikeAction(ICoreServerAPI sapi, string[] playerNames, int radius)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.radius = Math.Max(0, radius);
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;

            var wsys = sapi.ModLoader.GetModSystem<WeatherSystemServer>(true);
            if (wsys == null) return;

            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                var pos = player.Entity?.Pos;
                if (pos == null) continue;

                double x = pos.X + (random.NextDouble() * 2 - 1) * radius;
                double z = pos.Z + (random.NextDouble() * 2 - 1) * radius;
                int y = sapi.WorldManager.GetSurfacePosY((int)x, (int)z) ?? (int)pos.Y;

                wsys.SpawnLightningFlash(new Vec3d(x, y + 1, z));
            }
        }
    }
}
