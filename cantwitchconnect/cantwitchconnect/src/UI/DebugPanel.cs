using System;
using System.Collections.Generic;
using cantwitchconnect.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace cantwitchconnect.UI
{
    /// <summary>Admin panel: start polls, preview the HUD, toggle commands, cast test votes.</summary>
    internal sealed class DebugPanel : GuiDialog
    {
        private const double RowH      = 28;
        private const double SwitchW   = 30;
        private const double NameW     = 150;
        private const double BtnGap    = 4;
        private const double PanelW    = 620;

        private readonly TarotOverlay _overlay;
        private readonly OverlayPlacer _placer;
        private readonly Action<PreviewRequestMessage> _sendPreview;
        private readonly Action<DebugVoteMessage> _sendVote;
        private readonly Action<TriggerPollMessage> _sendTrigger;
        private readonly Action<ToggleCommandMessage> _sendToggle;

        private string[] _commands = Array.Empty<string>();
        private readonly Dictionary<string, bool> _enabledStates = new(StringComparer.OrdinalIgnoreCase);
        private int _winnerIndex;
        private string _activePollName = "";
        private string[] _activePollLabels = Array.Empty<string>();

        public DebugPanel(ICoreClientAPI capi, TarotOverlay overlay, OverlayPlacer placer,
            Action<PreviewRequestMessage> sendPreview,
            Action<DebugVoteMessage> sendVote,
            Action<TriggerPollMessage> sendTrigger,
            Action<ToggleCommandMessage> sendToggle) : base(capi)
        {
            _overlay     = overlay;
            _placer      = placer;
            _sendPreview = sendPreview;
            _sendVote    = sendVote;
            _sendTrigger = sendTrigger;
            _sendToggle  = sendToggle;
        }

        public override string ToggleKeyCombinationCode => null;

        public void Toggle()
        {
            if (IsOpened()) TryClose();
            else TryOpen();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        public void SetCommands(string[] names, bool[] enabledStates = null)
        {
            _commands = names ?? Array.Empty<string>();
            _enabledStates.Clear();
            for (int i = 0; i < _commands.Length; i++)
            {
                bool en = enabledStates == null || i >= enabledStates.Length || enabledStates[i];
                _enabledStates[_commands[i]] = en;
            }
            if (IsOpened()) Compose();
        }

        public void SetActivePoll(string name, string[] labels)
        {
            _activePollName   = name ?? "";
            _activePollLabels = labels ?? Array.Empty<string>();
            if (IsOpened()) Compose();
        }

        public void ClearActivePoll()
        {
            _activePollName   = "";
            _activePollLabels = Array.Empty<string>();
            if (IsOpened()) Compose();
        }

        // ── composition ───────────────────────────────────────────────────────

        private void Compose()
        {
            var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftTop)
                .WithFixedAlignmentOffset(20, 20);
            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var composer = capi.Gui
                .CreateCompo("ctc-debug", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("cantwitchconnect:debug_title"), () => TryClose())
                .BeginChildElements(bgBounds);

            double y = GuiStyle.TitleBarHeight + 8;

            y = ComposeVoteRow(composer, y);
            y = ComposeWinnerRow(composer, y);
            y = ComposeCommandRows(composer, y);
            ComposeOverlayRow(composer, y);

            var built = composer.EndChildElements().Compose();

            SingleComposer?.Dispose();
            SingleComposer = built;

            ApplyStates();
        }

        private double ComposeVoteRow(GuiComposer composer, double y)
        {
            if (_activePollLabels.Length == 0) return y;

            composer.AddStaticText(Lang.Get("cantwitchconnect:debug_vote", _activePollName),
                CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, PanelW, 22));
            y += 24;

            double x = 0;
            for (int i = 0; i < _activePollLabels.Length; i++)
            {
                int idx = i;
                double w = 60 + _activePollLabels[i].Length * 6;
                if (x + w > PanelW) { x = 0; y += RowH; }

                composer.AddSmallButton($"[{i}] {_activePollLabels[i]}",
                    () => { _sendVote(new DebugVoteMessage { OptionIndex = idx }); return true; },
                    ElementBounds.Fixed(x, y, w, RowH - 4), EnumButtonStyle.Normal, "vote" + i);
                x += w + BtnGap;
            }
            return y + RowH + 8;
        }

        private double ComposeWinnerRow(GuiComposer composer, double y)
        {
            composer.AddStaticText(Lang.Get("cantwitchconnect:debug_winner", _winnerIndex),
                CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 4, 190, 22), "winnerlabel");

            composer.AddSmallButton("-", () => { SetWinnerIndex(_winnerIndex - 1); return true; },
                ElementBounds.Fixed(190, y, 34, RowH - 4));
            composer.AddSmallButton("+", () => { SetWinnerIndex(_winnerIndex + 1); return true; },
                ElementBounds.Fixed(190 + 34 + BtnGap, y, 34, RowH - 4));

            return y + RowH + 8;
        }

        private void SetWinnerIndex(int value)
        {
            _winnerIndex = Math.Max(0, value);
            Compose();
        }

        private double ComposeCommandRows(GuiComposer composer, double y)
        {
            if (_commands.Length == 0)
            {
                composer.AddStaticText(Lang.Get("cantwitchconnect:debug_waiting"),
                    CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, y, PanelW, 22));
                return y + RowH;
            }

            foreach (var name in _commands)
            {
                string cmd = name;

                composer.AddSwitch(on => _sendToggle(new ToggleCommandMessage { CommandName = cmd, Enabled = on }),
                    ElementBounds.Fixed(0, y, SwitchW, RowH - 4), "sw-" + cmd, SwitchW, 3);

                composer.AddStaticText(cmd, CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(SwitchW + 8, y + 4, NameW, 22));

                double x = SwitchW + 8 + NameW;
                x = AddRowButton(composer, "btn-start-" + cmd, Lang.Get("cantwitchconnect:debug_start"), x, y, 70,
                    () => _sendTrigger(new TriggerPollMessage { CommandName = cmd }));
                x = AddRowButton(composer, "btn-hud-" + cmd, Lang.Get("cantwitchconnect:debug_hud"), x, y, 70,
                    () => _sendPreview(new PreviewRequestMessage { CommandName = cmd }));
                x = AddRowButton(composer, "btn-run-" + cmd,
                    Lang.Get("cantwitchconnect:debug_run", _winnerIndex), x, y, 110,
                    () => _sendPreview(new PreviewRequestMessage
                    {
                        CommandName   = cmd,
                        HasWinner     = true,
                        WinnerIndex   = _winnerIndex,
                        ExecuteAction = true
                    }));
                AddRowButton(composer, "btn-rnd-" + cmd, Lang.Get("cantwitchconnect:debug_random"), x, y, 110,
                    () => _sendPreview(new PreviewRequestMessage { CommandName = cmd }));

                y += RowH;
            }
            return y + 8;
        }

        private double AddRowButton(GuiComposer composer, string key, string text,
                                    double x, double y, double w, Action onClick)
        {
            composer.AddSmallButton(text, () => { onClick(); return true; },
                ElementBounds.Fixed(x, y, w, RowH - 4), EnumButtonStyle.Normal, key);
            return x + w + BtnGap;
        }

        private void ComposeOverlayRow(GuiComposer composer, double y)
        {
            composer.AddStaticText(Lang.Get("cantwitchconnect:debug_scale"), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(0, y + 4, 130, 22));

            composer.AddSlider(OnScaleChanged, ElementBounds.Fixed(130, y, 200, RowH - 4), "scale");

            composer.AddSmallButton(Lang.Get("cantwitchconnect:debug_move"), () =>
                {
                    _placer.TryOpen();
                    return true;
                },
                ElementBounds.Fixed(345, y, 150, RowH - 4));
        }

        private bool OnScaleChanged(int percent)
        {
            _overlay.Scale = percent / 100f;
            _overlay.SaveConfig();
            return true;
        }

        /// <summary>Values that can only be set after Compose().</summary>
        private void ApplyStates()
        {
            foreach (var name in _commands)
            {
                bool enabled = !_enabledStates.TryGetValue(name, out var en) || en;
                SingleComposer.GetSwitch("sw-" + name)?.SetValue(enabled);

                foreach (var prefix in new[] { "btn-start-", "btn-hud-", "btn-run-", "btn-rnd-" })
                {
                    var button = SingleComposer.GetButton(prefix + name);
                    if (button != null) button.Enabled = enabled;
                }
            }

            SingleComposer.GetSlider("scale")?.SetValues((int)Math.Round(_overlay.Scale * 100), 50, 200, 5);
        }

    }
}
