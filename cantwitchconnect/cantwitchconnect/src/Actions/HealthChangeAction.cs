using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace cantwitchconnect.Actions
{
    public sealed class HealthChangeAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly string[] playerNames;
        private readonly HealthChangeType changeType;

        public HealthChangeAction(ICoreServerAPI sapi, string[] playerNames, HealthChangeType changeType)
        {
            this.sapi = sapi;
            this.playerNames = playerNames ?? Array.Empty<string>();
            this.changeType = changeType;
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;
            foreach (var player in PlayerLookup.FindOnline(sapi, playerNames))
            {
                var hb = player.Entity?.GetBehavior<EntityBehaviorHealth>();
                if (hb == null) continue;

                switch (changeType)
                {
                    case HealthChangeType.RESTORE_FULL:
                        hb.Health = hb.MaxHealth;
                        break;
                    case HealthChangeType.RESTORE_HALF:
                        hb.Health = Math.Min(hb.MaxHealth, hb.Health + hb.MaxHealth / 2f);
                        break;
                    case HealthChangeType.REMOVE_HALF:
                        hb.Health = Math.Max(0, hb.Health - hb.MaxHealth / 2f);
                        break;
                    case HealthChangeType.SET_HALF:
                        hb.Health = hb.MaxHealth / 2f;
                        break;
                }
            }
        }
    }
}
