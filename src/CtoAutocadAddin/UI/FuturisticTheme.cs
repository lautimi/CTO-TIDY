using WinFont = System.Drawing.Font;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Koovra.Cto.AutocadAddin.UI
{
    internal static class FuturisticTheme
    {
        // ── Marca ────────────────────────────────────────────────────────────
        internal static readonly Color Navy    = Color.FromArgb(0x1D, 0x35, 0x57);
        internal static readonly Color Steel   = Color.FromArgb(0x46, 0x7B, 0x9D);
        internal static readonly Color Tinta   = Color.FromArgb(0x0F, 0x25, 0x40);

        // ── Superficies ──────────────────────────────────────────────────────
        internal static readonly Color BgBase        = Color.FromArgb(0x08, 0x14, 0x20);
        internal static readonly Color BgPanel       = Color.FromArgb(0x0F, 0x25, 0x40);
        internal static readonly Color BgPanelHover  = Color.FromArgb(0x16, 0x2E, 0x50);
        internal static readonly Color BgElev        = Color.FromArgb(0x13, 0x20, 0x38);

        // ── Bordes ───────────────────────────────────────────────────────────
        internal static readonly Color BorderSubtle = Color.FromArgb(0x1D, 0x35, 0x57);
        internal static readonly Color BorderStrong = Color.FromArgb(0x80, 0x46, 0x7B, 0x9D);
        internal static readonly Color Divider      = Color.FromArgb(0x2E, 0x46, 0x7B, 0x9D);

        // ── Acentos funcionales ──────────────────────────────────────────────
        internal static readonly Color Success = Color.FromArgb(0x10, 0xB9, 0x81);
        internal static readonly Color Warning = Color.FromArgb(0xF5, 0x9E, 0x0B);
        internal static readonly Color Error   = Color.FromArgb(0xEF, 0x44, 0x44);
        internal static readonly Color Info    = Color.FromArgb(0x00, 0xB4, 0xD8);

        // ── Texto ────────────────────────────────────────────────────────────
        internal static readonly Color TextPrimary   = Color.FromArgb(0xE8, 0xF0, 0xF8);
        internal static readonly Color TextSecondary = Color.FromArgb(0x9A, 0xB4, 0xCC);
        internal static readonly Color TextMuted     = Color.FromArgb(0x5A, 0x7A, 0x9A);

        // ── Compat aliases (usados en SettingsDialog / LoadingOverlay) ───────
        internal static readonly Color AccentPrimary   = Info;
        internal static readonly Color AccentSecondary = Steel;
        internal static readonly Color AccentGlow      = Color.FromArgb(0x38, 0x46, 0x7B, 0x9D);
        internal static readonly Color BorderFocus     = Steel;
        internal static readonly Color Danger          = Error;

        // ── Tipografía ───────────────────────────────────────────────────────
        private static readonly bool _openSansAvailable = IsFontInstalled("Open Sans");

        private static bool IsFontInstalled(string name)
        {
            using (var testFont = new WinFont(name, 10f))
                return string.Equals(testFont.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        public static string PrimaryFontFamily => _openSansAvailable ? "Open Sans" : "Arial";

        // ── Logo (lazy-loaded) ───────────────────────────────────────────────
        private static Image _logoWhite;
        private static bool  _logoLoaded;

        internal static Image GetLogoWhite()
        {
            if (_logoLoaded) return _logoWhite;
            _logoLoaded = true;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream(
                    "Koovra.Cto.AutocadAddin.Resources.vezeel-logo-final.png"))
                {
                    if (stream == null) return null;
                    var src = Image.FromStream(stream);
                    _logoWhite = ApplyWhiteMatrix(src);
                }
            }
            catch { _logoWhite = null; }
            return _logoWhite;
        }

        internal static Image ApplyWhiteMatrix(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                var matrix = new ColorMatrix(new float[][]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 1, 1, 1, 0, 1 },
                });
                var ia = new ImageAttributes();
                ia.SetColorMatrix(matrix);
                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return bmp;
        }

        // ── Paint helpers ────────────────────────────────────────────────────

        internal static GraphicsPath MakeChevronPath(int w, int h, int notch)
        {
            var p = new GraphicsPath();
            p.AddPolygon(new PointF[]
            {
                new PointF(notch,         0),
                new PointF(w,             0),
                new PointF(w - notch,     h / 2f),
                new PointF(w,             h),
                new PointF(notch,         h),
                new PointF(0,             h / 2f),
            });
            return p;
        }

        internal static GraphicsPath MakeCornerCutPath(int w, int h, int notch)
        {
            var p = new GraphicsPath();
            p.AddPolygon(new PointF[]
            {
                new PointF(0,          0),
                new PointF(w - notch,  0),
                new PointF(w,          notch),
                new PointF(w,          h),
                new PointF(notch,      h),
                new PointF(0,          h - notch),
            });
            return p;
        }

        internal static void DrawSideBar(Graphics g, int h, Color color, float alpha)
        {
            int a = (int)(255 * alpha);
            if (a <= 0) return;
            using (var b = new SolidBrush(Color.FromArgb(Math.Min(a, 255), color)))
                g.FillRectangle(b, 0, 0, 3, h);
        }

        internal static void DrawShimmer(Graphics g, RectangleF bounds, float shimmerX, float shimmerW = 80f)
        {
            if (shimmerX < 0f) return;
            var shimmerRect = new RectangleF(shimmerX - shimmerW / 2f, bounds.Y, shimmerW, bounds.Height);
            using (var lgb = new LinearGradientBrush(
                new PointF(shimmerRect.Left, 0),
                new PointF(shimmerRect.Right, 0),
                Color.Transparent,
                Color.Transparent))
            {
                var blend = new ColorBlend();
                blend.Colors    = new Color[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(51, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                lgb.InterpolationColors = blend;
                g.FillRectangle(lgb, shimmerRect);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // HeaderPanel
        // ─────────────────────────────────────────────────────────────────────

        internal class HeaderPanel : Panel
        {
            private readonly Form         _owner;
            private readonly Func<double> _getGlowPhase;
            private readonly Func<float>  _getShimmerX;
            private readonly string       _subtitle;
            private readonly string       _tag;
            private readonly bool         _showClose;
            private readonly int          _logoHeight;
            private Point _dragStart;
            private bool  _dragging;

            public HeaderPanel(
                Form         owner,
                Func<double> getGlowPhase,
                Func<float>  getShimmerX,
                string       title     = "CTO",
                string       subtitle  = null,
                string       tag       = null,
                bool         showClose = true,
                int          logoHeight = 26)
            {
                _owner        = owner;
                _getGlowPhase = getGlowPhase;
                _getShimmerX  = getShimmerX;
                _subtitle     = subtitle   ?? string.Empty;
                _tag          = tag        ?? string.Empty;
                _showClose    = showClose;
                _logoHeight   = logoHeight;

                BackColor      = BgBase;
                DoubleBuffered = true;
                MouseDown     += OnMD;
                MouseMove     += OnMM;
                MouseUp       += OnMU;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width < 4 || Height < 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (var brush = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(0x14, 0x2A, 0x44),
                    Tinta,
                    LinearGradientMode.Vertical))
                    g.FillRectangle(brush, ClientRectangle);

                int logoH = _logoHeight;
                int logoY = (Height - logoH - 18) / 2;
                if (logoY < 8) logoY = 8;

                var logo = GetLogoWhite();
                if (logo != null)
                {
                    int logoW = (int)((double)logo.Width / logo.Height * logoH);
                    var logoRect = new Rectangle(14, logoY, logoW, logoH);
                    using (var ia = new ImageAttributes())
                    {
                        ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.92f });
                        g.DrawImage(logo, logoRect, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, ia);
                    }
                }
                else
                {
                    using (var f = new WinFont(PrimaryFontFamily, 11f, FontStyle.Bold))
                    using (var b = new SolidBrush(TextPrimary))
                        g.DrawString("VEZEEL GROUP", f, b, new PointF(14, logoY));
                }

                if (!string.IsNullOrEmpty(_tag))
                {
                    using (var f = new WinFont("Courier New", 9f))
                    {
                        var sz = g.MeasureString(_tag, f);
                        float tagRight = _showClose ? Width - 40f : Width - 10f;
                        float tagX = tagRight - sz.Width - 8f;
                        float tagY = 14f;
                        var tagRect = new RectangleF(tagX - 4, tagY - 2, sz.Width + 14, sz.Height + 4);
                        using (var pen = new Pen(Color.FromArgb(0x47, 0x46, 0x7B, 0x9D)))
                            g.DrawRectangle(pen, tagRect.X, tagRect.Y, tagRect.Width, tagRect.Height);
                        using (var b = new SolidBrush(TextMuted))
                            g.DrawString(_tag, f, b, new PointF(tagX + 3, tagY + 1));
                    }
                }

                if (_showClose)
                {
                    using (var f = new WinFont(PrimaryFontFamily, 11f))
                    using (var b = new SolidBrush(TextMuted))
                        g.DrawString("×", f, b, new PointF(Width - 28, 8));
                }

                float shimmerX = _getShimmerX != null ? _getShimmerX() : -200f;
                DrawShimmer(g, new RectangleF(0, 0, Width, Height), shimmerX);

                } catch { /* GDI+ transient; repaint will retry */ }
            }

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
        // ChevronButton — pasos 1-5
        // ─────────────────────────────────────────────────────────────────────

        internal class ChevronButton : Control
        {
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;
            private bool  _pressed;
            private GraphicsPath _regionPath;

            public ChevronButton()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                Cursor    = Cursors.Hand;
                ForeColor = TextPrimary;
                BackColor = BgPanel;
                Font      = new WinFont(PrimaryFontFamily, 8.5f, FontStyle.Bold);

                MouseEnter += (s, e) => StartHover(true);
                MouseLeave += (s, e) => StartHover(false);
                MouseDown  += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
            }

            private void StartHover(bool inDir)
            {
                if (_hoverTimer == null)
                {
                    _hoverTimer = new Timer { Interval = 16 };
                    _hoverTimer.Tick += OnHoverTick;
                }
                _hoverTimer.Stop();
                _hoverTimer.Tag = inDir ? "in" : "out";
                _hoverTimer.Start();
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string dir = _hoverTimer.Tag as string;
                if (dir == "in")
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

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width > 4 && Height > 4)
                {
                    var newPath = MakeChevronPath(Width - 1, Height - 1, 8);
                    this.Region?.Dispose();
                    this.Region = new Region(newPath);
                    _regionPath?.Dispose();
                    _regionPath = newPath;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width < 1 || Height < 1) return;
                try
                {
                    using (var b = new SolidBrush(BackColor))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 4 || Height <= 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = Width - 1;
                int h = Height - 1;

                Color gradStart = _pressed ? Color.FromArgb(0x14, 0x2A, 0x44) : Navy;
                Color gradEnd   = _pressed ? Navy : Steel;

                // Brightness increase on hover
                if (_hoverProgress > 0f && !_pressed)
                {
                    int addR = (int)(20 * _hoverProgress);
                    int addG = (int)(20 * _hoverProgress);
                    int addB = (int)(20 * _hoverProgress);
                    gradEnd = Color.FromArgb(
                        Math.Min(Steel.R + addR, 255),
                        Math.Min(Steel.G + addG, 255),
                        Math.Min(Steel.B + addB, 255));
                }

                using (var grad = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h), gradStart, gradEnd,
                    LinearGradientMode.Horizontal))
                    g.FillRectangle(grad, 0, 0, w + 1, h + 1);

                // Top highlight bevel
                using (var lgb = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h / 2),
                    Color.FromArgb(31, 255, 255, 255),
                    Color.Transparent,
                    LinearGradientMode.Vertical))
                    g.FillRectangle(lgb, 0, 0, w + 1, h / 2);

                // Text centered
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                using (var b = new SolidBrush(_pressed ? Color.FromArgb(0xCC, 0xE0, 0xF0) : TextPrimary))
                    g.DrawString(Text, Font, b, new RectangleF(8, 0, w - 16, h + 1), sf);

                } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hoverTimer != null)
                    {
                        _hoverTimer.Stop();
                        _hoverTimer.Dispose();
                        _hoverTimer = null;
                    }
                    _regionPath?.Dispose();
                    this.Region?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SecondaryButton — Inspeccionar / Configuración
        // ─────────────────────────────────────────────────────────────────────

        internal class SecondaryButton : Control
        {
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;
            private bool  _pressed;
            private GraphicsPath _regionPath;

            public SecondaryButton()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                Cursor    = Cursors.Hand;
                ForeColor = TextPrimary;
                BackColor = BgPanel;
                Font      = new WinFont(PrimaryFontFamily, 9f, FontStyle.Bold);

                MouseEnter += (s, e) => StartHover(true);
                MouseLeave += (s, e) => StartHover(false);
                MouseDown  += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
            }

            private void StartHover(bool inDir)
            {
                if (_hoverTimer == null)
                {
                    _hoverTimer = new Timer { Interval = 16 };
                    _hoverTimer.Tick += OnHoverTick;
                }
                _hoverTimer.Stop();
                _hoverTimer.Tag = inDir ? "in" : "out";
                _hoverTimer.Start();
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string dir = _hoverTimer.Tag as string;
                if (dir == "in")
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

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width > 4 && Height > 4)
                {
                    var newPath = MakeCornerCutPath(Width - 1, Height - 1, 10);
                    this.Region?.Dispose();
                    this.Region = new Region(newPath);
                    _regionPath?.Dispose();
                    _regionPath = newPath;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width < 1 || Height < 1) return;
                try
                {
                    using (var b = new SolidBrush(BackColor))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 4 || Height <= 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = Width - 1;
                int h = Height - 1;

                float bgAlpha = 0.25f + 0.25f * _hoverProgress;
                using (var b = new SolidBrush(Color.FromArgb((int)(255 * bgAlpha), Navy)))
                    g.FillRectangle(b, 0, 0, w + 1, h + 1);

                float borderAlpha = 0.35f + 0.35f * _hoverProgress;
                using (var path = MakeCornerCutPath(w, h, 10))
                using (var pen = new Pen(Color.FromArgb((int)(255 * borderAlpha), Steel)))
                    g.DrawPath(pen, path);

                // Sidebar 3px
                float sideAlpha = 0.45f + 0.55f * _hoverProgress;
                DrawSideBar(g, h + 1, Steel, sideAlpha);

                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                using (var b = new SolidBrush(TextPrimary))
                    g.DrawString(Text, Font, b, new RectangleF(6, 0, w - 6, h + 1), sf);

                } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hoverTimer != null)
                    {
                        _hoverTimer.Stop();
                        _hoverTimer.Dispose();
                        _hoverTimer = null;
                    }
                    _regionPath?.Dispose();
                    this.Region?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // RunAllButton — EJECUTAR TODO
        // ─────────────────────────────────────────────────────────────────────

        internal class RunAllButton : Control
        {
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;
            private Timer _shimmerTimer;
            private float _shimmerX = -200f;
            private bool  _pressed;
            private GraphicsPath _regionPath;

            public RunAllButton()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                Cursor    = Cursors.Hand;
                ForeColor = TextPrimary;
                BackColor = BgBase;
                Font      = new WinFont(PrimaryFontFamily, 10f, FontStyle.Bold);

                MouseEnter += (s, e) => { StartHover(true);  StartShimmer(); };
                MouseLeave += (s, e) => StartHover(false);
                MouseDown  += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
            }

            private void StartHover(bool inDir)
            {
                if (_hoverTimer == null)
                {
                    _hoverTimer = new Timer { Interval = 16 };
                    _hoverTimer.Tick += OnHoverTick;
                }
                _hoverTimer.Stop();
                _hoverTimer.Tag = inDir ? "in" : "out";
                _hoverTimer.Start();
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string dir = _hoverTimer.Tag as string;
                if (dir == "in")
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

            private void StartShimmer()
            {
                if (_shimmerTimer != null) { _shimmerTimer.Stop(); _shimmerTimer.Dispose(); }
                _shimmerX = -80f;
                _shimmerTimer = new Timer { Interval = 16 };
                _shimmerTimer.Tick += (s, e) =>
                {
                    _shimmerX += (Width + 160f) / (600f / 16f);
                    if (_shimmerX > Width + 80f)
                    {
                        _shimmerTimer.Stop();
                        _shimmerTimer.Dispose();
                        _shimmerTimer = null;
                        _shimmerX = -200f;
                    }
                    Invalidate();
                };
                _shimmerTimer.Start();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width > 4 && Height > 4)
                {
                    var newPath = MakeChevronPath(Width - 1, Height - 1, 16);
                    this.Region?.Dispose();
                    this.Region = new Region(newPath);
                    _regionPath?.Dispose();
                    _regionPath = newPath;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width < 1 || Height < 1) return;
                try
                {
                    using (var b = new SolidBrush(BackColor))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 4 || Height <= 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = Width - 1;
                int h = Height - 1;

                // Gradient horizontal: #0D2240 → navy → steel
                using (var grad = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h), Color.Transparent, Color.Transparent,
                    LinearGradientMode.Horizontal))
                {
                    var cb = new ColorBlend();
                    cb.Colors    = new Color[] { Color.FromArgb(0x0D, 0x22, 0x40), Navy, Steel };
                    cb.Positions = new float[] { 0f, 0.35f, 1f };
                    grad.InterpolationColors = cb;
                    g.FillRectangle(grad, 0, 0, w + 1, h + 1);
                }

                // Top highlight bevel
                using (var lgb = new LinearGradientBrush(
                    new Rectangle(0, 0, w, h / 2),
                    Color.FromArgb(_pressed ? 18 : 36, 255, 255, 255),
                    Color.Transparent,
                    LinearGradientMode.Vertical))
                    g.FillRectangle(lgb, 0, 0, w + 1, h / 2);

                // Shimmer
                if (_shimmerX > -100f)
                    DrawShimmer(g, new RectangleF(0, 0, w, h), _shimmerX, 80f);

                // Hover brightness overlay
                if (_hoverProgress > 0f)
                {
                    using (var b = new SolidBrush(Color.FromArgb((int)(25 * _hoverProgress), 255, 255, 255)))
                        g.FillRectangle(b, 0, 0, w + 1, h + 1);
                }

                // V-mark left — dos trazos con opacidades 45%/100%
                int cx = 28;
                int cy = h / 2;
                using (var penFaint = new Pen(Color.FromArgb(115, 255, 255, 255), 2f))
                    g.DrawLine(penFaint, cx - 5, cy - 3, cx, cy + 3);
                using (var penSolid = new Pen(Color.FromArgb(255, 255, 255, 255), 2f))
                    g.DrawLine(penSolid, cx, cy + 3, cx + 8, cy - 5);

                // Main text "EJECUTAR TODO" centered
                using (var mainFont = new WinFont(PrimaryFontFamily, 10f, FontStyle.Bold))
                using (var b = new SolidBrush(_pressed ? Color.FromArgb(0xCC, 0xE0, 0xF0) : TextPrimary))
                {
                    var sf = new StringFormat
                    {
                        Alignment     = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near,
                    };
                    g.DrawString("EJECUTAR TODO", mainFont, b,
                        new RectangleF(16, 6, w - 32, h / 2f + 2), sf);
                }

                // Sub text "PASOS 1 → 5" steel-muted right area
                using (var subFont = new WinFont(PrimaryFontFamily, 8.5f))
                using (var b = new SolidBrush(Color.FromArgb(128, 255, 255, 255)))
                {
                    var sf = new StringFormat
                    {
                        Alignment     = StringAlignment.Center,
                        LineAlignment = StringAlignment.Far,
                    };
                    g.DrawString("PASOS 1 → 5", subFont, b,
                        new RectangleF(16, h / 2f - 2, w - 32, h / 2f + 2), sf);
                }

                } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hoverTimer != null)
                    {
                        _hoverTimer.Stop();
                        _hoverTimer.Dispose();
                        _hoverTimer = null;
                    }
                    if (_shimmerTimer != null)
                    {
                        _shimmerTimer.Stop();
                        _shimmerTimer.Dispose();
                        _shimmerTimer = null;
                    }
                    _regionPath?.Dispose();
                    this.Region?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DialogButton — OK/Cancel/Defaults/Danger del settings dialog
        // ─────────────────────────────────────────────────────────────────────

        internal enum DialogBtnStyle { Primary, Secondary, Danger }

        internal class DialogButton : Control, IButtonControl
        {
            public DialogResult DialogResult { get; set; }
            public void NotifyDefault(bool value) { }
            public void PerformClick() { OnClick(EventArgs.Empty); }

            private readonly DialogBtnStyle _style;
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;
            private bool  _pressed;
            private GraphicsPath _regionPath;

            public DialogButton(DialogBtnStyle style = DialogBtnStyle.Primary)
            {
                _style = style;
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                Cursor    = Cursors.Hand;
                ForeColor = TextPrimary;
                BackColor = BgPanel;
                Font      = new WinFont(PrimaryFontFamily, 9f, FontStyle.Bold);

                MouseEnter += (s, e) => StartHover(true);
                MouseLeave += (s, e) => StartHover(false);
                MouseDown  += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
                EnabledChanged += (s, e) => { this.Cursor = Enabled ? Cursors.Hand : Cursors.Default; };
            }

            private void StartHover(bool inDir)
            {
                if (_hoverTimer == null)
                {
                    _hoverTimer = new Timer { Interval = 16 };
                    _hoverTimer.Tick += OnHoverTick;
                }
                _hoverTimer.Stop();
                _hoverTimer.Tag = inDir ? "in" : "out";
                _hoverTimer.Start();
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string dir = _hoverTimer.Tag as string;
                if (dir == "in")
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

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width > 4 && Height > 4)
                {
                    var newPath = MakeCornerCutPath(Width - 1, Height - 1, 7);
                    this.Region?.Dispose();
                    this.Region = new Region(newPath);
                    _regionPath?.Dispose();
                    _regionPath = newPath;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width < 1 || Height < 1) return;
                try
                {
                    using (var b = new SolidBrush(BackColor))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 4 || Height <= 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = Width - 1;
                int h = Height - 1;

                if (!Enabled)
                {
                    using (var b = new SolidBrush(Color.FromArgb(45, Navy)))
                        g.FillRectangle(b, 0, 0, w + 1, h + 1);
                    using (var path = MakeCornerCutPath(w, h, 7))
                    using (var pen = new Pen(Color.FromArgb(45, Steel)))
                        g.DrawPath(pen, path);
                    using (var b = new SolidBrush(TextMuted))
                        g.DrawString(Text, Font, b,
                            new RectangleF(0, 0, w + 1, h + 1),
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                switch (_style)
                {
                    case DialogBtnStyle.Primary:
                    {
                        using (var grad = new LinearGradientBrush(
                            new Rectangle(0, 0, w, h), Navy, Steel,
                            LinearGradientMode.Horizontal))
                            g.FillRectangle(grad, 0, 0, w + 1, h + 1);

                        if (_hoverProgress > 0f)
                        {
                            using (var b = new SolidBrush(Color.FromArgb((int)(25 * _hoverProgress), 255, 255, 255)))
                                g.FillRectangle(b, 0, 0, w + 1, h + 1);
                        }

                        using (var b = new SolidBrush(_pressed ? Color.FromArgb(0xCC, 0xE0, 0xF0) : TextPrimary))
                            g.DrawString(Text, Font, b,
                                new RectangleF(0, 0, w + 1, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                    case DialogBtnStyle.Secondary:
                    {
                        float bgAlpha = 0.25f + 0.25f * _hoverProgress;
                        using (var b = new SolidBrush(Color.FromArgb((int)(255 * bgAlpha), Navy)))
                            g.FillRectangle(b, 0, 0, w + 1, h + 1);

                        float borderAlpha = 0.40f + 0.35f * _hoverProgress;
                        using (var path = MakeCornerCutPath(w, h, 7))
                        using (var pen = new Pen(Color.FromArgb((int)(255 * borderAlpha), Steel)))
                            g.DrawPath(pen, path);

                        using (var b = new SolidBrush(TextPrimary))
                            g.DrawString(Text, Font, b,
                                new RectangleF(0, 0, w + 1, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                    case DialogBtnStyle.Danger:
                    {
                        float bgAlpha = 0.13f * _hoverProgress;
                        if (_pressed) bgAlpha = 0.20f;
                        using (var b = new SolidBrush(Color.FromArgb((int)(255 * bgAlpha), Error)))
                            g.FillRectangle(b, 0, 0, w + 1, h + 1);

                        float borderAlpha = _hoverProgress > 0.5f ? 0.8f : 0.30f + 0.45f * _hoverProgress;
                        Color borderColor = _hoverProgress > 0f ? Error : BorderSubtle;
                        using (var path = MakeCornerCutPath(w, h, 7))
                        using (var pen = new Pen(Color.FromArgb((int)(255 * borderAlpha), borderColor)))
                            g.DrawPath(pen, path);

                        float tA = 0.6f + 0.4f * _hoverProgress;
                        Color textColor = _hoverProgress > 0f
                            ? Color.FromArgb((int)(255 * tA), Error)
                            : TextSecondary;
                        using (var b = new SolidBrush(textColor))
                            g.DrawString(Text, Font, b,
                                new RectangleF(0, 0, w + 1, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                }

                } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hoverTimer != null)
                    {
                        _hoverTimer.Stop();
                        _hoverTimer.Dispose();
                        _hoverTimer = null;
                    }
                    _regionPath?.Dispose();
                    this.Region?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BtnFuturista — kept as thin wrapper over DialogButton for compat
        // (SettingsDialog fields declared as BtnFuturista are being replaced,
        //  but this alias stays to avoid breaking any other callsites)
        // ─────────────────────────────────────────────────────────────────────

        internal enum BtnShape  { Chevron, CornerCut, ChevronWide, DialogCut, Rect }
        internal enum BtnStyle  { Primary, Secondary, Danger }
        internal enum BtnIcon   { None, Pick, Add, Remove, Reset }

        internal class BtnFuturista : Control
        {
            private readonly BtnStyle _style;
            private readonly BtnShape _shape;
            private float _hoverProgress = 0f;
            private Timer _hoverTimer;
            private bool  _pressed;
            private GraphicsPath _regionPath;

            public BtnFuturista(BtnStyle style = BtnStyle.Secondary, BtnIcon icon = BtnIcon.None,
                                BtnShape shape = BtnShape.Rect)
            {
                _style = style;
                _shape = shape;
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                Cursor    = Cursors.Hand;
                ForeColor = TextPrimary;
                BackColor = BgPanel;
                Font      = new WinFont(PrimaryFontFamily, 9f, FontStyle.Bold);

                MouseEnter += (s, e) => StartHover(true);
                MouseLeave += (s, e) => StartHover(false);
                MouseDown  += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp    += (s, e) => { _pressed = false; Invalidate(); };
            }

            public void SetColorOverride(Color bg, Color bgHover) { Invalidate(); }

            private void StartHover(bool inDir)
            {
                if (_hoverTimer == null)
                {
                    _hoverTimer = new Timer { Interval = 16 };
                    _hoverTimer.Tick += OnHoverTick;
                }
                _hoverTimer.Stop();
                _hoverTimer.Tag = inDir ? "in" : "out";
                _hoverTimer.Start();
            }

            private void OnHoverTick(object sender, EventArgs e)
            {
                string dir = _hoverTimer.Tag as string;
                if (dir == "in")
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

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (Width <= 4 || Height <= 4) return;
                int notch = (_shape == BtnShape.Chevron) ? 8
                          : (_shape == BtnShape.ChevronWide) ? 16
                          : (_shape == BtnShape.CornerCut) ? 10
                          : (_shape == BtnShape.DialogCut) ? 7
                          : 0;
                if (notch == 0) return;
                GraphicsPath newPath = (_shape == BtnShape.Chevron || _shape == BtnShape.ChevronWide)
                    ? MakeChevronPath(Width - 1, Height - 1, notch)
                    : MakeCornerCutPath(Width - 1, Height - 1, notch);
                this.Region?.Dispose();
                this.Region = new Region(newPath);
                _regionPath?.Dispose();
                _regionPath = newPath;
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width < 1 || Height < 1) return;
                try
                {
                    using (var b = new SolidBrush(BackColor))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 4 || Height <= 4) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Width - 1;
                int h = Height - 1;

                if (!Enabled)
                {
                    using (var b = new SolidBrush(Color.FromArgb(60, Navy)))
                        g.FillRectangle(b, 0, 0, w + 1, h + 1);
                    using (var b = new SolidBrush(TextMuted))
                        g.DrawString(Text, Font, b,
                            new RectangleF(0, 0, w + 1, h + 1),
                            new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    return;
                }

                switch (_style)
                {
                    case BtnStyle.Primary:
                    {
                        if (_shape == BtnShape.ChevronWide)
                        {
                            using (var grad = new LinearGradientBrush(
                                new Rectangle(0, 0, w, h), Color.Transparent, Color.Transparent,
                                LinearGradientMode.Horizontal))
                            {
                                var cb = new ColorBlend();
                                cb.Colors    = new Color[] { Color.FromArgb(0x0D, 0x22, 0x40), Navy, Steel };
                                cb.Positions = new float[] { 0f, 0.35f, 1f };
                                grad.InterpolationColors = cb;
                                g.FillRectangle(grad, 0, 0, w + 1, h + 1);
                            }
                        }
                        else
                        {
                            Color gs = _pressed ? Color.FromArgb(0x14, 0x2A, 0x44) : Navy;
                            Color ge = _pressed ? Navy : Steel;
                            using (var grad = new LinearGradientBrush(
                                new Rectangle(0, 0, w, h), gs, ge,
                                LinearGradientMode.Horizontal))
                                g.FillRectangle(grad, 0, 0, w + 1, h + 1);
                        }
                        using (var lgb = new LinearGradientBrush(
                            new Rectangle(0, 0, w, h / 2),
                            Color.FromArgb(31, 255, 255, 255),
                            Color.Transparent,
                            LinearGradientMode.Vertical))
                            g.FillRectangle(lgb, 0, 0, w + 1, h / 2);
                        using (var b = new SolidBrush(_pressed ? Color.FromArgb(0xCC, 0xE0, 0xF0) : TextPrimary))
                            g.DrawString(Text, Font, b,
                                new RectangleF(8, 0, w - 16, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                    case BtnStyle.Secondary:
                    {
                        float bgAlpha = 0.25f + 0.25f * _hoverProgress;
                        using (var b = new SolidBrush(Color.FromArgb((int)(255 * bgAlpha), Navy)))
                            g.FillRectangle(b, 0, 0, w + 1, h + 1);
                        float borderAlpha = 0.35f + 0.35f * _hoverProgress;
                        int notch = (_shape == BtnShape.DialogCut) ? 7 : 10;
                        using (var path = MakeCornerCutPath(w, h, notch))
                        using (var pen = new Pen(Color.FromArgb((int)(255 * borderAlpha), Steel)))
                            g.DrawPath(pen, path);
                        DrawSideBar(g, h + 1, Steel, 0.45f + 0.55f * _hoverProgress);
                        using (var b = new SolidBrush(TextPrimary))
                            g.DrawString(Text, Font, b,
                                new RectangleF(6, 0, w - 6, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                    case BtnStyle.Danger:
                    {
                        float bgA = 0.13f * _hoverProgress;
                        if (_pressed) bgA = 0.20f;
                        using (var b = new SolidBrush(Color.FromArgb((int)(255 * bgA), Error)))
                            g.FillRectangle(b, 0, 0, w + 1, h + 1);
                        float borderAlpha = _hoverProgress > 0.5f ? 0.8f : 0.35f + 0.45f * _hoverProgress;
                        Color borderColor = _hoverProgress > 0f ? Error : BorderSubtle;
                        using (var pen = new Pen(Color.FromArgb((int)(255 * borderAlpha), borderColor)))
                            g.DrawRectangle(pen, 0, 0, w, h);
                        float tA = 0.6f + 0.4f * _hoverProgress;
                        Color textColor = _hoverProgress > 0f
                            ? Color.FromArgb((int)(255 * tA), Error)
                            : TextSecondary;
                        using (var b = new SolidBrush(textColor))
                            g.DrawString(Text, Font, b,
                                new RectangleF(0, 0, w + 1, h + 1),
                                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        break;
                    }
                }
                } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_hoverTimer != null)
                    {
                        _hoverTimer.Stop();
                        _hoverTimer.Dispose();
                        _hoverTimer = null;
                    }
                    _regionPath?.Dispose();
                    this.Region?.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
