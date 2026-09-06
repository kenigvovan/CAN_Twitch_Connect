using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class DropInventoryAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;

        public DropInventoryAction(ICoreServerAPI sapi, string[] playerNames)
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
                if (sp.Entity == null) continue;

                var dropAt = sp.Entity.Pos.XYZ;
                foreach (var inv in sp.InventoryManager.Inventories.Values)
                {
                    if (inv.ClassName != "hotbar" && inv.ClassName != "backpack")
                        continue;

                    for (int i = 0; i < inv.Count; i++)
                    {
                        var slot = inv[i];
                        if (slot == null || slot.Empty) continue;
                        var stack = slot.TakeOutWhole();
                        if (stack != null) sapi.World.SpawnItemEntity(stack, dropAt);
                        slot.MarkDirty();
                    }
                }
            }
        }
    }
}
