using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinFont = System.Drawing.Font;

namespace Koovra.Cto.AutocadAddin.UI
{
    /// <summary>
    /// Overlay de inicialización para CTO_PANEL.
    /// Cubre el form mientras se precomputan datos pesados (StreetCornerLibrary, OD reads).
    /// Estilo: gradient dark + título "CTO" + 3 dots animados + status text.
    /// </summary>
    internal class LoadingOverlay : Panel
    {
        private string _status = "Detectando entidades del DWG...";
        private readonly Timer _timer;
        private double _phase;

        public string Status
        {
            get => _status;
            set { _status = value ?? string.Empty; Invalidate(); }
        }

        public LoadingOverlay()
        {
            DoubleBuffered = true;
            BackColor = FuturisticTheme.BgBase;
            Dock = DockStyle.Fill;

            _timer = new Timer { Interval = 30 };
            _timer.Tick += (s, e) =>
            {
                _phase += 0.06;
                if (_phase > Math.PI * 2) _phase -= Math.PI * 2;
                Invalidate();
            };
            _timer.Start();
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { /* suppress */ }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background gradient
            using (var brush = new LinearGradientBrush(
                ClientRectangle, FuturisticTheme.BgBase, FuturisticTheme.BgPanel,
                LinearGradientMode.Vertical))
                g.FillRectangle(brush, ClientRectangle);

            // Center coordinates
            float cx = Width / 2f;
            float cy = Height / 2f;

            // Top divider line
            float dividerWidth = Math.Min(280f, Width * 0.7f);
            using (var pen = new Pen(Color.FromArgb(0x33, 0x00, 0xBF, 0xFF), 1f))
                g.DrawLine(pen, cx - dividerWidth / 2f, cy - 90f, cx + dividerWidth / 2f, cy - 90f);

            // Title "CTO"
            using (var f = new WinFont("Segoe UI", 28f, FontStyle.Bold))
            using (var b = new SolidBrush(FuturisticTheme.TextPrimary))
            {
                var sz = g.MeasureString("CTO", f);
                g.DrawString("CTO", f, b, new PointF(cx - sz.Width / 2f, cy - 70f));
            }

            // Subtitle
            using (var f = new WinFont("Segoe UI", 10f))
            using (var b = new SolidBrush(FuturisticTheme.TextSecondary))
            {
                const string subtitle = "Inicializando análisis topológico";
                var sz = g.MeasureString(subtitle, f);
                g.DrawString(subtitle, f, b, new PointF(cx - sz.Width / 2f, cy - 14f));
            }

            // Three pulsing dots
            float dotSpacing = 22f;
            float dotsCenterY = cy + 30f;
            for (int i = 0; i < 3; i++)
            {
                double phaseOffset = i * (Math.PI * 2.0 / 3.0);
                double s = (Math.Sin(_phase - phaseOffset) + 1) / 2; // 0..1
                float radius = 3f + (float)(s * 3f);                  // 3..6
                int alpha = 80 + (int)(s * 175);                       // 80..255

                float dotX = cx - dotSpacing + i * dotSpacing;
                using (var b = new SolidBrush(Color.FromArgb(alpha, FuturisticTheme.AccentPrimary)))
                    g.FillEllipse(b, dotX - radius, dotsCenterY - radius, radius * 2f, radius * 2f);
            }

            // Status text
            using (var f = new WinFont("Segoe UI", 9f))
            using (var b = new SolidBrush(FuturisticTheme.TextMuted))
            {
                var sz = g.MeasureString(_status, f);
                g.DrawString(_status, f, b, new PointF(cx - sz.Width / 2f, cy + 60f));
            }

            // Bottom divider line
            using (var pen = new Pen(Color.FromArgb(0x33, 0x00, 0xBF, 0xFF), 1f))
                g.DrawLine(pen, cx - dividerWidth / 2f, cy + 90f, cx + dividerWidth / 2f, cy + 90f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Stop();
            base.Dispose(disposing);
        }
    }
}
