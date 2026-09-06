using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace cantwitchconnect.UI
{
    /// <summary>
    /// Tarot card HUD for a running poll. Drawn straight onto the GUI layer instead
    /// of through a composer, because every element animates each frame.
    /// </summary>
    internal sealed class TarotOverlay : HudElement
    {
        private enum State { Hidden, Appearing, Active, Revealing, Closing }

        private const float CardW        = 148f;
        private const float CardH        = 260f;
        private const float CardGap      = 22f;
        private const float CornerScale  = 1.05f;
        private const float CornerMargin = 80f;

        private const float AppearDur    = 0.55f;
        private const float RevealDur    = 3.0f;
        private const float CloseDur     = 0.60f;
        private const float StaleTimeout = 15f;   // grace period after the vote timer runs out
        private const int   MaxPerRow    = 3;
        private const float CardRowGap   = 16f;

        private static readonly Vec4f Brass      = Draw2D.Color(148, 112, 55);
        private static readonly Vec4f BrassDim   = Draw2D.Color(95, 72, 35);
        private static readonly Vec4f BrassFaint = Draw2D.Color(70, 53, 25);

        private static readonly Vec4f[] Accents = {
            Draw2D.Color(165,  52,  42),   // aged red
            Draw2D.Color( 52,  88, 148),   // slate blue
            Draw2D.Color( 52, 128,  68),   // sage green
            Draw2D.Color(182, 145,  40),   // amber
            Draw2D.Color(112,  52, 148),   // dusty violet
            Draw2D.Color(182,  90,  38),   // burnt sienna
        };

        private static readonly string[] Numerals = { "I", "II", "III", "IV", "V", "VI" };

        private readonly Draw2D draw;
        private readonly string _configPath;
        private readonly Dictionary<string, LoadedTexture> _cardCache = new();

        private State    _state = State.Hidden;
        private float    _time;
        private string   _pollName    = "";
        private string   _description = "";
        private string[] _labels      = Array.Empty<string>();
        private LoadedTexture[] _cards = Array.Empty<LoadedTexture>();
        private int      _winnerIdx = -1;
        private float    _voteDur;
        private float    _voteLeft;
        private int[]    _votes = Array.Empty<int>();
        private float    _scale = 1f;

        // Normalised (0..1) position of the panel's top left corner, negative = use the default corner.
        private float _posX = -1f;
        private float _posY = -1f;

        private bool _textDirty;
        private LoadedTexture _titleTex;
        private LoadedTexture _descTex;
        private LoadedTexture _winnerTex;
        private LoadedTexture[] _labelTex   = Array.Empty<LoadedTexture>();
        private LoadedTexture[] _numeralTex = Array.Empty<LoadedTexture>();
        private LoadedTexture[] _pctTex     = Array.Empty<LoadedTexture>();

        public TarotOverlay(ICoreClientAPI capi) : base(capi)
        {
            draw = new Draw2D(capi);
            _configPath = Path.Combine(capi.DataBasePath, "ModData", "cantwitchconnect", "overlayconfig.json");
            LoadConfig();
        }

        public override EnumDialogType DialogType => EnumDialogType.HUD;
        public override string ToggleKeyCombinationCode => null;
        public override bool Focusable => false;
        public override bool ShouldReceiveKeyboardEvents() => false;
        public override float ZSize => 0f;
        public override bool TryClose() => false;
        public override void OnMouseDown(MouseEvent args) { }

        public float Scale
        {
            get => _scale;
            set
            {
                float clamped = GameMath.Clamp(value, 0.5f, 2f);
                if (Math.Abs(clamped - _scale) < 0.001f) return;
                _scale = clamped;
                _textDirty = true;
            }
        }

        // ── public API ────────────────────────────────────────────────────────

        public void ShowPoll(string pollName, string description, string[] labels,
                             int durationSeconds, string[] cardAssets)
        {
            _pollName    = pollName ?? "";
            _description = description ?? "";
            _labels      = labels ?? Array.Empty<string>();
            _cards       = ResolveCards(cardAssets);
            _voteDur     = durationSeconds;
            _voteLeft    = durationSeconds;
            _winnerIdx   = -1;
            _votes       = Array.Empty<int>();
            _time        = 0f;
            _state       = State.Appearing;
            _textDirty   = true;
        }

        public void UpdateVotes(int[] counts)
        {
            if (_state == State.Hidden) return;
            _votes = counts ?? Array.Empty<int>();
        }

        public void RevealWinner(int winnerIdx)
        {
            if (_state == State.Hidden) return;
            _winnerIdx = winnerIdx;
            _time      = 0f;
            _state     = State.Revealing;
            _winnerTex?.Dispose();
            _winnerTex = GenText(WinnerLabel(), 19f * _scale, Draw2D.Color(210, 175, 90));
        }

        /// <summary>Panel size in screen pixels, used by the placer dialog for its ghost.</summary>
        public (double W, double H) PanelSize()
        {
            var m = Metrics(Math.Max(2, _labels.Length));
            return (m.PanelW, m.PanelH);
        }

        public (float X, float Y) NormalisedPos()
        {
            if (_posX >= 0 && _posY >= 0) return (_posX, _posY);
            var (w, h) = PanelSize();
            double sw = Math.Max(1, capi.Render.FrameWidth);
            double sh = Math.Max(1, capi.Render.FrameHeight);
            var (dx, dy) = DefaultTopLeft(w, h);
            return ((float)(dx / sw), (float)(dy / sh));
        }

        public void SetNormalisedPos(float x, float y)
        {
            _posX = GameMath.Clamp(x, 0f, 1f);
            _posY = GameMath.Clamp(y, 0f, 1f);
        }

        // ── rendering ─────────────────────────────────────────────────────────

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (_state != State.Hidden)
            {
                _time += deltaTime;
                if (_state == State.Active)
                    _voteLeft = Math.Max(0f, _voteLeft - deltaTime);
                Tick();
            }

            if (_state == State.Hidden) return;
            if (_textDirty) RebuildTextures();

            Render();
        }

        private void Tick()
        {
            switch (_state)
            {
                case State.Appearing when _time >= AppearDur:
                    _state = State.Active; _time = 0f; break;
                // Safety net: without it a lost or never-sent finish packet leaves
                // the cards on screen forever.
                case State.Active when _voteLeft <= 0f && _time >= _voteDur + StaleTimeout:
                    RevealWinner(-1); break;
                case State.Revealing when _time >= RevealDur:
                    _state = State.Closing; _time = 0f; break;
                case State.Closing when _time >= CloseDur:
                    _state = State.Hidden; break;
            }
        }

        private struct Layout
        {
            public double UiScale, CardW, CardH, CardGap, RowGap, Pad, BarH;
            public double TotalW, CardsAreaH, PanelW, PanelH, HeaderH, DescH;
            public int Cols, Rows;
        }

        private Layout Metrics(int cardCount)
        {
            double ui = _scale * RuntimeEnv.GUIScale;
            double cs = CornerScale * ui;

            var m = new Layout
            {
                UiScale = ui,
                CardW   = CardW * cs,
                CardH   = CardH * cs,
                CardGap = CardGap * cs,
                RowGap  = CardRowGap * cs,
                Pad     = 8 * ui,
                BarH    = 5 * ui,
                HeaderH = _titleTex?.Height ?? 20 * ui,
                DescH   = _descTex?.Height ?? 0
            };

            int n = Math.Max(1, cardCount);
            m.Cols = Math.Min(n, MaxPerRow);
            m.Rows = (n + MaxPerRow - 1) / MaxPerRow;
            m.TotalW = m.Cols * m.CardW + (m.Cols - 1) * m.CardGap;
            m.CardsAreaH = m.Rows * m.CardH + (m.Rows - 1) * m.RowGap;
            m.PanelW = m.TotalW + m.Pad * 2;
            m.PanelH = m.Pad + m.HeaderH + m.DescH + m.Pad + m.CardsAreaH + m.Pad + m.BarH + m.Pad;
            return m;
        }

        private (double X, double Y) DefaultTopLeft(double panelW, double panelH)
        {
            double margin = CornerMargin * _scale * RuntimeEnv.GUIScale;
            return (capi.Render.FrameWidth - margin - panelW,
                    capi.Render.FrameHeight - margin - panelH);
        }

        private void Render()
        {
            int n = _labels.Length;
            if (n == 0) return;

            var m = Metrics(n);
            float ma = MasterAlpha();

            double screenW = capi.Render.FrameWidth;
            double screenH = capi.Render.FrameHeight;

            double bx, by;
            if (_posX >= 0 && _posY >= 0)
            {
                bx = GameMath.Clamp(_posX * screenW, 0, Math.Max(0, screenW - m.PanelW));
                by = GameMath.Clamp(_posY * screenH, 0, Math.Max(0, screenH - m.PanelH));
            }
            else
            {
                (bx, by) = DefaultTopLeft(m.PanelW, m.PanelH);
            }

            if (_state == State.Appearing)
                by += (1f - EaseOut(_time / AppearDur)) * screenH * 0.15;

            // Background - dark aged wood
            draw.Fill(bx, by, m.PanelW, m.PanelH, Draw2D.Color(20, 14, 9, ma * 0.90f));
            draw.Frame(bx, by, m.PanelW, m.PanelH, 2 * m.UiScale, Draw2D.WithAlpha(Brass, ma * 0.78f));
            draw.Frame(bx + 3 * m.UiScale, by + 3 * m.UiScale,
                       m.PanelW - 6 * m.UiScale, m.PanelH - 6 * m.UiScale,
                       1 * m.UiScale, Draw2D.WithAlpha(BrassFaint, ma * 0.63f));

            DrawCornerMarks(bx, by, m.PanelW, m.PanelH, 6 * m.UiScale, Draw2D.WithAlpha(Brass, ma * 0.86f));

            // Dark overlay during reveal
            if (_state == State.Revealing)
            {
                float ot = EaseOut(GameMath.Clamp(_time / RevealDur * 2f, 0f, 1f));
                draw.Fill(bx, by, m.PanelW, m.PanelH, Draw2D.Color(0, 0, 0, ot * 0.55f * ma));
            }

            double headerY = by + m.Pad;
            double centerX = bx + m.Pad + m.TotalW / 2.0;
            draw.TextCentered(_titleTex, centerX, headerY, ma);
            if (_descTex != null)
                draw.TextCentered(_descTex, centerX, headerY + m.HeaderH, ma * 0.80f);

            // Brass rule below the header block
            draw.Fill(bx + 10 * m.UiScale, headerY + m.HeaderH + m.DescH + 1,
                      m.PanelW - 20 * m.UiScale, Math.Max(1, m.UiScale),
                      Draw2D.WithAlpha(BrassDim, ma * 0.70f));

            double cardsY    = headerY + m.HeaderH + m.DescH + m.Pad;
            double cardsLeft = bx + m.Pad;

            float revealT = _state == State.Revealing
                ? EaseOut(GameMath.Clamp(_time / RevealDur, 0f, 1f))
                : _state == State.Closing ? 1f : 0f;

            double winnerScale = CornerScale * m.UiScale * (1f + revealT * 0.35f);
            double winnerRx = cardsLeft + (m.TotalW - m.CardW) / 2.0;
            double winnerRy = cardsY + (m.CardsAreaH - m.CardH) / 2.0;

            for (int i = 0; i < n; i++)
            {
                int row = i / MaxPerRow;
                int col = i % MaxPerRow;
                // Center the last (possibly incomplete) row
                int rowLen = (row == m.Rows - 1) ? n - row * MaxPerRow : MaxPerRow;
                double rowW = rowLen * m.CardW + (rowLen - 1) * m.CardGap;
                double rx = cardsLeft + (m.TotalW - rowW) / 2.0 + col * (m.CardW + m.CardGap);
                double ry = cardsY + row * (m.CardH + m.RowGap);

                float alpha = CardAlpha(i, ma, revealT);
                double cardScale = CornerScale * m.UiScale;

                if ((_state == State.Revealing || _state == State.Closing) && i == _winnerIdx)
                {
                    rx = Lerp(rx, winnerRx, revealT);
                    ry = Lerp(ry, winnerRy, revealT);
                    cardScale = winnerScale;
                }

                DrawCard(rx, ry, i, alpha, cardScale,
                         isWinner: _state == State.Revealing && i == _winnerIdx);
            }

            // Countdown bar
            if (_state is State.Appearing or State.Active)
                DrawCountdown(cardsLeft, cardsY + m.CardsAreaH + m.Pad, m.TotalW, m.BarH, ma);

            // Winner label below the enlarged card
            bool showLabel = (_state == State.Revealing && revealT > 0.3f) || _state == State.Closing;
            if (showLabel && _winnerTex != null)
            {
                float labelAlpha = _state == State.Closing ? ma : EaseOut((revealT - 0.3f) / 0.7f) * ma;
                double cardCenterX = winnerRx + m.CardW / 2.0;
                double cardBottom  = winnerRy + CardH * winnerScale;

                draw.TextCentered(_winnerTex, cardCenterX + 1, cardBottom + 6 * m.UiScale, labelAlpha * 0.7f);
                draw.TextCentered(_winnerTex, cardCenterX, cardBottom + 5 * m.UiScale, labelAlpha);
            }
        }

        private void DrawCornerMarks(double x, double y, double w, double h, double len, Vec4f color)
        {
            double t = Math.Max(1, len / 3.0);
            // top left, top right, bottom left, bottom right
            draw.Fill(x, y, len, t, color);
            draw.Fill(x, y, t, len, color);
            draw.Fill(x + w - len, y, len, t, color);
            draw.Fill(x + w - t, y, t, len, color);
            draw.Fill(x, y + h - t, len, t, color);
            draw.Fill(x, y + h - len, t, len, color);
            draw.Fill(x + w - len, y + h - t, len, t, color);
            draw.Fill(x + w - t, y + h - len, t, len, color);
        }

        private void DrawCard(double px, double py, int idx, float alpha, double scale, bool isWinner)
        {
            if (alpha <= 0.004f) return;

            double w = CardW * scale;
            double h = CardH * scale;

            Vec4f acc      = Accents[idx % Accents.Length];
            Vec4f bg       = Draw2D.Color(26, 18, 11, alpha * 0.96f);
            Vec4f brassA   = Draw2D.WithAlpha(Brass, alpha * 0.75f);
            Vec4f brassDim = Draw2D.WithAlpha(BrassDim, alpha * 0.39f);
            Vec4f accFill  = Draw2D.WithAlpha(acc, alpha * 0.11f);
            Vec4f accLine  = Draw2D.WithAlpha(acc, alpha * 0.31f);

            // Shadow
            draw.Fill(px + 4 * scale, py + 6 * scale, w, h, Draw2D.Color(0, 0, 0, alpha * 0.47f));

            // Body plus accent tint
            draw.Fill(px, py, w, h, bg);
            draw.Fill(px, py, w, h, accFill);

            // Pulsing gold glow around the winner
            if (isWinner)
            {
                float pulse = (MathF.Sin(_time * 4f) * 0.25f + 0.75f) * alpha;
                for (int gi = 4; gi >= 1; gi--)
                {
                    double e = gi * 5f * scale;
                    draw.Frame(px - e, py - e, w + e * 2, h + e * 2, 2 * scale,
                        Draw2D.WithAlpha(Brass, pulse * (0.9f / gi)));
                }
                if (_time < 0.35f)
                {
                    float fl = (1f - _time / 0.35f) * 0.45f * alpha;
                    draw.Fill(px, py, w, h, Draw2D.Color(220, 185, 100, fl));
                }
            }

            draw.Frame(px, py, w, h, 2 * scale, brassA);

            // Inner frame
            double m  = 7 * scale;
            double sh = 14 * scale;
            double ix = px + m, iy = py + m;
            double iw = w - m * 2, ih = h - m * 2;
            draw.Frame(ix, iy, iw, ih, Math.Max(1, scale), brassDim);
            DrawCornerMarks(ix, iy, iw, ih, 5 * scale, brassA);

            // Ornament strips
            draw.Fill(ix + 1, iy + 1, iw - 2, sh, accFill);
            draw.Fill(ix + 4 * scale, iy + sh, iw - 8 * scale, Math.Max(1, scale), accLine);
            draw.Fill(ix + 1, iy + ih - sh - 1, iw - 2, sh, accFill);
            draw.Fill(ix + 4 * scale, iy + ih - sh, iw - 8 * scale, Math.Max(1, scale), accLine);

            double labelAreaH = 52 * scale;
            double cx = px + w / 2.0;

            // Card art - tarot image or roman numeral fallback
            var tex = idx < _cards.Length ? _cards[idx] : null;
            double artTop = iy + sh + 2 * scale;
            double artBottom = iy + ih - labelAreaH - 2 * scale;
            if (tex != null && tex.TextureId != 0)
            {
                if (artBottom > artTop)
                    draw.Image(tex, ix + 1, artTop, iw - 2, artBottom - artTop, alpha);
            }
            else if (idx < _numeralTex.Length)
            {
                draw.TextCentered(_numeralTex[idx], cx,
                    artTop + (artBottom - artTop) / 2.0 - (_numeralTex[idx]?.Height ?? 0) / 2.0, alpha);
            }

            // Divider and option label
            draw.Fill(ix + 5 * scale, iy + ih - labelAreaH, iw - 10 * scale, Math.Max(1, scale),
                Draw2D.WithAlpha(acc, alpha * 0.22f));

            if (idx < _labelTex.Length)
                draw.TextCentered(_labelTex[idx], cx, iy + ih - labelAreaH + 5 * scale, alpha * 0.90f);

            if (_state is State.Appearing or State.Active or State.Revealing)
                DrawVoteBar(ix, iy, iw, ih, idx, alpha, scale);
        }

        private void DrawVoteBar(double ix, double iy, double iw, double ih, int cardIdx, float alpha, double scale)
        {
            int total = 0;
            foreach (var c in _votes) total += c;
            int myVotes = cardIdx < _votes.Length ? _votes[cardIdx] : 0;

            double barH = 8 * scale;
            double barY = iy + ih - barH - 3 * scale;
            double barX = ix + 3 * scale;
            double barW = iw - 6 * scale;

            draw.Fill(barX, barY, barW, barH, Draw2D.Color(30, 22, 14, alpha * 0.78f));
            draw.Frame(barX, barY, barW, barH, Math.Max(1, scale), Draw2D.WithAlpha(BrassFaint, alpha * 0.55f));

            if (total > 0 && myVotes > 0)
            {
                double pct = (double)myVotes / total;
                draw.Fill(barX, barY, barW * pct, barH,
                    Draw2D.WithAlpha(Accents[cardIdx % Accents.Length], alpha * 0.78f));
            }

            int shown = total > 0 ? (int)MathF.Round((float)myVotes / total * 100f) : 0;
            UpdatePercentTexture(cardIdx, shown);
            if (cardIdx < _pctTex.Length)
                draw.TextCentered(_pctTex[cardIdx], ix + iw / 2.0,
                    barY - (_pctTex[cardIdx]?.Height ?? 0) - 2 * scale, alpha * 0.74f);
        }

        private void DrawCountdown(double bx, double by, double barW, double barH, float ma)
        {
            float pct = _voteDur > 0 ? GameMath.Clamp(_voteLeft / _voteDur, 0f, 1f) : 0f;

            draw.Fill(bx, by, barW, barH, Draw2D.Color(25, 18, 12, ma * 0.70f));
            draw.Frame(bx, by, barW, barH, 1, Draw2D.WithAlpha(BrassFaint, ma * 0.55f));
            if (pct > 0.001f)
            {
                Vec4f col = pct > 0.35f
                    ? Draw2D.Color(88, 148, 72, ma * 0.82f)    // sage green
                    : Draw2D.Color(165, 60, 45, ma * 0.82f);   // aged red
                draw.Fill(bx, by, barW * pct, barH, col);
            }
        }

        // ── textures ──────────────────────────────────────────────────────────

        private LoadedTexture[] ResolveCards(string[] assets)
        {
            if (assets == null) return Array.Empty<LoadedTexture>();
            var result = new LoadedTexture[assets.Length];
            for (int i = 0; i < assets.Length; i++)
                result[i] = ResolveCard(assets[i]);
            return result;
        }

        private LoadedTexture ResolveCard(string assetLocation)
        {
            if (string.IsNullOrWhiteSpace(assetLocation)) return null;
            if (_cardCache.TryGetValue(assetLocation, out var cached))
                return cached;

            LoadedTexture tex = null;
            try
            {
                tex = new LoadedTexture(capi);
                capi.Render.GetOrLoadTexture(new AssetLocation(assetLocation), ref tex);
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[cantwitchconnect] could not load tarot card texture '{0}': {1}",
                    assetLocation, ex.Message);
                tex = null;
            }

            _cardCache[assetLocation] = tex;
            return tex;
        }

        private void RebuildTextures()
        {
            _textDirty = false;
            DisposeTextTextures();

            // Font sizes stay unscaled: CairoFont multiplies by GUIScale itself.
            int n = _labels.Length;

            _titleTex = GenText(_pollName.ToUpperInvariant(), 16f * _scale, Draw2D.Color(222, 200, 155));
            _descTex  = string.IsNullOrEmpty(_description)
                ? null
                : GenText(_description, 14f * _scale, Draw2D.Color(168, 148, 110));

            double cardInnerW = (CardW - 20) * CornerScale * _scale * RuntimeEnv.GUIScale;
            _labelTex   = new LoadedTexture[n];
            _numeralTex = new LoadedTexture[n];
            _pctTex     = new LoadedTexture[n];

            for (int i = 0; i < n; i++)
            {
                _labelTex[i] = GenText(_labels[i], 14f * _scale, Draw2D.Color(218, 196, 155), cardInnerW);
                string num = i < Numerals.Length ? Numerals[i] : (i + 1).ToString();
                _numeralTex[i] = GenText(num, 42f * _scale, Accents[i % Accents.Length]);
                _pctTex[i] = new LoadedTexture(capi);
            }

            if (_winnerIdx != -1 || _state is State.Revealing or State.Closing)
                _winnerTex = GenText(WinnerLabel(), 19f * _scale, Draw2D.Color(210, 175, 90));
        }

        private string WinnerLabel()
            => _winnerIdx >= 0 && _winnerIdx < _labels.Length
                ? _labels[_winnerIdx]
                : Lang.Get("cantwitchconnect:tarot_nomajority");

        private void UpdatePercentTexture(int idx, int percent)
        {
            if (idx >= _pctTex.Length) return;
            string text = percent + "%";
            var tex = _pctTex[idx];
            if (tex != null && tex.TextureId != 0 && _pctValues.TryGetValue(idx, out var prev) && prev == text)
                return;

            _pctValues[idx] = text;
            var font = Font(11f * _scale, Draw2D.Color(185, 165, 125));
            capi.Gui.TextTexture.GenOrUpdateTextTexture(text, font, ref tex);
            _pctTex[idx] = tex;
        }

        private readonly Dictionary<int, string> _pctValues = new();

        private LoadedTexture GenText(string text, float fontSize, Vec4f color, double maxWidth = 0)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var font = Font(fontSize, color);
            return maxWidth > 0
                ? capi.Gui.TextTexture.GenTextTexture(text, font, (int)maxWidth, null, EnumTextOrientation.Center)
                : capi.Gui.TextTexture.GenTextTexture(text, font);
        }

        private static CairoFont Font(float fontSize, Vec4f color)
            => CairoFont.WhiteSmallText()
                .WithFontSize(fontSize)
                .WithColor(new double[] { color.R, color.G, color.B, 1.0 });

        private void DisposeTextTextures()
        {
            _titleTex?.Dispose();  _titleTex = null;
            _descTex?.Dispose();   _descTex = null;
            _winnerTex?.Dispose(); _winnerTex = null;
            foreach (var t in _labelTex)   t?.Dispose();
            foreach (var t in _numeralTex) t?.Dispose();
            foreach (var t in _pctTex)     t?.Dispose();
            _labelTex   = Array.Empty<LoadedTexture>();
            _numeralTex = Array.Empty<LoadedTexture>();
            _pctTex     = Array.Empty<LoadedTexture>();
            _pctValues.Clear();
        }

        // ── persisted placement ───────────────────────────────────────────────

        public void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                var json = JsonSerializer.Serialize(new { x = _posX, y = _posY, scale = _scale });
                File.WriteAllText(_configPath, json);
            }
            catch { /* non-critical */ }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("x", out var xEl) && root.TryGetProperty("y", out var yEl))
                {
                    float x = xEl.GetSingle();
                    float y = yEl.GetSingle();
                    // The position is a fraction of the screen; anything larger is a
                    // stale pixel value from an earlier format.
                    if (x >= 0 && x <= 1 && y >= 0 && y <= 1) { _posX = x; _posY = y; }
                }
                if (root.TryGetProperty("scale", out var scEl))
                    _scale = GameMath.Clamp(scEl.GetSingle(), 0.5f, 2f);
            }
            catch { /* non-critical */ }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private float MasterAlpha() => _state switch
        {
            State.Appearing => EaseOut(_time / AppearDur),
            State.Closing   => 1f - EaseIn(_time / CloseDur),
            _               => 1f
        };

        private float CardAlpha(int i, float ma, float revealT)
        {
            // Non-winner cards fade out quickly, gone by halfway through the reveal
            if (_state == State.Revealing && i != _winnerIdx)
                return ma * Math.Max(0f, 1f - revealT * 2.5f);
            if (_state == State.Closing && i != _winnerIdx)
                return 0f;
            return ma;
        }

        private static double Lerp(double a, double b, float t) => a + (b - a) * t;
        private static float EaseOut(float t) { float c = GameMath.Clamp(t, 0f, 1f); return 1f - (1f - c) * (1f - c); }
        private static float EaseIn(float t)  { float c = GameMath.Clamp(t, 0f, 1f); return c * c; }

        public override void Dispose()
        {
            SaveConfig();
            DisposeTextTextures();
            foreach (var tex in _cardCache.Values) tex?.Dispose();
            _cardCache.Clear();
            draw.Dispose();
            base.Dispose();
        }
    }
}
