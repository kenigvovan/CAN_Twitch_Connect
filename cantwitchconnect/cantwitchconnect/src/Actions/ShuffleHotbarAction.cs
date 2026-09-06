using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class ShuffleHotbarAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly Random random = new();

        public ShuffleHotbarAction(ICoreServerAPI sapi, string[] playerNames)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;

            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                if (player is not IServerPlayer sp) continue;
                var hotbar = sp.InventoryManager?.GetHotbarInventory();
                if (hotbar == null || hotbar.Count < 2) continue;

                int n = hotbar.Count;
                var original = new ItemStack[n];
                for (int i = 0; i < n; i++) original[i] = hotbar[i].Itemstack;

                for (int i = n - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (original[i], original[j]) = (original[j], original[i]);
                }

                for (int i = 0; i < n; i++)
                {
                    hotbar[i].Itemstack = original[i];
                    hotbar[i].MarkDirty();
                }
            }
        }
    }
}
