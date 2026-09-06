using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace cantwitchconnect.Actions
{
    public sealed class ChangeWeatherAction : IVoteAction
    {
        private readonly ICoreServerAPI sapi;
        private readonly WeatherChangeType changeType;
        private readonly int overrideSeconds;
        private long? resetCallbackId;

        public ChangeWeatherAction(ICoreServerAPI sapi, WeatherChangeType changeType, int overrideSeconds)
        {
            this.sapi = sapi;
            this.changeType = changeType;
            this.overrideSeconds = overrideSeconds;
        }

        public void Execute(int winnerIndex, AnswerInfo winner)
        {
            if (winnerIndex != 0) return;

            var wsys = sapi.ModLoader.GetModSystem<WeatherSystemServer>(true);
            if (wsys == null) return;

            Apply(wsys, changeType == WeatherChangeType.START_RAIN ? 1f : 0f);

            // Without a reset the override sticks forever and natural weather never returns.
            if (resetCallbackId.HasValue)
            {
                try { sapi.Event.UnregisterCallback(resetCallbackId.Value); } catch { }
                resetCallbackId = null;
            }
            if (overrideSeconds > 0)
            {
                resetCallbackId = sapi.Event.RegisterCallback(_ =>
                {
                    resetCallbackId = null;
                    var ws = sapi.ModLoader.GetModSystem<WeatherSystemServer>(true);
                    if (ws != null) Apply(ws, null);
                }, overrideSeconds * 1000);
            }
        }

        private static void Apply(WeatherSystemServer wsys, float? precipitation)
        {
            wsys.OverridePrecipitation = precipitation;
            wsys.serverChannel.BroadcastPacket(new WeatherConfigPacket
            {
                OverridePrecipitation = wsys.OverridePrecipitation,
                RainCloudDaysOffset = wsys.RainCloudDaysOffset
            }, Array.Empty<IServerPlayer>());
        }
    }
}
