using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class RtpPlayerAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly int[] radii;
        private readonly Random random = new();

        public RtpPlayerAction(ICoreServerAPI sapi, string[] playerNames, int[] radii)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.radii = radii ?? Array.Empty<int>();
        }

        // Scan downward from above the reported surface until we find a non-air
        // block that has two air blocks above it. Returns the feet Y of the safe
        // standing position, or null if no valid spot was found in the column
        // (e.g. unloaded chunk reads everything as air).
        private int? FindSafeY(int x, int surfaceY, int z)
        {
            var ba = sapi.World.BlockAccessor;
            int maxY = sapi.WorldManager.MapSizeY - 2;
            int ceiling = Math.Min(surfaceY + 8, maxY);

            for (int y = ceiling; y > 1; y--)
            {
                if (ba.GetBlock(x, y, z).Id == 0) continue;

                int feetY = y + 1;
                if (feetY + 1 > maxY) continue;
                if (ba.GetBlock(x, feetY, z).Id != 0) continue;
                if (ba.GetBlock(x, feetY + 1, z).Id != 0) continue;

                return feetY;
            }
            return null;
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex <= 0) return;
            int radiusIdx = winnerIndex - 1;
            if (radiusIdx >= radii.Length) return;

            int radius = radii[radiusIdx];
            if (radius <= 0) return;

            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                var pos = player.Entity?.Pos;
                if (pos == null) continue;

                int targetX = 0, targetZ = 0;
                int? safeY = null;
                for (int attempt = 0; attempt < 20 && safeY == null; attempt++)
                {
                    int x = (int)pos.X + random.Next(-radius, radius + 1);
                    int z = (int)pos.Z + random.Next(-radius, radius + 1);
                    int? surfaceY = sapi.WorldManager.GetSurfacePosY(x, z);
                    if (surfaceY == null) continue;

                    int? candidate = FindSafeY(x, surfaceY.Value, z);
                    if (candidate == null) continue;

                    targetX = x;
                    targetZ = z;
                    safeY = candidate;
                }

                if (safeY == null)
                {
                    sapi.Logger.Warning("[cantwitchconnect] RTP: no safe location found within radius {0}, skipping", radius);
                    continue;
                }

                player.Entity.TeleportToDouble(targetX + 0.5, safeY.Value, targetZ + 0.5);
            }
        }
    }
}
