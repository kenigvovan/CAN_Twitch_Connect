using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class GiveItemAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly string[] itemCodes;
        private readonly int[] quantities;

        public GiveItemAction(ICoreServerAPI sapi, string[] playerNames, string[] itemCodes, int[] quantities)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.itemCodes = itemCodes ?? Array.Empty<string>();
            this.quantities = quantities ?? Array.Empty<int>();
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex <= 0) return;
            int idx = winnerIndex - 1;
            if (idx >= itemCodes.Length) return;

            var loc = new AssetLocation(itemCodes[idx]);
            int qty = idx < quantities.Length && quantities[idx] > 0 ? quantities[idx] : 1;

            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                if (player is not IServerPlayer sp) continue;
                if (sp.Entity == null) continue;

                var stack = BuildStack(loc, qty);
                if (stack == null)
                {
                    sapi.Logger.Warning("[cantwitchconnect] GiveItem: unknown item code '{0}'", itemCodes[idx]);
                    return;
                }
                if (!sp.InventoryManager.TryGiveItemstack(stack, true))
                {
                    sapi.World.SpawnItemEntity(stack, sp.Entity.Pos.XYZ);
                }
            }
        }

        private ItemStack BuildStack(AssetLocation loc, int qty)
        {
            var item = sapi.World.GetItem(loc);
            if (item != null) return new ItemStack(item, qty);
            var block = sapi.World.GetBlock(loc);
            if (block != null) return new ItemStack(block, qty);
            return null;
        }
    }
}
