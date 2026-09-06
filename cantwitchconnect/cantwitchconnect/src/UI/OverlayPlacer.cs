using System;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace cantwitchconnect.UI
{
    /// <summary>
    /// Drag-to-move helper for the tarot overlay. A HUD element gets no mouse
    /// events, so the thing being dragged has to be a dialog of its own.
    /// </summary>
    internal sealed class OverlayPlacer : GuiDialog
    {
        private const string GhostKey = "ctc-overlay-ghost";
        private static readonly double[] GhostIdle    = { 1.00, 0.83, 0.42, 0.16 };
        private static readonly double[] GhostDragged = { 0.47, 0.78, 0.36, 0.30 };

        private readonly TarotOverlay overlay;
        private double ghostX, ghostY, ghostW, ghostH;
        private bool dragging;
        private double grabOffsetX, grabOffsetY;

        public OverlayPlacer(ICoreClientAPI capi, TarotOverlay overlay) : base(capi)
        {
            this.overlay = overlay;
        }

        public override string ToggleKeyCombinationCode => null;
        public override bool PrefersUngrabbedMouse => true;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            try
            {
                BuildGhost();
                Compose();
            }
            catch (Exception e)
            {
                capi.Logger.Error("[cantwitchconnect] overlay placer failed to open: {0}", e);
                TryClose();
            }
        }

        private void BuildGhost()
        {
            (ghostW, ghostH) = overlay.PanelSize();
            var (nx, ny) = overlay.NormalisedPos();
            ghostX = nx * capi.Render.FrameWidth;
            ghostY = ny * capi.Render.FrameHeight;
            Clamp();
        }

        private void Clamp()
        {
            ghostX = GameMath.Clamp(ghostX, 0, Math.Max(0, capi.Render.FrameWidth - ghostW));
            ghostY = GameMath.Clamp(ghostY, 0, Math.Max(0, capi.Render.FrameHeight - ghostH));
        }

        private void Compose()
        {
            Composers.ClearComposers();

            // Composer bounds are unscaled units, the overlay works in raw pixels.
            double s = RuntimeEnv.GUIScale;
            var bounds = ElementBounds.Fixed(ghostX / s, ghostY / s, ghostW / s, ghostH / s);
            var inner  = ElementBounds.Fixed(0, 0, ghostW / s, ghostH / s);

            Composers[GhostKey] = capi.Gui
                .CreateCompo("ctc-overlay-ghost", bounds)
                .AddGameOverlay(inner, dragging ? GhostDragged : GhostIdle)
                .BeginChildElements(inner)
                    .AddStaticText(Lang.Get("cantwitchconnect:overlay_placer_ghost"),
                        CairoFont.WhiteSmallText().WithOrientation(EnumTextOrientation.Center),
                        ElementBounds.Fixed(0, ghostH / s / 2 - 12, ghostW / s, 22))
                .EndChildElements()
                .Compose();

            ComposeToolbox();
        }

        private void ComposeToolbox()
        {
            var dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterBottom)
                .WithFixedAlignmentOffset(0, -60);
            var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            Composers["toolbox"] = capi.Gui
                .CreateCompo("ctc-overlay-placer", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(Lang.Get("cantwitchconnect:overlay_placer_title"), OnDone)
                .BeginChildElements(bgBounds)
                    .AddStaticText(Lang.Get("cantwitchconnect:overlay_placer_hint"),
                        CairoFont.WhiteDetailText(),
                        ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, 320, 40))
                    .AddSmallButton(Lang.Get("cantwitchconnect:overlay_placer_done"),
                        () => { OnDone(); return true; },
                        ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, GuiStyle.TitleBarHeight + 46, 0, 0)
                            .WithFixedPadding(9, 4))
                .EndChildElements()
                .Compose();
        }

        private void OnDone()
        {
            overlay.SaveConfig();
            TryClose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            overlay.SaveConfig();
        }

        public override void OnMouseDown(MouseEvent args)
        {
            double mx = capi.Input.MouseX;
            double my = capi.Input.MouseY;

            if (mx >= ghostX && mx <= ghostX + ghostW && my >= ghostY && my <= ghostY + ghostH)
            {
                dragging = true;
                grabOffsetX = mx - ghostX;
                grabOffsetY = my - ghostY;
                Compose();
                args.Handled = true;
                return;
            }

            base.OnMouseDown(args);   // let the toolbox have it
        }

        public override void OnMouseMove(MouseEvent args)
        {
            if (!dragging) { base.OnMouseMove(args); return; }

            ghostX = capi.Input.MouseX - grabOffsetX;
            ghostY = capi.Input.MouseY - grabOffsetY;
            Clamp();

            // Move the existing composer rather than rebuilding it on every mouse event.
            var composer = Composers[GhostKey];
            if (composer?.Bounds != null)
            {
                double s = RuntimeEnv.GUIScale;
                composer.Bounds.fixedX = ghostX / s;
                composer.Bounds.fixedY = ghostY / s;
                composer.Bounds.CalcWorldBounds();
            }

            ApplyToOverlay();
            args.Handled = true;
        }

        public override void OnMouseUp(MouseEvent args)
        {
            if (!dragging) { base.OnMouseUp(args); return; }

            dragging = false;
            ApplyToOverlay();
            overlay.SaveConfig();
            Compose();
            args.Handled = true;
        }

        private void ApplyToOverlay()
        {
            double sw = Math.Max(1, capi.Render.FrameWidth);
            double sh = Math.Max(1, capi.Render.FrameHeight);
            overlay.SetNormalisedPos((float)(ghostX / sw), (float)(ghostY / sh));
        }
    }
}
