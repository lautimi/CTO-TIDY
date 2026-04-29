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
        // ── Design tokens (delegated to FuturisticTheme) ─────────────────────
        private static Color BgBase          => FuturisticTheme.BgBase;
        private static Color BgPanel         => FuturisticTheme.BgPanel;
        private static Color BgPanelHover    => FuturisticTheme.BgPanelHover;
        private static Color BorderSubtle    => FuturisticTheme.BorderSubtle;
        private static Color BorderFocus     => FuturisticTheme.BorderFocus;
        private static Color AccentPrimary   => FuturisticTheme.AccentPrimary;
        private static Color AccentSecondary => FuturisticTheme.AccentSecondary;
        private static Color AccentGlow      => FuturisticTheme.AccentGlow;
        private static Color TextPrimary     => FuturisticTheme.TextPrimary;
        private static Color TextSecondary   => FuturisticTheme.TextSecondary;
        private static Color TextMuted       => FuturisticTheme.TextMuted;
        private static Color Danger          => FuturisticTheme.Danger;

        // ── Controls ─────────────────────────────────────────────────────────
        private CheckedListBoxOwner _clbLayers;
        private Label               _lblLayerSummary;
        private CboFuturista                    _cboNewCode;
        private ListBoxOwner                    _lstCodes;
        private FuturisticTheme.BtnFuturista    _btnPick;
        private FuturisticTheme.BtnFuturista    _btnAddCode;
        private FuturisticTheme.BtnFuturista    _btnRemoveCode;
        private FuturisticTheme.BtnFuturista    _btnDefaults;
        private FuturisticTheme.BtnFuturista    _btnOk;
        private FuturisticTheme.BtnFuturista    _btnCancel;

        // drag header (kept for form-drag fallback outside header)
        private Point _dragStart;
        private bool  _dragging;

        // ── Animation state ──────────────────────────────────────────────────
        private Timer  _fadeTimer;
        private Timer  _glowTimer;
        private Timer  _shimmerTimer;
        private double _glowPhase  = 0.0;
        private float  _shimmerX   = -200f;
        private FuturisticTheme.HeaderPanel _header;

        // ── Constructor ───────────────────────────────────────────────────────
        public SettingsDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(520, 480);
            MinimumSize     = new Size(520, 480);
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
            this.Region = new System.Drawing.Region(path);

            // Fade-in: start transparent, timer fires on Load
            this.Opacity = 0.0;

            BuildUI();
            PopulateLayerList();
            LoadFromSettings();

            KeyPreview = true;
            KeyDown += OnKeyDown;
            Load    += OnFormLoad;
            Resize  += OnFormResize;
        }

        // ── Form Load ─────────────────────────────────────────────────────────

        private void OnFormLoad(object sender, EventArgs e)
        {
            // 1. Fade-in timer
            _fadeTimer = new Timer { Interval = 16 };
            _fadeTimer.Tick += OnFadeTick;
            _fadeTimer.Start();

            // 2. Glow pulse timer
            _glowTimer = new Timer { Interval = 30 };
            _glowTimer.Tick += OnGlowTick;
            _glowTimer.Start();

            // 3. Shimmer timer
            _shimmerTimer = new Timer { Interval = 16 };
            _shimmerTimer.Tick += OnShimmerTick;
            _shimmerTimer.Start();
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            // Recompute rounded region on resize
            var path = new GraphicsPath();
            path.AddArc(0, 0, 8, 8, 180, 90);
            path.AddArc(Width - 8, 0, 8, 8, 270, 90);
            path.AddArc(Width - 8, Height - 8, 8, 8, 0, 90);
            path.AddArc(0, Height - 8, 8, 8, 90, 90);
            path.CloseFigure();
            this.Region = new System.Drawing.Region(path);
            Invalidate();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            double next = Opacity + 0.07;
            if (next >= 1.0)
            {
                Opacity = 1.0;
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }
            else
            {
                Opacity = next;
            }
        }

        private void OnGlowTick(object sender, EventArgs e)
        {
            _glowPhase += 0.08;
            if (_glowPhase > Math.PI * 2)
                _glowPhase -= Math.PI * 2;
            this.Invalidate();
            if (_header != null)
                _header.Invalidate();
        }

        private void OnShimmerTick(object sender, EventArgs e)
        {
            _shimmerX += 8f;
            if (_header != null)
            {
                if (_shimmerX > _header.Width + 200)
                {
                    _shimmerTimer.Stop();
                    _shimmerTimer.Dispose();
                    _shimmerTimer = null;
                    return;
                }
                _header.Invalidate();
            }
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            // ── Header panel (Dock=Top, 72px) ────────────────────────────────
            _header = new FuturisticTheme.HeaderPanel(
                this, GetGlowPhase, GetShimmerX,
                title:     "CONFIGURACIÓN CTO",
                subtitle:  "Settings de sesión (no persisten)",
                tag:       "[ CTO_CONFIG ]",
                showClose: true)
            {
                Dock   = DockStyle.Top,
                Height = 72,
            };

            var markImg = FuturisticTheme.LoadEmbeddedImage(
                "Koovra.Cto.AutocadAddin.UI.Assets.vezeel-mark.png");
            if (markImg != null)
            {
                var markPb = new PictureBox
                {
                    Image     = markImg,
                    SizeMode  = PictureBoxSizeMode.Zoom,
                    Height    = 30,
                    Width     = 60,
                    Location  = new Point(14, 21),
                    BackColor = Color.Transparent,
                };
                _header.Controls.Add(markPb);
            }

            Controls.Add(_header);

            // ── Hairline brand stripe ────────────────────────────────────────
            var hairline = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 2,
                BackColor = Color.Transparent,
            };
            hairline.Paint += (s, e) => FuturisticTheme.PaintHairlineStripe(e.Graphics, hairline.Width);
            Controls.Add(hairline);

            // ── Footer panel (Dock=Bottom, 64px) ─────────────────────────────
            var footer = new Panel
            {
                BackColor = BgPanel,
                Dock      = DockStyle.Bottom,
                Height    = 64,
            };
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(FuturisticTheme.Divider))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            Controls.Add(footer);

            _btnDefaults = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Secondary, FuturisticTheme.BtnIcon.Reset) { Text = "Defaults" };
            _btnDefaults.Size     = new Size(110, 32);
            _btnDefaults.Location = new Point(16, 16);
            _btnDefaults.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnDefaults.Click   += OnDefaultsClick;
            footer.Controls.Add(_btnDefaults);

            _btnCancel = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Secondary, FuturisticTheme.BtnIcon.None) { Text = "Cancelar" };
            _btnCancel.Size     = new Size(80, 32);
            _btnCancel.Location = new Point(footer.Width - 20 - 80, 16);
            _btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCancel.Click   += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(_btnCancel);

            _btnOk = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Primary, FuturisticTheme.BtnIcon.None) { Text = "OK" };
            _btnOk.Size     = new Size(70, 32);
            _btnOk.Location = new Point(footer.Width - 20 - 80 - 8 - 70, 16);
            _btnOk.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnOk.Click   += OnOkClick;
            footer.Controls.Add(_btnOk);

            // ── Grip panel 16×16 bottom-right corner ──────────────────────────
            var gripPanel = new GripPanel
            {
                Size   = new Size(16, 16),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            // Will be positioned after footer is added; use Resize to keep aligned
            gripPanel.Location = new Point(footer.Width - 16, 64 - 16);
            footer.Controls.Add(gripPanel);
            gripPanel.ResizeTarget = this;

            // ── Body content ─────────────────────────────────────────────────
            int y = 82; // 72px header + 2px hairline

            // ── LAYER DE POSTES section ───────────────────────────────────────
            var lblLayerSection = MakeSectionLabel("LAYER DE POSTES", 20, y);
            lblLayerSection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblLayerSection);
            y += 28;

            _clbLayers = new CheckedListBoxOwner
            {
                Size     = new Size(380, 90),
                Location = new Point(20, y),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            _clbLayers.ItemCheck += OnLayerItemCheck;
            Controls.Add(_clbLayers);

            _btnPick = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Primary, FuturisticTheme.BtnIcon.Pick) { Text = "Pick" };
            _btnPick.Size     = new Size(90, 30);
            _btnPick.Location = new Point(408, y);
            _btnPick.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnPick.Click   += OnPickClick;
            Controls.Add(_btnPick);
            y += 96;

            _lblLayerSummary = new Label
            {
                Text      = "0 layer(s) seleccionado(s)",
                Location  = new Point(20, y),
                AutoSize  = false,
                Size      = new Size(480, 18),
                ForeColor = TextSecondary,
                Font      = new WinFont("Segoe UI", 8f),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(_lblLayerSummary);
            y += 22;

            // ── CÓDIGOS DE OBSERVACIÓN section ──────────────────────────────
            var lblCodesSection = MakeSectionLabel("CÓDIGOS DE OBSERVACIÓN", 20, y);
            lblCodesSection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(lblCodesSection);
            y += 24;

            var subLabel = new Label
            {
                Text      = "Códigos que penalizan ranking de postes PRIORIDAD",
                Location  = new Point(20, y),
                AutoSize  = true,
                ForeColor = TextSecondary,
                Font      = new WinFont("Segoe UI", 9f),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left,
            };
            Controls.Add(subLabel);
            y += 20;

            // Fix 3: _btnAddCode same row as _cboNewCode
            _cboNewCode = new CboFuturista { DropDownStyle = ComboBoxStyle.DropDown, PlaceholderText = "nuevo código..." };
            _cboNewCode.Size     = new Size(380, 30);
            _cboNewCode.Location = new Point(20, y);
            _cboNewCode.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _cboNewCode.KeyDown += OnCodeComboKeyDown;
            foreach (string c in AddinSettings.BuildDefaultObservationCodes())
                _cboNewCode.Items.Add(c);
            Controls.Add(_cboNewCode);

            _btnAddCode = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Primary, FuturisticTheme.BtnIcon.Add) { Text = "Agregar" };
            _btnAddCode.Size     = new Size(90, 30);
            _btnAddCode.Location = new Point(408, y);
            _btnAddCode.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnAddCode.Click   += OnAddCodeClick;
            Controls.Add(_btnAddCode);
            y += 38;

            // Fix 3: _lstCodes anchored Top|Left|Right|Bottom so it grows with resize
            _lstCodes = new ListBoxOwner
            {
                Size     = new Size(476, 100),
                Location = new Point(20, y),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            };
            _lstCodes.SelectedIndexChanged += (s, e) => UpdateRemoveButton();
            Controls.Add(_lstCodes);

            // Fix 3: _btnRemoveCode below listbox, aligned right, Anchor=Bottom|Right
            _btnRemoveCode = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Danger, FuturisticTheme.BtnIcon.Remove) { Text = "Quitar", Enabled = false };
            _btnRemoveCode.Size     = new Size(90, 30);
            // Position relative to bottom of form (footer is 64px, so 64+30+8=102 from bottom)
            _btnRemoveCode.Location = new Point(406, Height - 64 - 30 - 8);
            _btnRemoveCode.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnRemoveCode.Click   += OnRemoveCodeClick;
            Controls.Add(_btnRemoveCode);

            // Tab order
            _clbLayers.TabIndex     = 0;
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

        private double GetGlowPhase() { return _glowPhase; }
        private float  GetShimmerX()  { return _shimmerX; }

        private Label MakeSectionLabel(string text, int x, int y)
        {
            var lbl = new SectionLabel(text) { Location = new Point(x, y), Size = new Size(480, 22) };
            return lbl;
        }

        // ── LayerTable loading ────────────────────────────────────────────────

        private void PopulateLayerList()
        {
            _clbLayers.Items.Clear();
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) { return; }
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt)
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        _clbLayers.Items.Add(rec.Name, false);
                    }
                    tr.Commit();
                }
            }
            catch { /* no layers available */ }
        }

        private void UpdateLayerSummary()
        {
            int count = 0;
            for (int i = 0; i < _clbLayers.Items.Count; i++)
            {
                if (_clbLayers.GetItemChecked(i))
                    count++;
            }
            _lblLayerSummary.Text = count + " layer(s) seleccionado(s)";
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        private void LoadFromSettings()
        {
            List<string> poleLayerNames = AddinSettings.Current.PoleLayerNames;

            for (int i = 0; i < _clbLayers.Items.Count; i++)
            {
                string itemName = _clbLayers.Items[i] as string ?? string.Empty;
                bool shouldCheck = false;
                foreach (string pln in poleLayerNames)
                {
                    if (string.Equals(itemName, pln, StringComparison.OrdinalIgnoreCase))
                    {
                        shouldCheck = true;
                        break;
                    }
                }
                _clbLayers.SetItemChecked(i, shouldCheck);
            }

            UpdateLayerSummary();

            _lstCodes.Items.Clear();
            foreach (string code in AddinSettings.Current.ObservationCodes)
                _lstCodes.Items.Add(code);

            UpdateRemoveButton();
        }

        private bool ValidateAndApply()
        {
            var selectedLayers = new List<string>();
            for (int i = 0; i < _clbLayers.Items.Count; i++)
            {
                if (_clbLayers.GetItemChecked(i))
                    selectedLayers.Add(_clbLayers.Items[i] as string ?? string.Empty);
            }

            if (selectedLayers.Count == 0)
            {
                ShowInlineError("Seleccioná al menos un layer de postes.");
                return false;
            }

            AddinSettings.Current.PoleLayerNames = selectedLayers;

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

        private void OnLayerItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Defer summary update until after check state is applied
            var timer = new Timer { Interval = 1 };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                timer.Dispose();
                UpdateLayerSummary();
            };
            timer.Start();
        }

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
                        // Find item in CheckedListBox and toggle its checked state
                        int idx = -1;
                        for (int i = 0; i < _clbLayers.Items.Count; i++)
                        {
                            if (string.Equals(_clbLayers.Items[i] as string, layerName, StringComparison.OrdinalIgnoreCase))
                            {
                                idx = i;
                                break;
                            }
                        }
                        if (idx < 0)
                        {
                            // Layer not in list yet — add and check it
                            _clbLayers.Items.Add(layerName, true);
                        }
                        else
                        {
                            // Toggle
                            _clbLayers.SetItemChecked(idx, !_clbLayers.GetItemChecked(idx));
                        }
                        UpdateLayerSummary();
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
            PopulateLayerList();
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

        // ── OnPaint: outer border + animated glow ────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Animated halo: 3 concentric layers
            int glowAlpha = (int)(30 + 20 * Math.Sin(_glowPhase));

            using (var pen = new Pen(Color.FromArgb(glowAlpha, 0, 191, 255), 3f))
                g.DrawRectangle(pen, new Rectangle(-2, -2, Width + 3, Height + 3));

            using (var pen = new Pen(Color.FromArgb(glowAlpha, 0, 191, 255), 2f))
                g.DrawRectangle(pen, new Rectangle(-1, -1, Width + 1, Height + 1));

            using (var pen = new Pen(Color.FromArgb(glowAlpha, 0, 191, 255), 1f))
                g.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));

            // Static glow fill
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

        // ── Dispose ───────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fadeTimer != null)
                {
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    _fadeTimer = null;
                }
                if (_glowTimer != null)
                {
                    _glowTimer.Stop();
                    _glowTimer.Dispose();
                    _glowTimer = null;
                }
                if (_shimmerTimer != null)
                {
                    _shimmerTimer.Stop();
                    _shimmerTimer.Dispose();
                    _shimmerTimer = null;
                }
            }
            base.Dispose(disposing);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Inner control: GripPanel (resize grip bottom-right)
        // ─────────────────────────────────────────────────────────────────────

        private class GripPanel : Panel
        {
            public Form ResizeTarget { get; set; }

            private bool  _resizing;
            private Point _resizeStart;
            private Size  _resizeStartSize;

            public GripPanel()
            {
                BackColor      = Color.Transparent;
                DoubleBuffered = true;
                Cursor         = Cursors.SizeNWSE;
                MouseDown     += OnMD;
                MouseMove     += OnMM;
                MouseUp       += OnMU;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Draw 3 diagonal lines as grip indicator
                using (var pen = new Pen(TextMuted, 1f))
                {
                    g.DrawLine(pen, 4, 14, 14, 4);
                    g.DrawLine(pen, 8, 14, 14, 8);
                    g.DrawLine(pen, 12, 14, 14, 12);
                }
            }

            private void OnMD(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || ResizeTarget == null) return;
                _resizing        = true;
                _resizeStart     = ResizeTarget.PointToScreen(e.Location);
                _resizeStartSize = ResizeTarget.Size;
                Capture          = true;
            }

            private void OnMM(object s, MouseEventArgs e)
            {
                if (!_resizing || ResizeTarget == null) return;
                var current = ResizeTarget.PointToScreen(e.Location);
                int dx = current.X - _resizeStart.X;
                int dy = current.Y - _resizeStart.Y;
                int newW = Math.Max(ResizeTarget.MinimumSize.Width,  _resizeStartSize.Width  + dx);
                int newH = Math.Max(ResizeTarget.MinimumSize.Height, _resizeStartSize.Height + dy);
                ResizeTarget.Size = new Size(newW, newH);
            }

            private void OnMU(object s, MouseEventArgs e)
            {
                _resizing = false;
                Capture   = false;
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress */ }
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
        // Inner control: CheckedListBoxOwner (dark/cyan theme)
        // ─────────────────────────────────────────────────────────────────────

        private class CheckedListBoxOwner : CheckedListBox
        {
            public CheckedListBoxOwner()
            {
                BackColor      = BgPanel;
                ForeColor      = TextPrimary;
                DrawMode       = DrawMode.OwnerDrawFixed;
                ItemHeight     = 24;
                BorderStyle    = BorderStyle.None;
                Font           = new WinFont("Segoe UI", 10f);
                DoubleBuffered = true;
                CheckOnClick   = true;
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                var g = e.Graphics;

                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool ischecked = GetItemChecked(e.Index);

                Color bg = selected
                    ? Color.FromArgb(0x33, 0x00, 0xBF, 0xFF)
                    : BgPanel;

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, e.Bounds);

                if (selected)
                    using (var p = new Pen(AccentPrimary, 3f))
                        g.DrawLine(p, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

                // Checkbox indicator
                int cbx = e.Bounds.X + 4;
                int cby = e.Bounds.Y + (e.Bounds.Height - 12) / 2;
                var cbRect = new Rectangle(cbx, cby, 12, 12);

                using (var pen = new Pen(ischecked ? AccentPrimary : BorderSubtle))
                    g.DrawRectangle(pen, cbRect);

                if (ischecked)
                {
                    using (var pen = new Pen(AccentPrimary, 2f))
                    {
                        g.DrawLine(pen, cbx + 2, cby + 6, cbx + 5, cby + 9);
                        g.DrawLine(pen, cbx + 5, cby + 9, cbx + 10, cby + 3);
                    }
                }

                string text = Items[e.Index] as string ?? string.Empty;
                var textRect = new RectangleF(e.Bounds.X + 22, e.Bounds.Y + 4,
                    e.Bounds.Width - 26, e.Bounds.Height - 4);
                var fmt = new StringFormat
                {
                    Trimming    = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                };

                Color textColor = ischecked ? TextPrimary : TextSecondary;
                using (var b = new SolidBrush(textColor))
                using (var f = new WinFont("Segoe UI", 10f))
                    g.DrawString(text, f, b, textRect, fmt);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                bool focused = ContainsFocus;
                using (var pen = new Pen(focused ? BorderFocus : BorderSubtle))
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                using (var b = new SolidBrush(BgPanel))
                    e.Graphics.FillRectangle(b, ClientRectangle);
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
            private int   _hoverIdx = -1;

            public ListBoxOwner()
            {
                BackColor      = BgPanel;
                ForeColor      = TextPrimary;
                DrawMode       = DrawMode.OwnerDrawFixed;
                ItemHeight     = 28;
                BorderStyle    = BorderStyle.None;
                Font           = new WinFont("Segoe UI", 10f);
                DoubleBuffered = true;
                MouseMove     += OnMouseMoveList;
                MouseLeave    += (s, e) => { _hoverIdx = -1; Invalidate(); };
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
                    Trimming    = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                };

                using (var b = new SolidBrush(TextPrimary))
                using (var f = new WinFont("Segoe UI", 10f))
                    g.DrawString(text, f, b, textRect, fmt);
            }

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

            protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
        }

    }
}
