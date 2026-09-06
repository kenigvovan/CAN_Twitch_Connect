using System;
using System.Collections.Generic;
using System.Linq;
using cantwitchconnect.Voting;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public sealed class SpawnEntityAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly string[] entityCodes;
        private readonly Random random = new();

        public SpawnEntityAction(ICoreServerAPI sapi, string[] playerNames, string[] entityCodes)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.entityCodes = entityCodes ?? Array.Empty<string>();
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex <= 0) return;
            if (entityCodes.Length == 0)
            {
                sapi.Logger.Warning("[cantwitchconnect] SpawnEntity: no entity codes configured");
                return;
            }

            var types = new List<EntityProperties>(entityCodes.Length);
            foreach (var code in entityCodes)
            {
                var props = sapi.World.GetEntityType(new AssetLocation(code));
                if (props != null)
                    types.Add(props);
                else
                    sapi.Logger.Warning("[cantwitchconnect] SpawnEntity: unknown entity code '{0}'", code);
            }
            if (types.Count == 0)
            {
                sapi.Logger.Warning("[cantwitchconnect] SpawnEntity: none of the entity codes resolved, skipping");
                return;
            }

            var targets = PlayerLookup.FindOnline(sapi, playerNames).ToList();
            if (targets.Count == 0)
            {
                sapi.Logger.Warning("[cantwitchconnect] SpawnEntity: no target players online (looking for: {0})",
                    string.Join(", ", playerNames));
                return;
            }

            int amount = winnerIndex;
            foreach (var player in targets)
            {
                if (player.Entity == null) continue;
                for (int i = 0; i < amount; i++)
                {
                    var props  = types[random.Next(types.Count)];
                    var entity = sapi.World.ClassRegistry.CreateEntity(props);
                    if (entity == null)
                    {
                        sapi.Logger.Warning("[cantwitchconnect] SpawnEntity: CreateEntity returned null for '{0}'", props.Code);
                        continue;
                    }

                    var pos = player.Entity.ServerPos;
                    entity.ServerPos.SetPos(
                        pos.X + random.Next(-2, 3),
                        pos.Y + random.Next(0, 2),
                        pos.Z + random.Next(-2, 3));
                    entity.Pos.SetFrom(entity.ServerPos);
                    sapi.World.SpawnEntity(entity);
                }
            }
        }
    }
}
