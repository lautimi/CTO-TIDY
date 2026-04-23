using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFont = System.Drawing.Font;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Koovra.Cto.AutocadAddin.Models;

namespace Koovra.Cto.AutocadAddin.UI
{
    public class SettingsDialog : Form
    {
        // ── Design tokens ────────────────────────────────────────────────────
        private static readonly Color BgBase        = Color.FromArgb(0x0B, 0x12, 0x20);
        private static readonly Color BgPanel       = Color.FromArgb(0x12, 0x1A, 0x2B);
        private static readonly Color BgPanelHover  = Color.FromArgb(0x1A, 0x25, 0x40);
        private static readonly Color BorderSubtle  = Color.FromArgb(0x1E, 0x2A, 0x44);
        private static readonly Color BorderFocus   = Color.FromArgb(0x00, 0xBF, 0xFF);
        private static readonly Color AccentPrimary = Color.FromArgb(0x00, 0xBF, 0xFF);
        private static readonly Color AccentSecondary = Color.FromArgb(0x1E, 0x90, 0xFF);
        private static readonly Color AccentGlow    = Color.FromArgb(0x33, 0x00, 0xBF, 0xFF);
        private static readonly Color TextPrimary   = Color.FromArgb(0xE6, 0xF1, 0xFF);
        private static readonly Color TextSecondary = Color.FromArgb(0x8F, 0xA3, 0xBF);
        private static readonly Color TextMuted     = Color.FromArgb(0x5A, 0x6B, 0x85);
        private static readonly Color Danger        = Color.FromArgb(0xFF, 0x55, 0x77);

        // ── Controls ─────────────────────────────────────────────────────────
        private CboFuturista _cboLayer;
        private CboFuturista _cboNewCode;
        private ListBoxOwner _lstCodes;
        private BtnFuturista _btnPick;
        private BtnFuturista _btnAddCode;
        private BtnFuturista _btnRemoveCode;
        private BtnFuturista _btnDefaults;
        private BtnFuturista _btnOk;
        private BtnFuturista _btnCancel;

        // drag header
        private Point _dragStart;
        private bool  _dragging;

        // ── Constructor ───────────────────────────────────────────────────────
        public SettingsDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(520, 480);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = BgBase;
            ForeColor       = TextPrimary;
            Font            = new WinFont("Segoe UI", 9f);
            DoubleBuffered  = true;

            // Rounded corners 4px
            var path = new GraphicsPath();
            path.AddArc(0, 0, 8, 8, 180, 90);
            path.AddArc(Width - 8, 0, 8, 8, 270, 90);
            path.AddArc(Width - 8, Height - 8, 8, 8, 0, 90);
            path.AddArc(0, Height - 8, 8, 8, 90, 90);
            path.CloseFigure();
            Region = new Region(path);

            BuildUI();
            PopulateLayerCombo();
            LoadFromSettings();

            KeyPreview = true;
            KeyDown += OnKeyDown;
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            // Header panel (72px)
            var header = new HeaderPanel(this) { Size = new Size(520, 72), Location = new Point(0, 0) };
            Controls.Add(header);

            // ── LAYER DE POSTES section ──────────────────────────────────────
            int y = 80;
            Controls.Add(MakeSectionLabel("LAYER DE POSTES", 20, y));
            y += 28;

            _cboLayer = new CboFuturista { DropDownStyle = ComboBoxStyle.DropDownList };
            _cboLayer.Size     = new Size(380, 30);
            _cboLayer.Location = new Point(20, y);
            Controls.Add(_cboLayer);

            _btnPick = new BtnFuturista(BtnStyle.Primary, BtnIcon.Pick) { Text = "Pick" };
            _btnPick.Size     = new Size(90, 30);
            _btnPick.Location = new Point(408, y);
            _btnPick.Click   += OnPickClick;
            Controls.Add(_btnPick);
            y += 42;

            // ── CÓDIGOS DE OBSERVACIÓN section ──────────────────────────────
            Controls.Add(MakeSectionLabel("CÓDIGOS DE OBSERVACIÓN", 20, y));
            y += 24;

            var subLabel = new Label
            {
                Text      = "Códigos que penalizan ranking de postes PRIORIDAD",
                Location  = new Point(20, y),
                AutoSize  = true,
                ForeColor = TextSecondary,
                Font      = new WinFont("Segoe UI", 9f),
            };
            Controls.Add(subLabel);
            y += 20;

            _cboNewCode = new CboFuturista { DropDownStyle = ComboBoxStyle.DropDown, PlaceholderText = "nuevo código..." };
            _cboNewCode.Size     = new Size(380, 30);
            _cboNewCode.Location = new Point(20, y);
            _cboNewCode.KeyDown += OnCodeComboKeyDown;
            // Sugerencias defaults
            foreach (string c in AddinSettings.BuildDefaultObservationCodes())
                _cboNewCode.Items.Add(c);
            Controls.Add(_cboNewCode);

            _btnAddCode = new BtnFuturista(BtnStyle.Primary, BtnIcon.Add) { Text = "Agregar" };
            _btnAddCode.Size     = new Size(90, 30);
            _btnAddCode.Location = new Point(408, y);
            _btnAddCode.Click   += OnAddCodeClick;
            Controls.Add(_btnAddCode);
            y += 38;

            _lstCodes = new ListBoxOwner { Size = new Size(476, 180), Location = new Point(20, y) };
            _lstCodes.SelectedIndexChanged += (s, e) => UpdateRemoveButton();
            Controls.Add(_lstCodes);
            y += 188;

            _btnRemoveCode = new BtnFuturista(BtnStyle.Danger, BtnIcon.Remove) { Text = "Quitar", Enabled = false };
            _btnRemoveCode.Size     = new Size(90, 30);
            _btnRemoveCode.Location = new Point(406, y);
            _btnRemoveCode.Click   += OnRemoveCodeClick;
            Controls.Add(_btnRemoveCode);

            // ── Footer (64px) ────────────────────────────────────────────────
            var footer = new Panel
            {
                BackColor = BgPanel,
                Size      = new Size(520, 64),
                Location  = new Point(0, 416),
            };
            // top border line
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(0x22, 0x00, 0xBF, 0xFF)))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            _btnDefaults = new BtnFuturista(BtnStyle.Secondary, BtnIcon.Reset) { Text = "Defaults" };
            _btnDefaults.Size     = new Size(110, 32);
            _btnDefaults.Location = new Point(16, 16);
            _btnDefaults.Click   += OnDefaultsClick;
            footer.Controls.Add(_btnDefaults);

            _btnCancel = new BtnFuturista(BtnStyle.Secondary, BtnIcon.None) { Text = "Cancelar" };
            _btnCancel.Size     = new Size(80, 32);
            _btnCancel.Location = new Point(520 - 20 - 80, 16);
            _btnCancel.Click   += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(_btnCancel);

            _btnOk = new BtnFuturista(BtnStyle.Primary, BtnIcon.None) { Text = "OK" };
            _btnOk.Size     = new Size(70, 32);
            _btnOk.Location = new Point(520 - 20 - 80 - 8 - 70, 16);
            _btnOk.Click   += OnOkClick;
            footer.Controls.Add(_btnOk);

            // Tab order
            _cboLayer.TabIndex      = 0;
            _btnPick.TabIndex       = 1;
            _cboNewCode.TabIndex    = 2;
            _btnAddCode.TabIndex    = 3;
            _lstCodes.TabIndex      = 4;
            _btnRemoveCode.TabIndex = 5;
            _btnDefaults.TabIndex   = 6;
            _btnOk.TabIndex         = 7;
            _btnCancel.TabIndex     = 8;

            AcceptButton = _btnOk;
        }

        private Label MakeSectionLabel(string text, int x, int y)
        {
            var lbl = new SectionLabel(text) { Location = new Point(x, y), Size = new Size(480, 22) };
            return lbl;
        }

        // ── LayerTable loading ────────────────────────────────────────────────

        private void PopulateLayerCombo()
        {
            _cboLayer.Items.Clear();
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) { SetLayerComboEmpty(); return; }
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        _cboLayer.Items.Add(rec.Name);
                    }
                    tr.Commit();
                }

                if (_cboLayer.Items.Count == 0)
                    SetLayerComboEmpty();
            }
            catch
            {
                SetLayerComboEmpty();
            }
        }

        private void SetLayerComboEmpty()
        {
            _cboLayer.Items.Add("Sin layers en el dibujo");
            _cboLayer.Enabled = false;
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        private void LoadFromSettings()
        {
            string poleLayer = AddinSettings.Current.PoleLayerName;
            int idx = _cboLayer.FindStringExact(poleLayer);
            if (idx >= 0)
                _cboLayer.SelectedIndex = idx;
            else if (_cboLayer.Items.Count > 0)
                _cboLayer.SelectedIndex = 0;

            _lstCodes.Items.Clear();
            foreach (string code in AddinSettings.Current.ObservationCodes)
                _lstCodes.Items.Add(code);

            UpdateRemoveButton();
        }

        private bool ValidateAndApply()
        {
            string layer = _cboLayer.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(layer))
            {
                ShowInlineError("El layer de postes no puede estar vacío.");
                return false;
            }

            // Verify layer still exists (edge case: user deleted it mid-session)
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                        if (!lt.Has(layer))
                        {
                            tr.Commit();
                            ShowInlineError($"El layer '{layer}' ya no existe en el dibujo.");
                            return false;
                        }
                        tr.Commit();
                    }
                }
            }
            catch { /* si no podemos verificar, dejamos pasar */ }

            AddinSettings.Current.PoleLayerName = layer;

            var codes = new List<string>();
            foreach (string item in _lstCodes.Items)
                codes.Add(item);
            AddinSettings.Current.ObservationCodes = codes;

            return true;
        }

        private void ShowInlineError(string msg)
        {
            MessageBox.Show(this, msg, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnPickClick(object sender, EventArgs e)
        {
            Hide();
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) { Show(); return; }
                var ed = doc.Editor;
                var opts = new PromptEntityOptions("\nSeleccioná una entidad del layer destino: ");
                opts.AllowNone = true;
                var res = ed.GetEntity(opts);
                if (res.Status == PromptStatus.OK)
                {
                    string layerName = null;
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = (Entity)tr.GetObject(res.ObjectId, OpenMode.ForRead);
                        layerName = ent.Layer;
                        tr.Commit();
                    }
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        int idx = _cboLayer.FindStringExact(layerName);
                        if (idx < 0)
                        {
                            _cboLayer.Items.Add(layerName);
                            idx = _cboLayer.Items.Count - 1;
                        }
                        _cboLayer.SelectedIndex = idx;
                    }
                }
            }
            catch { /* usuario canceló o error de editor */ }
            finally
            {
                Show();
            }
        }

        private void OnAddCodeClick(object sender, EventArgs e)
        {
            AddCurrentCode();
        }

        private void OnCodeComboKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddCurrentCode();
                e.SuppressKeyPress = true;
                e.Handled          = true;
            }
        }

        private void AddCurrentCode()
        {
            string code = (_cboNewCode.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code)) return;

            // Check duplicate case-insensitive
            int dupIdx = -1;
            for (int i = 0; i < _lstCodes.Items.Count; i++)
            {
                if (string.Equals(_lstCodes.Items[i] as string, code, StringComparison.OrdinalIgnoreCase))
                {
                    dupIdx = i;
                    break;
                }
            }

            if (dupIdx >= 0)
            {
                // Flash the existing item
                _lstCodes.FlashItem(dupIdx);
                _cboNewCode.Text = string.Empty;
                return;
            }

            _lstCodes.Items.Add(code);
            _cboNewCode.Text = string.Empty;
            UpdateRemoveButton();
        }

        private void OnRemoveCodeClick(object sender, EventArgs e)
        {
            int idx = _lstCodes.SelectedIndex;
            if (idx >= 0)
            {
                _lstCodes.Items.RemoveAt(idx);
                if (_lstCodes.Items.Count > 0)
                    _lstCodes.SelectedIndex = Math.Min(idx, _lstCodes.Items.Count - 1);
                UpdateRemoveButton();
            }
        }

        private void OnDefaultsClick(object sender, EventArgs e)
        {
            var res = MessageBox.Show(this,
                "¿Restaurar valores por defecto?",
                "Confirmar",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (res != DialogResult.OK) return;

            AddinSettings.Current.ResetToDefaults();
            PopulateLayerCombo();
            LoadFromSettings();
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            if (!ValidateAndApply()) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        private void UpdateRemoveButton()
        {
            _btnRemoveCode.Enabled = _lstCodes.SelectedIndex >= 0;
        }

        // ── OnPaint: outer border + glow ─────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Glow
            using (var pen = new Pen(AccentGlow, 4f))
            {
                var r = new Rectangle(2, 2, Width - 4, Height - 4);
                g.DrawRectangle(pen, r);
            }
            // Border 1px accent-primary
            using (var pen = new Pen(AccentPrimary, 1f))
            {
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                g.DrawRectangle(pen, r);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: HeaderPanel
        // ─────────────────────────────────────────────────────────────────────

        private class HeaderPanel : Panel
        {
            private readonly Form _owner;
            private Point _dragStart;
            private bool  _dragging;

            public HeaderPanel(Form owner)
            {
                _owner          = owner;
                BackColor       = Color.Transparent;
                DoubleBuffered  = true;
                MouseDown      += OnMD;
                MouseMove      += OnMM;
                MouseUp        += OnMU;
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
                using (var f = new WinFont("Segoe UI", 16f, FontStyle.Bold))
                using (var b = new SolidBrush(TextPrimary))
                    g.DrawString("CONFIGURACIÓN CTO", f, b, new PointF(20, 14));

                // Tag monospace right
                using (var f = new WinFont("Courier New", 8f))
                using (var b = new SolidBrush(TextMuted))
                {
                    string tag = "[ CTO_CONFIG ]";
                    var sz = g.MeasureString(tag, f);
                    g.DrawString(tag, f, b, new PointF(Width - sz.Width - 40, 18));
                }

                // Subtitle
                using (var f = new WinFont("Segoe UI", 9f))
                using (var b = new SolidBrush(TextSecondary))
                    g.DrawString("Settings de sesión (no persisten)", f, b, new PointF(20, 48));

                // Divider bottom
                using (var pen = new Pen(Color.FromArgb(0x22, 0x00, 0xBF, 0xFF)))
                    g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }

            // Close button zone: top-right 32×32
            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                if (e.X >= Width - 36 && e.Y <= 36)
                {
                    _owner.DialogResult = DialogResult.Cancel;
                    _owner.Close();
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress */ }

            private void OnMD(object s, MouseEventArgs e)
            {
                if (e.X >= Width - 36 && e.Y <= 36) return;
                _dragging  = true;
                _dragStart = e.Location;
            }
            private void OnMM(object s, MouseEventArgs e)
            {
                if (!_dragging) return;
                var pt  = _owner.Location;
                pt.X   += e.X - _dragStart.X;
                pt.Y   += e.Y - _dragStart.Y;
                _owner.Location = pt;
            }
            private void OnMU(object s, MouseEventArgs e) { _dragging = false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: SectionLabel (title + divider line)
        // ─────────────────────────────────────────────────────────────────────

        private class SectionLabel : Label
        {
            public SectionLabel(string text)
            {
                Text      = text;
                ForeColor = AccentPrimary;
                Font      = new WinFont("Segoe UI", 10f, FontStyle.Bold);
                AutoSize  = false;
                Height    = 22;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(AccentPrimary))
                using (var f = new WinFont("Segoe UI", 10f, FontStyle.Bold))
                {
                    var sz = g.MeasureString(Text, f);
                    g.DrawString(Text, f, b, new PointF(0, 2));

                    int lineX = (int)sz.Width + 8;
                    using (var pen = new Pen(Color.FromArgb(0x22, 0x00, 0xBF, 0xFF)))
                        g.DrawLine(pen, lineX, Height / 2, Width, Height / 2);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: CboFuturista
        // ─────────────────────────────────────────────────────────────────────

        private class CboFuturista : ComboBox
        {
            public string PlaceholderText { get; set; } = string.Empty;
            private bool _focused;

            public CboFuturista()
            {
                BackColor = BgPanel;
                ForeColor = TextPrimary;
                FlatStyle = FlatStyle.Flat;
                Font      = new WinFont("Segoe UI", 10f);
                Height    = 30;
                GotFocus  += (s, e) => { _focused = true;  Invalidate(); };
                LostFocus += (s, e) => { _focused = false; Invalidate(); };
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                // Paint custom border after base
                if (m.Msg == 0xF /*WM_PAINT*/)
                {
                    using (var g = Graphics.FromHwnd(Handle))
                    {
                        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                        var pen  = _focused ? new Pen(BorderFocus) : new Pen(BorderSubtle);
                        g.DrawRectangle(pen, rect);
                        pen.Dispose();

                        if (_focused)
                        {
                            using (var glow = new Pen(AccentGlow, 2f))
                                g.DrawRectangle(glow, new Rectangle(1, 1, Width - 3, Height - 3));
                        }

                        // Custom chevron
                        int cx = Width - 18;
                        int cy = Height / 2;
                        using (var chevPen = new Pen(AccentPrimary, 1.5f))
                        {
                            g.DrawLine(chevPen, cx - 4, cy - 2, cx, cy + 2);
                            g.DrawLine(chevPen, cx, cy + 2, cx + 4, cy - 2);
                        }

                        // Placeholder
                        if (DropDownStyle == ComboBoxStyle.DropDown
                            && string.IsNullOrEmpty(Text)
                            && !string.IsNullOrEmpty(PlaceholderText))
                        {
                            using (var b = new SolidBrush(TextMuted))
                            using (var f = new WinFont("Segoe UI", 9f, FontStyle.Italic))
                                g.DrawString(PlaceholderText, f, b, new PointF(4, 7));
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: ListBoxOwner (owner-draw ListBox)
        // ─────────────────────────────────────────────────────────────────────

        private class ListBoxOwner : ListBox
        {
            private int _flashIdx = -1;
            private Timer _flashTimer;
            private bool  _flashOn;
            private bool  _hoverSet;
            private int   _hoverIdx = -1;

            public ListBoxOwner()
            {
                BackColor    = BgPanel;
                ForeColor    = TextPrimary;
                DrawMode     = DrawMode.OwnerDrawFixed;
                ItemHeight   = 28;
                BorderStyle  = BorderStyle.None;
                Font         = new WinFont("Segoe UI", 10f);
                DoubleBuffered = true;
                MouseMove   += OnMouseMoveList;
                MouseLeave  += (s, e) => { _hoverIdx = -1; Invalidate(); };
            }

            private void OnMouseMoveList(object s, MouseEventArgs e)
            {
                int idx = IndexFromPoint(e.Location);
                if (idx != _hoverIdx)
                {
                    _hoverIdx = idx;
                    Invalidate();
                }
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                var g = e.Graphics;

                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool hover    = (e.Index == _hoverIdx);
                bool flash    = (_flashIdx == e.Index && _flashOn);

                Color bg = flash   ? AccentGlow
                         : selected ? Color.FromArgb(0x33, 0x00, 0xBF, 0xFF)
                         : hover   ? BgPanelHover
                         :           BgPanel;

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, e.Bounds);

                if (selected)
                    using (var p = new Pen(AccentPrimary, 3f))
                        g.DrawLine(p, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

                string text = Items[e.Index] as string ?? string.Empty;

                // Empty state
                if (Items.Count == 0)
                {
                    using (var b = new SolidBrush(TextMuted))
                    using (var f = new WinFont("Segoe UI", 9f, FontStyle.Italic))
                        g.DrawString("Sin códigos. Agregá uno arriba.", f, b,
                            new RectangleF(e.Bounds.X + 12, e.Bounds.Y + 6, e.Bounds.Width - 12, e.Bounds.Height));
                    return;
                }

                var textRect = new RectangleF(e.Bounds.X + 12, e.Bounds.Y + 4,
                    e.Bounds.Width - 24, e.Bounds.Height - 4);
                var fmt = new StringFormat
                {
                    Trimming   = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                };

                using (var b = new SolidBrush(TextPrimary))
                using (var f = new WinFont("Segoe UI", 10f))
                    g.DrawString(text, f, b, textRect, fmt);

                // Tooltip for long text: delegate to parent ToolTip component if needed
            }

            // Empty state: override to draw placeholder when no items
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Items.Count == 0)
                {
                    using (var b = new SolidBrush(TextMuted))
                    using (var f = new WinFont("Segoe UI", 9f, FontStyle.Italic))
                        e.Graphics.DrawString("Sin códigos. Agregá uno arriba.", f, b,
                            new RectangleF(12, 8, Width - 24, Height - 8));
                }

                // Border
                bool focused = ContainsFocus;
                using (var pen = new Pen(focused ? BorderFocus : BorderSubtle))
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
            }

            public void FlashItem(int idx)
            {
                _flashIdx = idx;
                _flashOn  = true;
                _flashTimer?.Dispose();
                _flashTimer = new Timer { Interval = 200 };
                int ticks = 0;
                _flashTimer.Tick += (s, e) =>
                {
                    ticks++;
                    _flashOn = !_flashOn;
                    Invalidate();
                    if (ticks >= 2) { _flashTimer.Stop(); _flashTimer.Dispose(); _flashIdx = -1; Invalidate(); }
                };
                _flashTimer.Start();
                Invalidate();
            }

            // Workaround for OwnerDrawFixed not calling OnDrawItem when empty
            protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: BtnFuturista
        // ─────────────────────────────────────────────────────────────────────

        private enum BtnStyle { Primary, Secondary, Danger }
        private enum BtnIcon  { None, Pick, Add, Remove, Reset }

        private class BtnFuturista : Button
        {
            private readonly BtnStyle _style;
            private readonly BtnIcon  _icon;
            private bool _hover;
            private bool _pressed;

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

                MouseEnter  += (s, e) => { _hover   = true;  Invalidate(); };
                MouseLeave  += (s, e) => { _hover   = false; Invalidate(); };
                MouseDown   += (s, e) => { _pressed = true;  Invalidate(); };
                MouseUp     += (s, e) => { _pressed = false; Invalidate(); };
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g   = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rc  = new Rectangle(0, 0, Width - 1, Height - 1);

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
                        Color p1 = _pressed ? Color.FromArgb(0x00, 0x99, 0xCC)
                                 : _hover   ? AccentSecondary
                                 :            AccentPrimary;
                        Color p2 = _hover   ? AccentPrimary : AccentSecondary;
                        using (var grad = new LinearGradientBrush(rc, p1, p2, LinearGradientMode.Horizontal))
                            g.FillRectangle(grad, rc);
                        using (var pen = new Pen(AccentPrimary))
                            g.DrawRectangle(pen, rc);
                        if (_hover)
                            using (var glow = new Pen(AccentGlow, 4f))
                                g.DrawRectangle(glow, new Rectangle(1, 1, Width - 3, Height - 3));
                        using (var b = new SolidBrush(BgBase))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;

                    case BtnStyle.Secondary:
                        Color sbg = _pressed ? BgPanel : _hover ? BgPanelHover : Color.Transparent;
                        using (var b2 = new SolidBrush(sbg))
                            g.FillRectangle(b2, rc);
                        using (var pen = new Pen(_hover ? AccentPrimary : BorderSubtle))
                            g.DrawRectangle(pen, rc);
                        using (var b = new SolidBrush(TextPrimary))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;

                    case BtnStyle.Danger:
                        Color dbg = _hover ? Color.FromArgb(0x22, 0xFF, 0x55, 0x77) : Color.Transparent;
                        using (var b2 = new SolidBrush(dbg))
                            g.FillRectangle(b2, rc);
                        using (var pen = new Pen(_hover ? Danger : BorderSubtle))
                            g.DrawRectangle(pen, rc);
                        using (var b = new SolidBrush(_hover ? Danger : TextSecondary))
                        using (var f = new WinFont("Segoe UI", 9f, FontStyle.Bold))
                            DrawContent(g, f, b);
                        break;
                }
            }

            private void DrawContent(Graphics g, WinFont f, SolidBrush textBrush)
            {
                if (_icon == BtnIcon.None)
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(Text, f, textBrush, new RectangleF(0, 0, Width, Height), sf);
                    return;
                }

                // Icon 14×14, text to the right with 6px gap
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
                        // Circle with dot (crosshair simplified)
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
                        // Arc + arrow tip
                        g.DrawArc(pen, x + 1, y + 1, 12, 12, 30, 300);
                        var tip = new PointF[] {
                            new PointF(x + 11f, y + 1.5f),
                            new PointF(x + 14f, y + 5f),
                            new PointF(x + 8f,  y + 4f),
                        };
                        g.DrawPolygon(pen, tip);
                        break;
                }
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress flicker */ }
        }
    }
}
