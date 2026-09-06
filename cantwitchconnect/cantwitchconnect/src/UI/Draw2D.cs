using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace cantwitchconnect.UI
{
    /// <summary>
    /// Immediate 2D drawing on top of the GUI: filled rects, frames, images and
    /// pre-rendered text textures. Everything solid is the same white texture
    /// tinted through the color parameter.
    /// </summary>
    internal sealed class Draw2D : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private int whiteTexId;

        public float Z = 50f;

        public Draw2D(ICoreClientAPI capi)
        {
            this.capi = capi;

            using var surface = new ImageSurface(Format.Argb32, 4, 4);
            using (var ctx = new Context(surface))
            {
                ctx.SetSourceRGBA(1, 1, 1, 1);
                ctx.Paint();
            }
            whiteTexId = capi.Gui.LoadCairoTexture(surface, false);
        }

        public void Fill(double x, double y, double w, double h, Vec4f color)
        {
            if (w <= 0 || h <= 0 || color.A <= 0.001f) return;
            capi.Render.RenderTexture(whiteTexId, x, y, w, h, Z, color);
        }

        public void Frame(double x, double y, double w, double h, double thickness, Vec4f color)
        {
            if (w <= 0 || h <= 0 || color.A <= 0.001f) return;
            Fill(x, y, w, thickness, color);
            Fill(x, y + h - thickness, w, thickness, color);
            Fill(x, y, thickness, h, color);
            Fill(x + w - thickness, y, thickness, h, color);
        }

        public void Image(LoadedTexture tex, double x, double y, double w, double h, float alpha)
        {
            if (tex == null || tex.TextureId == 0 || alpha <= 0.001f) return;
            capi.Render.RenderTexture(tex.TextureId, x, y, w, h, Z, new Vec4f(1f, 1f, 1f, alpha));
        }

        /// <summary>Draws a text texture centered horizontally on centerX.</summary>
        public void TextCentered(LoadedTexture tex, double centerX, double y, float alpha, double scale = 1.0)
        {
            if (tex == null || tex.TextureId == 0 || alpha <= 0.001f) return;
            double w = tex.Width * scale;
            double h = tex.Height * scale;
            capi.Render.RenderTexture(tex.TextureId, centerX - w / 2.0, y, w, h, Z, new Vec4f(1f, 1f, 1f, alpha));
        }

        public static Vec4f Color(byte r, byte g, byte b, float a = 1f)
            => new Vec4f(r / 255f, g / 255f, b / 255f, a);

        public static Vec4f WithAlpha(Vec4f color, float a)
            => new Vec4f(color.R, color.G, color.B, a);

        public void Dispose()
        {
            if (whiteTexId != 0)
            {
                capi.Gui.DeleteTexture(whiteTexId);
                whiteTexId = 0;
            }
        }
    }
}
