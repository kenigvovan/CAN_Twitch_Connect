using System;
using cantwitchconnect.Voting;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    public static class CommandFactory
    {
        public static (VoteDefinition Definition, IVoteAction Action)? Build(CommandConfig cfg, ICoreServerAPI sapi,
            IServerNetworkChannel channel = null, Config global = null)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.Name)) return null;

            var definition = new VoteDefinition(
                cfg.Name,
                cfg.Description,
                cfg.Answers?.ToArray() ?? Array.Empty<AnswerInfo>(),
                cfg.SecondsForVote,
                cfg.MinimumVotes,
                cfg.StartLangCode,
                cfg.FinishLangCode,
                cfg.Enabled,
                cfg.CooldownSeconds,
                cfg.SubscribersOnly);

            // Falls back to the global list, then to "everyone online" (empty array).
            var playerList = cfg.PlayerNames != null && cfg.PlayerNames.Count > 0
                ? cfg.PlayerNames
                : global?.DefaultPlayerNames;
            var players = playerList?.ToArray() ?? Array.Empty<string>();

            if (cfg.Kind == CommandKind.CallChatCommand
                && (cfg.AllowedChatCommands == null || cfg.AllowedChatCommands.Count == 0))
            {
                sapi.Logger.Warning(
                    "[cantwitchconnect] command '{0}' has empty AllowedChatCommands, will be a no-op",
                    cfg.Name);
            }

            IVoteAction action = cfg.Kind switch
            {
                CommandKind.KillPlayers     => new KillPlayersAction(sapi, players),
                CommandKind.HealthChange    => new HealthChangeAction(sapi, players, cfg.HealthChangeType ?? HealthChangeType.RESTORE_FULL),
                CommandKind.SpawnEntity     => new SpawnEntityAction(sapi, players, cfg.EntityCodes?.ToArray() ?? Array.Empty<string>()),
                CommandKind.TossPlayer      => new TossPlayerAction(sapi, players, cfg.Heights?.ToArray() ?? Array.Empty<int>(), channel),
                CommandKind.RtpPlayer       => new RtpPlayerAction(sapi, players, cfg.Radius?.ToArray() ?? Array.Empty<int>()),
                CommandKind.SetOnFire       => new SetOnFireAction(sapi, players),
                CommandKind.ChangeWeather   => new ChangeWeatherAction(sapi, cfg.WeatherChangeType ?? WeatherChangeType.START_RAIN, global?.WeatherOverrideSeconds ?? 600),
                CommandKind.CallChatCommand => new CallChatCommandAction(sapi, cfg.CommandToCall, cfg.AllowedChatCommands?.ToArray() ?? Array.Empty<string>()),
                CommandKind.GiveItem        => new GiveItemAction(sapi, players, cfg.ItemCodes?.ToArray() ?? Array.Empty<string>(), cfg.Quantities?.ToArray() ?? Array.Empty<int>()),
                CommandKind.DropInventory   => new DropInventoryAction(sapi, players),
                CommandKind.ShuffleHotbar   => new ShuffleHotbarAction(sapi, players),
                CommandKind.LightningStrike => new LightningStrikeAction(sapi, players, cfg.LightningRadius ?? 8),
                _ => null
            };

            if (action == null) return null;
            return (definition, action);
        }
    }
}
