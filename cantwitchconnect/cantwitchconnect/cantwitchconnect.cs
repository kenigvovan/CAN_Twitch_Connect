using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cantwitchconnect.Infrastructure;
using cantwitchconnect.Network;
using cantwitchconnect.src;
using cantwitchconnect.Voting;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect
{
    public class cantwitchconnect : ModSystem
    {
        private static readonly TimeSpan RefreshLeadTime = TimeSpan.FromMinutes(5);
        private const int MinRefreshDelayMs = 60_000;
        private const int MaxRefreshFailures = 5;

        private ICoreServerAPI sapi;
        private Config config;
        private PollsHandler pollsHandler;
        private TwitchClient twitchClient;
        private IMainThreadDispatcher dispatcher;
        private IServerNetworkChannel serverChannel;

        private int refreshFailureCount;
        private long? pendingRefreshCallbackId;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            config = LoadOrInitConfig(api);
            api.StoreModConfig(config, Mod.Info.ModID);

            dispatcher = new ServerMainThreadDispatcher(api);

            serverChannel = sapi.Network.RegisterChannel("cantwitchconnect")
                .RegisterMessageType<PollStartedMessage>()
                .RegisterMessageType<PollFinishedMessage>()
                .RegisterMessageType<ForceMotionMessage>()
                .RegisterMessageType<CommandListMessage>()
                .RegisterMessageType<PreviewRequestMessage>()
                .RegisterMessageType<PollVotesMessage>()
                .RegisterMessageType<DebugVoteMessage>()
                .RegisterMessageType<TriggerPollMessage>()
                .RegisterMessageType<ToggleCommandMessage>()
                .SetMessageHandler<PreviewRequestMessage>(OnPreviewRequest)
                .SetMessageHandler<DebugVoteMessage>((player, msg) =>
                {
                    if (!player.HasPrivilege(Privilege.controlserver)) return;
                    pollsHandler?.Vote(msg.OptionIndex, $"[debug]{Guid.NewGuid():N}");
                })
                .SetMessageHandler<TriggerPollMessage>((player, msg) =>
                {
                    if (!player.HasPrivilege(Privilege.controlserver)) return;
                    pollsHandler?.Trigger(msg.CommandName);
                })
                .SetMessageHandler<ToggleCommandMessage>((player, msg) =>
                {
                    if (!player.HasPrivilege(Privilege.controlserver)) return;
                    pollsHandler?.SetEnabled(msg.CommandName, msg.Enabled);
                    BroadcastCommandList();
                });

            sapi.Event.PlayerJoin += OnPlayerJoin;

            RegisterAdminCommands();

            if (config.OfflineDebug)
                InitOfflineMode();
            else
                _ = Task.Run(InitTwitchAsync);
        }

        private Config LoadOrInitConfig(ICoreServerAPI api)
        {
            var cfg = api.LoadModConfig<Config>(Mod.Info.ModID);
            if (cfg == null)
            {
                cfg = new Config();
                cfg.InitConfig();
                return cfg;
            }

            FillMissingCommands(cfg);
            return cfg;
        }

        // Only seed the defaults when the config carries no commands of its own.
        private static void FillMissingCommands(Config cfg)
        {
            if (cfg.Commands != null && cfg.Commands.Count > 0) return;
            var defaults = new Config();
            defaults.InitConfig();
            cfg.Commands = defaults.Commands;
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            var names = config.Commands?.Select(c => c.Name).ToArray() ?? Array.Empty<string>();
            var states = pollsHandler?.GetEnabledStates() ?? new Dictionary<string, bool>();
            var enabled = names.Select(n => states.TryGetValue(n, out var v) ? v : true).ToArray();
            serverChannel.SendPacket(new CommandListMessage { Names = names, EnabledStates = enabled }, player);
        }

        private void BroadcastCommandList()
        {
            var names = config.Commands?.Select(c => c.Name).ToArray() ?? Array.Empty<string>();
            var states = pollsHandler?.GetEnabledStates() ?? new Dictionary<string, bool>();
            var enabled = names.Select(n => states.TryGetValue(n, out var v) ? v : true).ToArray();
            serverChannel.BroadcastPacket(new CommandListMessage { Names = names, EnabledStates = enabled });
        }

        private void OnPreviewRequest(IServerPlayer player, PreviewRequestMessage msg)
        {
            if (!player.HasPrivilege(Privilege.controlserver)) return;

            var cmd = config.Commands?.Find(c =>
                string.Equals(c.Name, msg.CommandName, StringComparison.OrdinalIgnoreCase));
            if (cmd == null) return;

            StartPreview(player, cmd, msg.HasWinner, msg.WinnerIndex, msg.ExecuteAction);
        }

        // Shared by the debug panel and by /ctc preview.
        private int StartPreview(IServerPlayer player, Actions.CommandConfig cmd,
                                 bool hasWinner, int winnerIndex, bool executeAction)
        {
            var answers  = cmd.Answers ?? new List<AnswerInfo>();
            var labels   = answers.Select(a => Vintagestory.API.Config.Lang.Get(a.LangCode)).ToArray();
            var cards    = answers.Select(a => a.TarotCard ?? "").ToArray();
            int duration = cmd.SecondsForVote > 0 ? cmd.SecondsForVote : 15;

            serverChannel.SendPacket(new PollStartedMessage
            {
                PollName        = cmd.Name,
                Description     = cmd.Description ?? "",
                Labels          = labels,
                DurationSeconds = duration,
                CardAssets      = cards
            }, player);

            int winner = hasWinner
                ? winnerIndex
                : (answers.Count > 0 ? new Random().Next(0, answers.Count) : 0);

            int revealMs = hasWinner ? 500 : duration * 1000;
            sapi.Event.RegisterCallback(_ =>
            {
                serverChannel.SendPacket(new PollFinishedMessage { HasWinner = true, WinnerIndex = winner }, player);

                if (executeAction)
                {
                    var built = Actions.CommandFactory.Build(cmd, sapi, serverChannel, config);
                    if (built.HasValue && winner >= 0 && winner < answers.Count)
                    {
                        try { built.Value.Action.Execute(winner, answers[winner]); }
                        catch (Exception ex) { sapi.Logger.Error("[cantwitchconnect] preview action '{0}' failed: {1}", cmd.Name, ex); }
                    }
                }
            }, revealMs);

            return winner;
        }

        private void RegisterAdminCommands()
        {
            sapi.ChatCommands
                .Create("ctc")
                .WithDescription("cantwitchconnect admin commands")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("pause")
                    .WithDescription("Cancel active poll and stop accepting new ones")
                    .HandleWith(_ =>
                    {
                        if (pollsHandler == null)
                            return TextCommandResult.Error("Twitch integration not initialized");
                        pollsHandler.Pause();
                        return TextCommandResult.Success("Polls paused");
                    })
                .EndSubCommand()
                .BeginSubCommand("resume")
                    .WithDescription("Resume accepting polls")
                    .HandleWith(_ =>
                    {
                        if (pollsHandler == null)
                            return TextCommandResult.Error("Twitch integration not initialized");
                        pollsHandler.Resume();
                        return TextCommandResult.Success("Polls resumed");
                    })
                .EndSubCommand()
                .BeginSubCommand("preview")
                    .WithDescription("Preview the HUD overlay for a command (usage: /ctc preview <name> [winnerIndex])")
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("commandname"),
                        sapi.ChatCommands.Parsers.OptionalInt("winnerIndex", -1))
                    .HandleWith(args =>
                    {
                        string name = args.Parsers[0].GetValue() as string ?? "";
                        int forceWinner = (int)args.Parsers[1].GetValue();

                        var cmd = config.Commands?.Find(c =>
                            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                        if (cmd == null)
                        {
                            var names = config.Commands?.Select(c => c.Name) ?? Enumerable.Empty<string>();
                            return TextCommandResult.Error(
                                $"Unknown command '{name}'. Available: {string.Join(", ", names)}");
                        }

                        if (args.Caller.Player is not IServerPlayer player)
                            return TextCommandResult.Error("Must be called by a player");

                        int winner = StartPreview(player, cmd, forceWinner >= 0, forceWinner, false);
                        return TextCommandResult.Success($"Preview started for '{cmd.Name}', winner {winner}");
                    })
                .EndSubCommand()
                .BeginSubCommand("trigger")
                    .WithDescription("(Debug) Manually trigger a poll by command name")
                    .WithArgs(sapi.ChatCommands.Parsers.Word("commandname"))
                    .HandleWith(args =>
                    {
                        if (pollsHandler == null)
                            return TextCommandResult.Error("Twitch integration not initialized");
                        string name = args.Parsers[0].GetValue() as string ?? "";
                        if (string.IsNullOrWhiteSpace(name))
                            return TextCommandResult.Error("Usage: /ctc trigger <commandname>");
                        bool started = pollsHandler.Trigger(name);
                        if (!started)
                        {
                            var names = config.Commands?.Select(c => c.Name) ?? Enumerable.Empty<string>();
                            return TextCommandResult.Error(
                                $"Could not start '{name}' (vote in progress, paused, or unknown). Available: {string.Join(", ", names)}");
                        }
                        return TextCommandResult.Success($"Poll '{name}' started");
                    })
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Hot-reload config from disk (commands + cooldowns, keeps live tokens)")
                    .HandleWith(_ =>
                    {
                        try
                        {
                            var fresh = sapi.LoadModConfig<Config>(Mod.Info.ModID);
                            if (fresh == null)
                                return TextCommandResult.Error("Config file missing");

                            fresh.AccessCode = config.AccessCode;
                            fresh.AccessToken = config.AccessToken;
                            fresh.RefreshToken = config.RefreshToken;
                            fresh.TokenExpiry = config.TokenExpiry;

                            FillMissingCommands(fresh);

                            config = fresh;
                            pollsHandler?.Reload(fresh);
                            return TextCommandResult.Success($"Config reloaded: {fresh.Commands.Count} commands, global cooldown {fresh.GlobalCooldownSeconds}s");
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Error("[cantwitchconnect] reload failed: {0}", ex);
                            return TextCommandResult.Error("Reload failed: " + ex.Message);
                        }
                    })
                .EndSubCommand();
        }

        private void InitOfflineMode()
        {
            sapi.Logger.Notification("[cantwitchconnect] OfflineDebug: Twitch disabled, running offline");
            pollsHandler = new PollsHandler(config, sapi, null, dispatcher, serverChannel);
            pollsHandler.Start();
        }

        private async Task InitTwitchAsync()
        {
            try
            {
                if (!await EnsureTokens())
                {
                    sapi.Logger.Warning("[cantwitchconnect] Twitch tokens unavailable, integration disabled.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(config.Channel))
                {
                    sapi.Logger.Warning("[cantwitchconnect] channel not configured, integration disabled.");
                    return;
                }

                var credentials = new ConnectionCredentials(config.Channel, "oauth:" + config.AccessToken);
                twitchClient = new TwitchClient();
                twitchClient.Initialize(credentials, config.Channel);

                twitchClient.OnConnected += OnConnected;
                twitchClient.OnJoinedChannel += OnJoinedChannel;

                pollsHandler = new PollsHandler(config, sapi, twitchClient, dispatcher, serverChannel);

                await twitchClient.ConnectAsync();

                dispatcher.Enqueue(() =>
                {
                    pollsHandler.Start();
                    ScheduleTokenRefresh();
                });
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[cantwitchconnect] Twitch init failed: {0}", ex);
            }
        }

        private Task OnJoinedChannel(object sender, OnJoinedChannelArgs e) => Task.CompletedTask;

        private Task OnConnected(object sender, OnConnectedEventArgs e)
            => twitchClient.JoinChannelAsync(config.Channel);

        private async Task<bool> EnsureTokens()
        {
            if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
                return false;

            var api = new TwitchAPI();

            if (!string.IsNullOrWhiteSpace(config.AccessToken))
            {
                try
                {
                    var valid = await api.Auth.ValidateAccessTokenAsync(config.AccessToken);
                    if (valid != null)
                    {
                        config.TokenExpiry = DateTime.UtcNow.AddSeconds(valid.ExpiresIn);
                        dispatcher.Enqueue(() => sapi.StoreModConfig(config, Mod.Info.ModID));
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[cantwitchconnect] token validation failed: {0}", ex.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(config.RefreshToken))
            {
                try
                {
                    var refreshed = await api.Auth.RefreshAuthTokenAsync(config.RefreshToken, config.ClientSecret, config.ClientId);
                    ApplyRefreshedTokens(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresIn);
                    return true;
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[cantwitchconnect] refresh token failed: {0}", ex.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(config.AccessCode))
            {
                try
                {
                    var fresh = await api.Auth.GetAccessTokenFromCodeAsync(
                        config.AccessCode, config.ClientSecret, config.RedirectUri, config.ClientId);
                    ApplyRefreshedTokens(fresh.AccessToken, fresh.RefreshToken, fresh.ExpiresIn);
                    config.AccessCode = "";
                    dispatcher.Enqueue(() => sapi.StoreModConfig(config, Mod.Info.ModID));
                    return true;
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error("[cantwitchconnect] access code exchange failed: {0}", ex);
                }
            }

            return false;
        }

        private void ApplyRefreshedTokens(string accessToken, string refreshToken, int expiresInSeconds)
        {
            if (!string.IsNullOrWhiteSpace(accessToken))
                config.AccessToken = accessToken;
            if (!string.IsNullOrWhiteSpace(refreshToken))
                config.RefreshToken = refreshToken;
            config.TokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds);
            // Called from a background task, so the file write goes through the main thread.
            dispatcher.Enqueue(() => sapi.StoreModConfig(config, Mod.Info.ModID));

            TryUpdateLiveClientCredentials();
        }

        private void TryUpdateLiveClientCredentials()
        {
            if (twitchClient == null) return;
            if (string.IsNullOrWhiteSpace(config.Channel) || string.IsNullOrWhiteSpace(config.AccessToken)) return;
            // TwitchLib rejects credential changes while the socket is up; the live
            // session keeps working and the new token is picked up on reconnect.
            if (twitchClient.IsConnected) return;

            try
            {
                var creds = new ConnectionCredentials(config.Channel, "oauth:" + config.AccessToken);
                twitchClient.SetConnectionCredentials(creds);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[cantwitchconnect] could not update live Twitch credentials: {0}", ex.Message);
            }
        }

        private void ScheduleTokenRefresh()
        {
            if (string.IsNullOrWhiteSpace(config.RefreshToken)) return;
            if (config.TokenExpiry == default) return;

            int delayMs;
            if (refreshFailureCount > 0)
            {
                int backoffMinutes = Math.Min(16, 1 << (refreshFailureCount - 1));
                delayMs = backoffMinutes * 60_000;
            }
            else
            {
                var until = config.TokenExpiry - RefreshLeadTime - DateTime.UtcNow;
                delayMs = (int)Math.Clamp(until.TotalMilliseconds, MinRefreshDelayMs, int.MaxValue);
            }

            if (pendingRefreshCallbackId.HasValue)
            {
                try { sapi.Event.UnregisterCallback(pendingRefreshCallbackId.Value); }
                catch { /* id may already be gone */ }
            }

            pendingRefreshCallbackId = sapi.Event.RegisterCallback(
                dt => { _ = Task.Run(RefreshAndRescheduleAsync); },
                delayMs);
        }

        private async Task RefreshAndRescheduleAsync()
        {
            try
            {
                var api = new TwitchAPI();
                var refreshed = await api.Auth.RefreshAuthTokenAsync(config.RefreshToken, config.ClientSecret, config.ClientId);
                ApplyRefreshedTokens(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresIn);
                refreshFailureCount = 0;
                sapi.Logger.Notification("[cantwitchconnect] access token refreshed, next expiry {0:u}", config.TokenExpiry);
            }
            catch (Exception ex)
            {
                refreshFailureCount++;
                sapi.Logger.Error("[cantwitchconnect] auto-refresh failed ({0}/{1}): {2}",
                    refreshFailureCount, MaxRefreshFailures, ex);
            }

            if (refreshFailureCount >= MaxRefreshFailures)
            {
                sapi.Logger.Error("[cantwitchconnect] giving up on auto-refresh after {0} failures; restart required", MaxRefreshFailures);
                pendingRefreshCallbackId = null;
                return;
            }

            dispatcher.Enqueue(ScheduleTokenRefresh);
        }

        public override void Dispose()
        {
            if (pendingRefreshCallbackId.HasValue && sapi != null)
            {
                try { sapi.Event.UnregisterCallback(pendingRefreshCallbackId.Value); }
                catch { /* ignore on shutdown */ }
                pendingRefreshCallbackId = null;
            }

            pollsHandler?.Dispose();
            pollsHandler = null;

            if (twitchClient != null)
            {
                try
                {
                    twitchClient.OnConnected -= OnConnected;
                    twitchClient.OnJoinedChannel -= OnJoinedChannel;
                    if (twitchClient.IsConnected)
                        twitchClient.DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    sapi?.Logger.Warning("[cantwitchconnect] Twitch disconnect failed: {0}", ex.Message);
                }
                twitchClient = null;
            }

            base.Dispose();
        }
    }
}
