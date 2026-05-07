using WinFont = System.Drawing.Font;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Koovra.Cto.AutocadAddin.UI
{
    internal static class FuturisticTheme
    {
        // ── Design tokens — Vezeel Group palette ─────────────────────────────
        // Brand
        internal static readonly Color BrandNavy        = Color.FromArgb(0x1B, 0x2D, 0x4F);
        internal static readonly Color BrandNavyHi      = Color.FromArgb(0x24, 0x3A, 0x66);
        internal static readonly Color BrandSteel       = Color.FromArgb(0x5B, 0x7F, 0xA8);
        internal static readonly Color BrandSteelHi     = Color.FromArgb(0x7B, 0xA0, 0xC8);
        internal static readonly Color BrandMist        = Color.FromArgb(0x8F, 0xA8, 0xC4);
        // Surfaces
        internal static readonly Color BgBase           = Color.FromArgb(0x0E, 0x1A, 0x2E);
        internal static readonly Color BgPanel          = Color.FromArgb(0x16, 0x24, 0x3D);
        internal static readonly Color BgPanelHover     = Color.FromArgb(0x1E, 0x30, 0x52);
        internal static readonly Color BgElev           = Color.FromArgb(0x1A, 0x29, 0x44);
        // Borders
        internal static readonly Color BorderSubtle     = Color.FromArgb(0x24, 0x3A, 0x5A);
        internal static readonly Color BorderStrong     = Color.FromArgb(0x35, 0x55, 0x81);
        internal static readonly Color BorderFocus      = Color.FromArgb(0x5B, 0x7F, 0xA8);
        internal static readonly Color Divider          = Color.FromArgb(0x24, 0x8F, 0xA8, 0xC4);
        // CTAs
        internal static readonly Color CtaFrom         = Color.FromArgb(0x1B, 0x2D, 0x4F);
        internal static readonly Color CtaTo           = Color.FromArgb(0x5B, 0x7F, 0xA8);
        internal static readonly Color CtaText         = Color.FromArgb(0xF1, 0xF6, 0xFC);
        // Status
        internal static readonly Color Success         = Color.FromArgb(0x4F, 0xB2, 0x86);
        internal static readonly Color SuccessHi       = Color.FromArgb(0x3F, 0x99, 0x74);
        internal static readonly Color Warn            = Color.FromArgb(0xE0, 0xA9, 0x5B);
        internal static readonly Color Danger          = Color.FromArgb(0xD9, 0x6A, 0x7C);
        internal static readonly Color DangerBg        = Color.FromArgb(0x21, 0xD9, 0x6A, 0x7C);
        // Text
        internal static readonly Color TextPrimary     = Color.FromArgb(0xE8, 0xEF, 0xF8);
        internal static readonly Color TextSecondary   = Color.FromArgb(0x9C, 0xB1, 0xCC);
        internal static readonly Color TextMuted       = Color.FromArgb(0x5F, 0x77, 0x99);
        // Aliases para compatibilidad
        internal static readonly Color AccentPrimary   = BrandSteel;
        internal static readonly Color AccentSecondary = BrandSteelHi;
        internal static readonly Color AccentGlow      = Color.FromArgb(0x38, 0x5B, 0x7F, 0xA8);

        // ─────────────────────────────────────────────────────────────────────
        // HeaderPanel
        // ─────────────────────────────────────────────────────────────────────

        internal class HeaderPanel : Panel
        {
            private readonly Form         _owner;
            private readonly Func<double> _getGlowPhase;
            private readonly Func<float>  _getShimmerX;
            private readonly string       _title;
            private readonly string       _subtitle;
            private readonly string       _tag;
            private readonly bool         _showClose;
            private Point _dragStart;
            private bool  _dragging;

            /// <param name="owner">Form que contiene el panel.</param>
            /// <param name="getGlowPhase">Delegado que retorna la fase actual del glow (puede ser null).</param>
            /// <param name="getShimmerX">Delegado que retorna la posición del shimmer (puede ser null).</param>
            /// <param name="title">Texto principal del header.</param>
            /// <param name="subtitle">Texto secundario debajo del título (puede ser null o vacío).</param>
            /// <param name="tag">Etiqueta monoespacio derecha (puede ser null o vacío).</param>
            /// <param name="showClose">Si true, muestra el botón [×] en esquina superior derecha.</param>
            public HeaderPanel(
                Form         owner,
                Func<double> getGlowPhase,
                Func<float>  getShimmerX,
                string       title     = "CTO",
                string       subtitle  = null,
                string       tag       = null,
                bool         showClose = true)
            {
                _owner        = owner;
                _getGlowPhase = getGlowPhase;
                _getShimmerX  = getShimmerX;
                _title        = title     ?? string.Empty;
                _subtitle     = subtitle  ?? string.Empty;
                _tag          = tag       ?? string.Empty;
                _showClose    = showClose;

                BackColor      = Color.Transparent;
                DoubleBuffered = true;
                MouseDown     += OnMD;
                MouseMove     += OnMM;
                MouseUp       += OnMU;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Gradient BgBase → BgPanel vertical
                using (var brush = new LinearGradientBrush(
                    ClientRectangle, BgBase, BgPanel, LinearGradientMode.Vertical))
                    g.FillRectangle(brush, ClientRectangle);

                // Title
                using (var f = new WinFont("Segoe UI", 14f, FontStyle.Bold))
                using (var b = new SolidBrush(TextPrimary))
                    g.DrawString(_title, f, b, new PointF(16, 10));

                // Tag (top-right) with pulse alpha
                if (!string.IsNullOrEmpty(_tag))
                {
                    double glowPhase = _getGlowPhase != null ? _getGlowPhase() : 0.0;
                    int tagAlpha = (int)(160 + 60 * Math.Sin(glowPhase * 1.5));
                    using (var f = new WinFont("Consolas", 8f))
                    using (var b = new SolidBrush(Color.FromArgb(tagAlpha, TextMuted)))
                    {
                        var sz = g.MeasureString(_tag, f);
                        float tagRight = _showClose ? Width - 40 : Width - 10;
                        g.DrawString(_tag, f, b, new PointF(tagRight - sz.Width, 14));
                    }
                }

                // Subtitle (only if non-empty)
                if (!string.IsNullOrEmpty(_subtitle))
                {
                    using (var f = new WinFont("Segoe UI", 9f))
                    using (var b = new SolidBrush(TextSecondary))
                        g.DrawString(_subtitle, f, b, new PointF(16, Height - 18));
                }

                // Close button [×]
                if (_showClose)
                {
                    using (var f = new WinFont("Segoe UI", 10f))
                    using (var b = new SolidBrush(TextMuted))
                        g.DrawString("×", f, b, new PointF(Width - 28, 8));
                }

                // Divider bottom
                using (var pen = new Pen(Divider))
                    g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

                // Shimmer
                float shimmerX = _getShimmerX != null ? _getShimmerX() : -200f;
                if (shimmerX >= 0f)
                {
                    var clip = new GraphicsPath();
                    clip.AddRectangle(new RectangleF(0, 0, Width, Height));
                    g.SetClip(clip);

                    float shimmerW = 80f;
                    var shimmerRect = new RectangleF(shimmerX - shimmerW / 2f, 0, shimmerW, Height);
                    var shimmerColors = new Color[]
                    {
                        Color.FromArgb(0,   255, 255, 255),
                        Color.FromArgb(40,  255, 255, 255),
                        Color.FromArgb(0,   255, 255, 255),
                    };
                    var shimmerPositions = new float[] { 0f, 0.5f, 1f };

                    using (var lgb = new LinearGradientBrush(
                        new PointF(shimmerRect.Left, 0),
                        new PointF(shimmerRect.Right, 0),
                        Color.Transparent,
                        Color.Transparent))
                    {
                        var blend = new ColorBlend();
                        blend.Colors    = shimmerColors;
                        blend.Positions = shimmerPositions;
                        lgb.InterpolationColors = blend;
                        g.FillRectangle(lgb, shimmerRect);
                    }

                    g.ResetClip();
                }
            }

            // Close button zone: top-right 36×36
            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (_showClose && e.X >= Width - 36 && e.Y <= 36)
                {
                    _owner.DialogResult = DialogResult.Cancel;
                    _owner.Close();
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress */ }

            private void OnMD(object s, MouseEventArgs e)
            {
                if (_showClose && e.X >= Width - 36 && e.Y <= 36) return;
                _dragging  = true;
                _dragStart = e.Location;
            }
            private void OnMM(object s, MouseEventArgs e)
            {
                if (!_dragging) return;
                var pt = _owner.Location;
                pt.X  += e.X - _dragStart.X;
                pt.Y  += e.Y - _dragStart.Y;
                _owner.Location = pt;
            }
            private void OnMU(object s, MouseEventArgs e) { _dragging = false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BtnFuturista
        // ─────────────────────────────────────────────────────────────────────

        internal enum BtnStyle { Primary, Secondary, Danger }
        internal enum BtnIcon  { None, Pick, Add, Remove, Reset }

        internal class BtnFuturista : Button
        {
            private readonly BtnStyle _style;
            private readonly BtnIcon  _icon;
            private bool  _pressed;
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;

            // Optional color overrides for Primary style
            private Color? _bgOverride;
            private Color? _bgHoverOverride;

            public BtnFuturista(BtnStyle style = BtnStyle.Secondary, BtnIcon icon = BtnIcon.None)
            {
                _style    = style;
                _icon     = icon;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor    = Cursors.Hand;
                Height    = 32;
                Font      = new WinFont("Segoe UI", 9f, FontStyle.Bold);
                ForeColor = TextPrimary;
                BackColor = Color.Transparent;

                MouseEnter  += OnHoverEnter;
                MouseLeave  += OnHoverLeave;
                MouseDown   += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp     += (s, e) => { _pressed = false; Invalidate(); };
            }

            /// <summary>Override the primary BG colors (only affects Primary style).</summary>
            public void SetColorOverride(Color bg, Color bgHover)
            {
                _bgOverride      = bg;
                _bgHoverOverride = bgHover;
                Invalidate();
            }

            private void OnHoverEnter(object sender, EventArgs e)
            {
                EnsureHoverTimer();
                _hoverTimer.Tag = "in";
                _hoverTimer.Start();
            }

            private void OnHoverLeave(object sender, EventArgs e)
            {
                EnsureHoverTimer();
                _hoverTimer.Tag = "out";
                _hoverTimer.Start();
            }

            private void EnsureHoverTimer()
            {
                if (_hoverTimer != null)
                {
                    _hoverTimer.Stop();
                    return;
                }
                _hoverTimer = new Timer { Interval = 16 };
                _hoverTimer.Tick += OnHoverTick;
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string direction = _hoverTimer.Tag as string;
                if (direction == "in")
                {
                    _hoverProgress += 0.15f;
                    if (_hoverProgress >= 1f) { _hoverProgress = 1f; _hoverTimer.Stop(); }
                }
                else
                {
                    _hoverProgress -= 0.15f;
                    if (_hoverProgress <= 0f) { _hoverProgress = 0f; _hoverTimer.Stop(); }
                }
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g  = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc = new Rectangle(0, 0, Width - 1, Height - 1);

                if (!Enabled)
                {
                    using (var b = new SolidBrush(BorderSubtle))
                        g.FillRectangle(b, rc);
                    using (var p = new Pen(BorderSubtle))
                        g.DrawRectangle(p, rc);
                    using (var b = new SolidBrush(TextMuted))
                    using (var f = new WinFont("Segoe UI", 9f))
                        DrawContent(g, f, b);
                    return;
                }

                switch (_style)
                {
                    case BtnStyle.Primary:
                    {
                        Color baseColor  = _bgOverride.HasValue      ? _bgOverride.Value      : CtaFrom;
                        Color hoverColor = _bgHoverOverride.HasValue ? _bgHoverOverride.Value  : CtaTo;

                        int r1 = baseColor.R,  g1 = baseColor.G,  b1 = baseColor.B;
                        int r2 = hoverColor.R, g2 = hoverColor.G, b2 = hoverColor.B;
                        int ri = (int)(r1 + (r2 - r1) * _hoverProgress);
                        int gi = (int)(g1 + (g2 - g1) * _hoverProgress);
                        int bi = (int)(b1 + (b2 - b1) * _hoverProgress);
                        Color interpColor = Color.FromArgb(255, ri, gi, bi);

                        Color fillEnd = _pressed ? BrandNavyHi : interpColor;
                        using (var grad = new LinearGradientBrush(rc, fillEnd, interpColor, LinearGradientMode.Horizontal))
                            g.FillRectangle(grad, rc);
                        using (var pen = new Pen(BorderStrong))
                            g.DrawRectangle(pen, rc);

                        int glowAlpha = (int)(60 * _hoverProgress);
                        if (glowAlpha > 0)
                        {
                            using (var glow = new Pen(Color.FromArgb(glowAlpha, BrandSteelHi), 4f))
                                g.DrawRectangle(glow, new Rectangle(1, 1, Width - 3, Height - 3));
                        }

                        using (var b = new SolidBrush(CtaText))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;
                    }

                    case BtnStyle.Secondary:
                    {
                        int r1 = BgBase.R,       g1 = BgBase.G,       b1 = BgBase.B;
                        int r2 = BgPanelHover.R, g2 = BgPanelHover.G, b2 = BgPanelHover.B;
                        int ri = (int)(r1 + (r2 - r1) * _hoverProgress);
                        int gi = (int)(g1 + (g2 - g1) * _hoverProgress);
                        int bi = (int)(b1 + (b2 - b1) * _hoverProgress);

                        Color sbg = _pressed ? BgPanel : Color.FromArgb(255, ri, gi, bi);
                        using (var brushSbg = new SolidBrush(sbg))
                            g.FillRectangle(brushSbg, rc);

                        int br1 = BorderSubtle.R,  bg1 = BorderSubtle.G,  bb1 = BorderSubtle.B;
                        int br2 = AccentPrimary.R, bg2 = AccentPrimary.G, bb2 = AccentPrimary.B;
                        int bri = (int)(br1 + (br2 - br1) * _hoverProgress);
                        int bgi = (int)(bg1 + (bg2 - bg1) * _hoverProgress);
                        int bbi = (int)(bb1 + (bb2 - bb1) * _hoverProgress);
                        using (var pen = new Pen(Color.FromArgb(255, bri, bgi, bbi)))
                            g.DrawRectangle(pen, rc);

                        int glowAlpha = (int)(60 * _hoverProgress);
                        if (glowAlpha > 0)
                        {
                            using (var glow = new Pen(Color.FromArgb(glowAlpha, BrandSteelHi), 4f))
                                g.DrawRectangle(glow, new Rectangle(1, 1, Width - 3, Height - 3));
                        }

                        using (var b = new SolidBrush(TextPrimary))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;
                    }

                    case BtnStyle.Danger:
                    {
                        int targetA = DangerBg.A, targetR = Danger.R, targetG = Danger.G, targetB2 = Danger.B;
                        int ia = (int)(targetA * _hoverProgress);
                        int ir = (int)(targetR * _hoverProgress);
                        int ig = (int)(targetG * _hoverProgress);
                        int ib = (int)(targetB2 * _hoverProgress);
                        Color dbg = _pressed
                            ? DangerBg
                            : Color.FromArgb(ia, ir, ig, ib);
                        using (var brushDbg = new SolidBrush(dbg))
                            g.FillRectangle(brushDbg, rc);

                        int br1 = BorderSubtle.R, bg1 = BorderSubtle.G, bb1 = BorderSubtle.B;
                        int br2 = Danger.R,       bg2 = Danger.G,       bb2 = Danger.B;
                        int bri = (int)(br1 + (br2 - br1) * _hoverProgress);
                        int bgi = (int)(bg1 + (bg2 - bg1) * _hoverProgress);
                        int bbi = (int)(bb1 + (bb2 - bb1) * _hoverProgress);
                        using (var pen = new Pen(Color.FromArgb(255, bri, bgi, bbi)))
                            g.DrawRectangle(pen, rc);

                        int glowAlpha = (int)(60 * _hoverProgress);
                        if (glowAlpha > 0)
                        {
                            using (var glow = new Pen(Color.FromArgb(glowAlpha, Danger), 4f))
                                g.DrawRectangle(glow, new Rectangle(1, 1, Width - 3, Height - 3));
                        }

                        int tr1 = TextSecondary.R, tg1 = TextSecondary.G, tb1 = TextSecondary.B;
                        int tr2 = Danger.R,        tg2 = Danger.G,        tb2 = Danger.B;
                        int tri = (int)(tr1 + (tr2 - tr1) * _hoverProgress);
                        int tgi = (int)(tg1 + (tg2 - tg1) * _hoverProgress);
                        int tbi = (int)(tb1 + (tb2 - tb1) * _hoverProgress);
                        using (var b = new SolidBrush(Color.FromArgb(255, tri, tgi, tbi)))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;
                    }
                }
            }

            private void DrawContent(Graphics g, WinFont f, SolidBrush textBrush)
            {
                if (_icon == BtnIcon.None)
                {
                    var sf = new StringFormat
                    {
                        Alignment     = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                    };
                    g.DrawString(Text, f, textBrush, new RectangleF(0, 0, Width, Height), sf);
                    return;
                }

                float iconW = 14f;
                float gap   = 6f;
                var sf2     = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                var textSz  = g.MeasureString(Text, f);
                float totalW = iconW + gap + textSz.Width;
                float startX = (Width - totalW) / 2f;
                float cy     = Height / 2f;
                float iconX  = startX;
                float textX  = startX + iconW + gap;

                using (var iconPen = new Pen(textBrush.Color, 1.5f))
                    DrawIcon(g, iconPen, textBrush, iconX, cy - 7f);

                g.DrawString(Text, f, textBrush, new PointF(textX, cy - textSz.Height / 2f));
            }

            private void DrawIcon(Graphics g, Pen pen, SolidBrush br, float x, float y)
            {
                switch (_icon)
                {
                    case BtnIcon.Pick:
                        g.DrawEllipse(pen, x + 1, y + 1, 12, 12);
                        g.FillEllipse(br, x + 5, y + 5, 4, 4);
                        break;
                    case BtnIcon.Add:
                        g.DrawLine(pen, x + 7, y + 1, x + 7, y + 13);
                        g.DrawLine(pen, x + 1, y + 7, x + 13, y + 7);
                        break;
                    case BtnIcon.Remove:
                        g.DrawLine(pen, x + 1, y + 7, x + 13, y + 7);
                        break;
                    case BtnIcon.Reset:
                        g.DrawArc(pen, x + 1, y + 1, 12, 12, 30, 300);
                        var tip = new PointF[]
                        {
                            new PointF(x + 11f, y + 1.5f),
                            new PointF(x + 14f, y + 5f),
                            new PointF(x + 8f,  y + 4f),
                        };
                        g.DrawPolygon(pen, tip);
                        break;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress flicker */ }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _hoverTimer != null)
                {
                    _hoverTimer.Stop();
                    _hoverTimer.Dispose();
                    _hoverTimer = null;
                }
                base.Dispose(disposing);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers de rendering
        // ─────────────────────────────────────────────────────────────────────

        internal static void PaintHairlineStripe(Graphics g, int width)
        {
            var cb = new ColorBlend(3);
            cb.Colors    = new[] { CtaFrom, BrandSteel, Color.Transparent };
            cb.Positions = new[] { 0f, 0.5f, 1f };
            using (var lgb = new LinearGradientBrush(
                new PointF(0, 0), new PointF(width, 0),
                CtaFrom, Color.Transparent))
            {
                lgb.InterpolationColors = cb;
                g.FillRectangle(lgb, 0, 0, width, 2);
            }
        }

        internal static Image LoadEmbeddedImage(string resourceName)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null) return null;
                return new Bitmap(s);
            }
        }
    }
}
