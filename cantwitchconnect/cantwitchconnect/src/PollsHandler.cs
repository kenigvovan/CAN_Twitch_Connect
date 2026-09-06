using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using cantwitchconnect.Actions;
using cantwitchconnect.Infrastructure;
using cantwitchconnect.Network;
using cantwitchconnect.Voting;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace cantwitchconnect.src
{
    public sealed class PollsHandler : IDisposable
    {
        private readonly ICoreServerAPI sapi;
        private readonly TwitchClient client;
        private readonly IMainThreadDispatcher dispatcher;
        private readonly IServerNetworkChannel networkChannel;

        private readonly Dictionary<string, (VoteDefinition Def, IVoteAction Action)> commands;
        private readonly Dictionary<string, (VoteDefinition Def, IVoteAction Action)> _allCommands
            = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runtimeDisabled = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> votes = new();
        private readonly Dictionary<string, DateTime> commandCooldownEnd = new(StringComparer.OrdinalIgnoreCase);
        private readonly object voteLock = new();

        private Config config;
        private bool voteInProgress;
        private bool paused;
        private VoteDefinition currentDef;
        private IVoteAction currentAction;
        private DateTime currentVotingEnd;
        private DateTime globalCooldownEnd;
        private long? tickListenerId;
        private int[] lastSentCounts;
        private readonly Random random = new();

        public PollsHandler(Config config, ICoreServerAPI sapi, TwitchClient client,
                            IMainThreadDispatcher dispatcher, IServerNetworkChannel networkChannel)
        {
            this.config = config;
            this.sapi = sapi;
            this.client = client;
            this.dispatcher = dispatcher;
            this.networkChannel = networkChannel;

            commands = new Dictionary<string, (VoteDefinition Def, IVoteAction Action)>(StringComparer.OrdinalIgnoreCase);
            PopulateCommands(config, commands);
            foreach (var kv in commands) _allCommands[kv.Key] = kv.Value;

            if (client != null)
                client.OnMessageReceived += OnChatMessage;
        }

        public void Start()
        {
            tickListenerId = sapi.Event.RegisterGameTickListener(_ => TickVoting(), 500);
        }

        public void Dispose()
        {
            if (client != null)
                client.OnMessageReceived -= OnChatMessage;

            if (tickListenerId.HasValue)
            {
                try { sapi.Event.UnregisterGameTickListener(tickListenerId.Value); } catch { }
                tickListenerId = null;
            }
        }

        public void Pause()
        {
            VoteDefinition cancelled;
            lock (voteLock)
            {
                paused = true;
                cancelled = voteInProgress ? currentDef : null;
                voteInProgress = false;
                currentDef = null;
                currentAction = null;
                votes.Clear();
                lastSentCounts = null;
            }

            // Clients keep the overlay on screen until they are told the poll is over.
            if (cancelled != null)
            {
                networkChannel?.BroadcastPacket(new PollFinishedMessage { HasWinner = false });
                BroadcastIngameFinish(cancelled, -1);
            }
        }

        public void Resume()
        {
            lock (voteLock) { paused = false; }
        }

        public void Reload(Config newConfig)
        {
            var rebuilt = new Dictionary<string, (VoteDefinition Def, IVoteAction Action)>(StringComparer.OrdinalIgnoreCase);
            PopulateCommands(newConfig, rebuilt);

            lock (voteLock)
            {
                this.config = newConfig;
                _allCommands.Clear();
                foreach (var kv in rebuilt) _allCommands[kv.Key] = kv.Value;

                commands.Clear();
                foreach (var kv in _allCommands)
                    if (!_runtimeDisabled.Contains(kv.Key))
                        commands[kv.Key] = kv.Value;
            }
        }

        public void SetEnabled(string commandName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(commandName)) return;
            lock (voteLock)
            {
                if (!_allCommands.ContainsKey(commandName)) return;

                if (enabled)
                {
                    _runtimeDisabled.Remove(commandName);
                    if (_allCommands.TryGetValue(commandName, out var pair))
                        commands[commandName] = pair;
                }
                else
                {
                    _runtimeDisabled.Add(commandName);
                    commands.Remove(commandName);
                }
            }
        }

        public Dictionary<string, bool> GetEnabledStates()
        {
            lock (voteLock)
            {
                var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in _allCommands.Keys)
                    result[name] = !_runtimeDisabled.Contains(name);
                return result;
            }
        }

        public void Vote(int optionIndex, string voterName)
        {
            lock (voteLock)
            {
                if (!voteInProgress || currentDef == null) return;
                if (optionIndex < 0 || optionIndex >= currentDef.Answers.Length) return;
                votes[voterName] = optionIndex;
            }
        }

        public bool Trigger(string commandName)
        {
            VoteDefinition toAnnounce = null;
            lock (voteLock)
            {
                if (voteInProgress || paused) return false;
                if (!commands.TryGetValue(commandName, out var pair)) return false;

                currentDef = pair.Def;
                currentAction = pair.Action;
                voteInProgress = true;
                votes.Clear();
                lastSentCounts = null;
                currentVotingEnd = DateTime.UtcNow.AddSeconds(pair.Def.SecondsForVote);
                toAnnounce = pair.Def;
            }

            AnnounceStart(toAnnounce);
            BroadcastIngameStart(toAnnounce);
            BroadcastNetworkStart(toAnnounce);
            return true;
        }

        private void PopulateCommands(Config src, Dictionary<string, (VoteDefinition Def, IVoteAction Action)> dst)
        {
            if (src.Commands == null) return;
            foreach (var entry in src.Commands)
            {
                var built = CommandFactory.Build(entry, sapi, networkChannel, src);
                if (!built.HasValue) continue;
                var pair = built.Value;
                if (!pair.Definition.Enabled) continue;
                dst[pair.Definition.Name] = (pair.Definition, pair.Action);
            }
        }

        private Task OnChatMessage(object sender, OnMessageReceivedArgs e)
        {
            var chat = e.ChatMessage;
            var msg = chat?.Message;
            var user = chat?.Username;
            if (string.IsNullOrEmpty(msg) || string.IsNullOrEmpty(user))
                return Task.CompletedTask;

            bool privileged   = chat.IsBroadcaster || chat.UserType >= UserType.Moderator;
            bool isSubscriber = chat.Badges != null && chat.Badges.Any(b =>
                string.Equals(b.Key, "subscriber", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.Key, "founder",    StringComparison.OrdinalIgnoreCase));
            if (!privileged && !IsAllowedUser(user)) return Task.CompletedTask;

            if (msg.Equals("!help", StringComparison.OrdinalIgnoreCase)
                || msg.StartsWith("!help ", StringComparison.OrdinalIgnoreCase))
            {
                string helpText;
                lock (voteLock) { helpText = BuildHelpText(); }
                SafeSend(helpText);
                return Task.CompletedTask;
            }

            VoteDefinition toAnnounce = null;

            lock (voteLock)
            {
                if (voteInProgress && currentDef != null)
                {
                    if (!votes.ContainsKey(user)
                        && (!currentDef.SubscribersOnly || privileged || isSubscriber))
                    {
                        int idx = currentDef.TryPlaceVote(msg);
                        if (idx >= 0) votes[user] = idx;
                    }
                    return Task.CompletedTask;
                }

                if (!msg.StartsWith("!")) return Task.CompletedTask;
                if (paused) return Task.CompletedTask;

                var name = msg.Substring(1).Split(' ').FirstOrDefault("");
                if (!commands.TryGetValue(name, out var pair)) return Task.CompletedTask;

                var now = DateTime.UtcNow;
                if (now < globalCooldownEnd) return Task.CompletedTask;
                if (commandCooldownEnd.TryGetValue(pair.Def.Name, out var cdEnd) && now < cdEnd)
                    return Task.CompletedTask;
                if (pair.Def.SubscribersOnly && !privileged && !isSubscriber)
                    return Task.CompletedTask;

                currentDef = pair.Def;
                currentAction = pair.Action;
                voteInProgress = true;
                votes.Clear();
                lastSentCounts = null;
                currentVotingEnd = now.AddSeconds(pair.Def.SecondsForVote);
                toAnnounce = pair.Def;
            }

            if (toAnnounce != null)
            {
                AnnounceStart(toAnnounce);
                BroadcastIngameStart(toAnnounce);
                BroadcastNetworkStart(toAnnounce);
            }
            return Task.CompletedTask;
        }

        private bool IsAllowedUser(string user)
        {
            var bl = config.VoterBlacklist;
            if (bl != null)
            {
                for (int i = 0; i < bl.Count; i++)
                    if (string.Equals(bl[i], user, StringComparison.OrdinalIgnoreCase)) return false;
            }
            var wl = config.VoterWhitelist;
            if (wl == null || wl.Count == 0) return true;
            for (int i = 0; i < wl.Count; i++)
                if (string.Equals(wl[i], user, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private string BuildHelpText()
        {
            if (commands.Count == 0) return Lang.Get("cantwitchconnect:no_commands");
            var names = commands.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Select(n => "!" + n);
            return Lang.Get("cantwitchconnect:help", string.Join(", ", names));
        }

        private void BroadcastIngame(string text)
        {
            if (!config.AnnounceIngame) return;
            if (string.IsNullOrEmpty(text)) return;

            dispatcher.Enqueue(() =>
            {
                try
                {
                    sapi.BroadcastMessageToAllGroups(text, EnumChatType.Notification);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error("[cantwitchconnect] ingame broadcast failed: {0}", ex);
                }
            });
        }

        private void BroadcastIngameStart(VoteDefinition def)
        {
            var options = string.Join(" / ", def.Answers.Select(a => Lang.Get(a.LangCode)));
            BroadcastIngame(Lang.Get("cantwitchconnect:ingame_poll_start", def.Name, def.SecondsForVote, options));
        }

        private void BroadcastIngameFinish(VoteDefinition def, int winner)
        {
            if (winner < 0 || winner >= def.Answers.Length)
            {
                BroadcastIngame(Lang.Get("cantwitchconnect:ingame_poll_nomajority", def.Name));
                return;
            }
            var label = Lang.Get(def.Answers[winner].LangCode);
            BroadcastIngame(Lang.Get("cantwitchconnect:ingame_poll_finish", def.Name, label));
        }

        private void TickVoting()
        {
            if (!voteInProgress) return;
            if (DateTime.UtcNow < currentVotingEnd)
            {
                BroadcastVoteCounts();
                return;
            }

            VoteDefinition def;
            IVoteAction action;
            Dictionary<string, int> snapshot;

            lock (voteLock)
            {
                if (!voteInProgress) return;
                if (DateTime.UtcNow < currentVotingEnd) return;

                def = currentDef;
                action = currentAction;
                snapshot = new Dictionary<string, int>(votes);

                voteInProgress = false;
                currentDef = null;
                currentAction = null;
                votes.Clear();

                var now = DateTime.UtcNow;
                int globalCd = config.GlobalCooldownSeconds;
                if (globalCd > 0) globalCooldownEnd = now.AddSeconds(globalCd);
                if (def != null && def.CooldownSeconds > 0)
                    commandCooldownEnd[def.Name] = now.AddSeconds(def.CooldownSeconds);
            }

            FinishVote(def, action, snapshot);
        }

        private void FinishVote(VoteDefinition def, IVoteAction action, Dictionary<string, int> snapshot)
        {
            if (def == null || action == null) return;

            if (snapshot.Count < def.MinVotes)
            {
                SafeSend(Lang.Get(def.FinishLangCode, def.Name, "-"));
                BroadcastIngameFinish(def, -1);
                networkChannel?.BroadcastPacket(new PollFinishedMessage { HasWinner = false });
                return;
            }

            // On a tie the leader is picked at random instead of by vote arrival order.
            var grouped = snapshot.GroupBy(kv => kv.Value).ToList();
            int best = grouped.Max(g => g.Count());
            var leaders = grouped.Where(g => g.Count() == best).Select(g => g.Key).ToList();
            int winner = leaders[random.Next(leaders.Count)];

            string winnerLabel = winner >= 0 && winner < def.Answers.Length
                ? def.Answers[winner].AnswerName
                : winner.ToString();
            SafeSend(Lang.Get(def.FinishLangCode, def.Name, winnerLabel));
            BroadcastIngameFinish(def, winner);
            networkChannel?.BroadcastPacket(new PollFinishedMessage { HasWinner = true, WinnerIndex = winner });

            if (winner < 0 || winner >= def.Answers.Length) return;

            var winningAnswer = def.Answers[winner];
            int delayMs = (int)(config.ActionDelaySeconds * 1000f);
            if (delayMs <= 0)
            {
                dispatcher.Enqueue(() =>
                {
                    try { action.Execute(winner, winningAnswer); }
                    catch (Exception ex) { sapi.Logger.Error("[cantwitchconnect] action '{0}' failed: {1}", def.Name, ex); }
                });
            }
            else
            {
                sapi.Event.RegisterCallback(_ =>
                {
                    try { action.Execute(winner, winningAnswer); }
                    catch (Exception ex) { sapi.Logger.Error("[cantwitchconnect] action '{0}' failed: {1}", def.Name, ex); }
                }, delayMs);
            }
        }

        private void BroadcastVoteCounts()
        {
            VoteDefinition def;
            lock (voteLock) { def = currentDef; }
            if (def == null) return;

            int n = def.Answers.Length;
            var counts = new int[n];
            foreach (var kv in votes)
                if (kv.Value >= 0 && kv.Value < n) counts[kv.Value]++;

            if (lastSentCounts != null && lastSentCounts.Length == n)
            {
                bool changed = false;
                for (int i = 0; i < n && !changed; i++)
                    changed = lastSentCounts[i] != counts[i];
                if (!changed) return;
            }

            lastSentCounts = counts;
            networkChannel?.BroadcastPacket(new PollVotesMessage { Counts = counts });
        }

        private void BroadcastNetworkStart(VoteDefinition def)
        {
            var labels = def.Answers.Select(a => Lang.Get(a.LangCode)).ToArray();
            var msg = new PollStartedMessage
            {
                PollName        = def.Name,
                Description     = def.Description,
                Labels          = labels,
                DurationSeconds = def.SecondsForVote,
                CardAssets      = def.Answers.Select(a => a.TarotCard ?? "").ToArray()
            };
            dispatcher.Enqueue(() => networkChannel?.BroadcastPacket(msg));
        }

        private void AnnounceStart(VoteDefinition def)
        {
            SafeSend(Lang.Get(def.StartLangCode, def.Name));

            var sb = new StringBuilder();
            for (int i = 0; i < def.Answers.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                var a = def.Answers[i];
                sb.Append(Lang.Get(a.LangCode))
                  .Append(": ")
                  .Append(string.Join(',', a.Aliases ?? Array.Empty<string>()));
            }
            SafeSend(sb.ToString());
        }

        private void SafeSend(string text)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(config.Channel)) return;
            if (client == null || !client.IsConnected) return;
            try
            {
                _ = client.SendMessageAsync(config.Channel, text)
                    .ContinueWith(
                        t => sapi.Logger.Error("[cantwitchconnect] send failed: {0}", t.Exception),
                        TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[cantwitchconnect] send threw: {0}", ex);
            }
        }
    }
}
