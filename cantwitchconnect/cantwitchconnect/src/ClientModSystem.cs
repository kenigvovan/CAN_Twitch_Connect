using System;
using cantwitchconnect.Network;
using cantwitchconnect.UI;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect
{
    public sealed class CantwitchClientMod : ModSystem
    {
        private ICoreClientAPI _api;
        private TarotOverlay _overlay;
        private OverlayPlacer _placer;
        private DebugPanel _debugPanel;
        private IClientNetworkChannel _clientChannel;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            _api = api;

            _overlay = new TarotOverlay(api);
            _placer  = new OverlayPlacer(api, _overlay);
            _debugPanel = new DebugPanel(api, _overlay, _placer,
                msg     => _clientChannel?.SendPacket(msg),
                vote    => _clientChannel?.SendPacket(vote),
                trigger => _clientChannel?.SendPacket(trigger),
                toggle  => _clientChannel?.SendPacket(toggle));

            api.Gui.RegisterDialog(_overlay, _placer, _debugPanel);
            _overlay.TryOpen();

            _clientChannel = api.Network.RegisterChannel("cantwitchconnect")
                .RegisterMessageType<PollStartedMessage>()
                .RegisterMessageType<PollFinishedMessage>()
                .RegisterMessageType<ForceMotionMessage>()
                .RegisterMessageType<CommandListMessage>()
                .RegisterMessageType<PreviewRequestMessage>()
                .RegisterMessageType<PollVotesMessage>()
                .RegisterMessageType<DebugVoteMessage>()
                .RegisterMessageType<TriggerPollMessage>()
                .RegisterMessageType<ToggleCommandMessage>()
                .SetMessageHandler<PollStartedMessage>(OnPollStarted)
                .SetMessageHandler<PollFinishedMessage>(OnPollFinished)
                .SetMessageHandler<ForceMotionMessage>(OnForceMotion)
                .SetMessageHandler<CommandListMessage>(OnCommandList)
                .SetMessageHandler<PollVotesMessage>(OnPollVotes);

            api.Input.RegisterHotKey("ctcdebug", "Toggle CantWitchConnect debug panel", GlKeys.F8);
            api.Input.SetHotKeyHandler("ctcdebug", _ =>
            {
                if (api.World.Player?.HasPrivilege(Privilege.controlserver) != true) return false;
                _debugPanel?.Toggle();
                return true;
            });

            api.ChatCommands
                .Create("ctcdebug")
                .WithDescription("Toggle CantWitchConnect debug panel (or press F8)")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ => { _debugPanel?.Toggle(); return TextCommandResult.Success(); });
        }

        private void OnPollStarted(PollStartedMessage msg)
        {
            _debugPanel?.SetActivePoll(msg.PollName, msg.Labels);
            _overlay?.ShowPoll(msg.PollName, msg.Description, msg.Labels,
                msg.DurationSeconds, msg.CardAssets ?? Array.Empty<string>());
        }

        private void OnPollFinished(PollFinishedMessage msg)
        {
            _debugPanel?.ClearActivePoll();
            _overlay?.RevealWinner(msg.HasWinner ? msg.WinnerIndex : -1);
        }

        private void OnForceMotion(ForceMotionMessage msg)
        {
            var entity = _api.World.Player?.Entity;
            if (entity == null) return;
            entity.Pos.Motion.Y = msg.MotionY;
        }

        private void OnCommandList(CommandListMessage msg) =>
            _debugPanel?.SetCommands(msg.Names, msg.EnabledStates);

        private void OnPollVotes(PollVotesMessage msg) =>
            _overlay?.UpdateVotes(msg.Counts);

        public override void Dispose()
        {
            _debugPanel?.Dispose();
            _debugPanel = null;
            _placer?.Dispose();
            _placer = null;
            _overlay?.Dispose();
            _overlay = null;
            base.Dispose();
        }
    }
}
