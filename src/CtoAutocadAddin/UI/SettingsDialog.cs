using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFont = System.Drawing.Font;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
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
        private Panel                _layerPanel;
        private CheckedListBoxOwner _clbLayers;
        private Label               _lblLayerSummary;
        private CboFuturista                    _cboNewCode;
        private ListBoxOwner                    _lstCodes;
        private FuturisticTheme.DialogButton    _btnPick;
        private FuturisticTheme.DialogButton    _btnAddCode;
        private FuturisticTheme.DialogButton    _btnRemoveCode;
        private FuturisticTheme.DialogButton    _btnDefaults;
        private FuturisticTheme.DialogButton    _btnOk;
        private FuturisticTheme.DialogButton    _btnCancel;

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
        private GraphicsPath _formRegionPath;
        private bool _inResize;

        // ── Constructor ───────────────────────────────────────────────────────
        public SettingsDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(540, 620);
            MinimumSize     = new Size(500, 560);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = BgBase;
            ForeColor       = TextPrimary;
            Font            = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f);
            DoubleBuffered  = true;

            // Rounded corners 4px
            {
                var newPath = new GraphicsPath();
                newPath.AddArc(0, 0, 8, 8, 180, 90);
                newPath.AddArc(Width - 8, 0, 8, 8, 270, 90);
                newPath.AddArc(Width - 8, Height - 8, 8, 8, 0, 90);
                newPath.AddArc(0, Height - 8, 8, 8, 90, 90);
                newPath.CloseFigure();
                this.Region?.Dispose();
                this.Region = new System.Drawing.Region(newPath);
                _formRegionPath?.Dispose();
                _formRegionPath = newPath;
            }

            // Fade-in: start transparent, timer fires on Load
            this.Opacity = 0.0;

            BuildUI();

            KeyPreview = true;
            KeyDown += OnKeyDown;
            Load    += OnFormLoad;
            Resize  += OnFormResize;
        }

        // ── Form Load ─────────────────────────────────────────────────────────

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PopulateLayerList();
            LoadFromSettings();
        }

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
            if (_inResize) return;
            _inResize = true;
            try
            {
                // Recompute rounded region on resize
                {
                    var newPath = new GraphicsPath();
                    newPath.AddArc(0, 0, 8, 8, 180, 90);
                    newPath.AddArc(Width - 8, 0, 8, 8, 270, 90);
                    newPath.AddArc(Width - 8, Height - 8, 8, 8, 0, 90);
                    newPath.AddArc(0, Height - 8, 8, 8, 90, 90);
                    newPath.CloseFigure();
                    this.Region?.Dispose();
                    this.Region = new System.Drawing.Region(newPath);
                    _formRegionPath?.Dispose();
                    _formRegionPath = newPath;
                }

                // Fix 4: _layerPanel crece proporcionalmente con el alto del form
                // para que _clbLayers (Anchor=Bottom dentro de _layerPanel) no quede
                // con altura fija cuando el dialog se agranda.
                if (_layerPanel != null && Created && WindowState != FormWindowState.Minimized)
                {
                    SuspendLayout();

                    int headerH = _header?.Height ?? 0;
                    int footerH = 64;
                    const int minCodesHeight = 200;

                    int extra = Math.Max(0, Height - MinimumSize.Height);
                    int calculada = 160 + (int)(extra * 0.35);
                    int maxPermitida = ClientSize.Height - headerH - footerH - minCodesHeight;
                    _layerPanel.Height = Math.Max(160, Math.Min(calculada, maxPermitida));

                    ResumeLayout();
                }

                Invalidate();
            }
            finally
            {
                _inResize = false;
            }
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
                title:     null,
                subtitle:  null,
                tag:       null,
                showClose: true)
            {
                Dock   = DockStyle.Top,
                Height = 68,
            };

            // ── Footer panel (Dock=Bottom, 64px) ─────────────────────────────
            var footer = new Panel
            {
                BackColor = BgPanel,
                Dock      = DockStyle.Bottom,
                Height    = 64,
            };
            footer.Paint += (s, e) =>
            {
                try
                {
                    using (var pen = new Pen(FuturisticTheme.Divider))
                        e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                }
                catch { /* GDI+ transient; repaint will retry */ }
            };

            _btnDefaults = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Secondary) { Text = "Defaults" };
            _btnDefaults.Size     = new Size(110, 32);
            _btnDefaults.Location = new Point(16, 16);
            _btnDefaults.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnDefaults.Click   += OnDefaultsClick;
            footer.Controls.Add(_btnDefaults);

            _btnCancel = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Secondary) { Text = "Cancelar" };
            _btnCancel.Size     = new Size(80, 32);
            _btnCancel.Location = new Point(footer.Width - 20 - 80, 16);
            _btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCancel.Click   += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(_btnCancel);

            _btnOk = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Primary) { Text = "OK" };
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

            // ── LAYER DE POSTES section ───────────────────────────────────────
            // Fix 4: _clbLayers usa Dock=Fill dentro de _layerPanel para
            // que crezca en alto junto con el form (en vez de altura fija de 90px).
            _layerPanel = new Panel
            {
                Dock   = DockStyle.Top,
                Height = 160,
            };

            _lblLayerSummary = new Label
            {
                Text      = "0 layer(s) seleccionado(s)",
                Dock      = DockStyle.Bottom,
                Height    = 22,
                AutoSize  = false,
                ForeColor = TextSecondary,
                Font      = new WinFont(FuturisticTheme.PrimaryFontFamily, 8f),
                Padding   = new Padding(20, 0, 20, 0),
            };

            var lblLayerSection = MakeSectionLabel("LAYER DE POSTES", 0, 0);
            lblLayerSection.Dock = DockStyle.Fill;

            _btnPick = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Primary) { Text = "Pick" };
            _btnPick.Dock   = DockStyle.Right;
            _btnPick.Width  = 90;
            _btnPick.Click += OnPickClick;

            var titleRow = new Panel
            {
                Dock   = DockStyle.Top,
                Height = 34,
            };
            titleRow.Controls.Add(lblLayerSection);
            titleRow.Controls.Add(_btnPick);

            _clbLayers = new CheckedListBoxOwner
            {
                Dock = DockStyle.Fill,
            };
            _clbLayers.ItemCheck += OnLayerItemCheck;

            _layerPanel.Padding = new Padding(20, 8, 20, 4);

            // Orden de adición: Fill primero para que ocupe el resto del panel,
            // luego Bottom, luego Top (WinForms docka respetando el orden de la colección).
            _layerPanel.Controls.Add(_clbLayers);
            _layerPanel.Controls.Add(_lblLayerSummary);
            _layerPanel.Controls.Add(titleRow);

            // ── CÓDIGOS DE OBSERVACIÓN section (Dock=Fill) ────────────────────
            var codesPanel = new Panel
            {
                Dock    = DockStyle.Fill,
                Padding = new Padding(20, 4, 20, 12),
            };

            // ── Orden de adición a this.Controls (WinForms docka en orden
            // INVERSO a la colección): Fill primero, luego Top/Bottom en orden
            // inverso al visual, header al final para que quede arriba de todo.
            Controls.Add(codesPanel);
            Controls.Add(footer);
            Controls.Add(_layerPanel);
            Controls.Add(_header);

            var lblCodesSection = MakeSectionLabel("CÓDIGOS DE OBSERVACIÓN", 0, 0);
            lblCodesSection.Dock = DockStyle.Top;

            var subLabel = new Label
            {
                Text      = "Códigos que penalizan ranking de postes PRIORIDAD",
                AutoSize  = false,
                Height    = 20,
                Dock      = DockStyle.Top,
                ForeColor = TextSecondary,
                Font      = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f),
            };

            // Fila "agregar código" (Dock=Top, altura fija)
            var addRow = new Panel
            {
                Dock   = DockStyle.Top,
                Height = 38,
            };

            _cboNewCode = new CboFuturista { DropDownStyle = ComboBoxStyle.DropDown, PlaceholderText = "nuevo código..." };
            _cboNewCode.Dock     = DockStyle.Fill;
            _cboNewCode.KeyDown += OnCodeComboKeyDown;
            foreach (string c in AddinSettings.BuildDefaultObservationCodes())
                _cboNewCode.Items.Add(c);
            addRow.Controls.Add(_cboNewCode);

            var addSpacer = new Panel
            {
                Width     = 8,
                Dock      = DockStyle.Right,
                BackColor = Color.Transparent,
            };
            addRow.Controls.Add(addSpacer);

            _btnAddCode = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Primary) { Text = "Agregar" };
            _btnAddCode.Size   = new Size(90, 30);
            _btnAddCode.Dock   = DockStyle.Right;
            _btnAddCode.Click += OnAddCodeClick;
            addRow.Controls.Add(_btnAddCode);

            // Fila "quitar código" (Dock=Bottom, altura fija)
            var removeRow = new Panel
            {
                Dock   = DockStyle.Bottom,
                Height = 38,
            };

            _btnRemoveCode = new FuturisticTheme.DialogButton(FuturisticTheme.DialogBtnStyle.Danger) { Text = "Quitar", Enabled = false };
            _btnRemoveCode.Size   = new Size(90, 30);
            _btnRemoveCode.Dock   = DockStyle.Right;
            _btnRemoveCode.Click += OnRemoveCodeClick;
            removeRow.Controls.Add(_btnRemoveCode);

            _lstCodes = new ListBoxOwner
            {
                Dock = DockStyle.Fill,
            };
            _lstCodes.SelectedIndexChanged += (s, e) => UpdateRemoveButton();

            // Orden de adición a codesPanel (WinForms docka en orden INVERSO a la
            // colección: el Fill va primero para que se dockee al final y ocupe
            // solo el espacio restante — si va último, se dockea primero, ocupa
            // todo el panel y las filas Top le tapan los primeros ítems).
            codesPanel.Controls.Add(_lstCodes);        // Fill — primero
            codesPanel.Controls.Add(removeRow);        // Bottom
            codesPanel.Controls.Add(addRow);           // Top (más cercana a la lista)
            codesPanel.Controls.Add(subLabel);         // Top
            codesPanel.Controls.Add(lblCodesSection);  // Top — arriba de todo

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
                    var names = new List<string>();
                    foreach (ObjectId id in lt)
                    {
                        var rec = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        names.Add(rec.Name);
                    }
                    tr.Commit();

                    foreach (string name in names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                        _clbLayers.Items.Add(name, false);
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

            if (_lstCodes.Items.Count > 0) _lstCodes.TopIndex = 0;
            _lstCodes.ClearSelected();

            UpdateRemoveButton();

            Infrastructure.AcadLogger.Info($"CTO_CONFIG: {_lstCodes.Items.Count} códigos de observación cargados.");
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
            if (Width <= 0 || Height <= 0) return;
            try
            {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Animated border glow: steel
            int glowAlpha = (int)(25 + 15 * Math.Sin(_glowPhase));

            using (var pen = new Pen(Color.FromArgb(glowAlpha, FuturisticTheme.Steel), 3f))
                g.DrawRectangle(pen, new Rectangle(-2, -2, Width + 3, Height + 3));

            using (var pen = new Pen(Color.FromArgb(glowAlpha, FuturisticTheme.Steel), 2f))
                g.DrawRectangle(pen, new Rectangle(-1, -1, Width + 1, Height + 1));

            // Static border: BorderStrong
            using (var pen = new Pen(FuturisticTheme.BorderStrong, 1f))
                g.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
            } catch { /* GDI+ transient; repaint will retry */ }
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
                _formRegionPath?.Dispose();
                this.Region?.Dispose();
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
                if (Width <= 0 || Height <= 0) return;
                try
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
                } catch { /* GDI+ transient; repaint will retry */ }
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
                Text      = text.ToUpperInvariant();
                ForeColor = FuturisticTheme.Steel;
                Font      = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f, FontStyle.Bold);
                AutoSize  = false;
                Height    = 22;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;
                try
                {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(FuturisticTheme.Steel))
                using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f, FontStyle.Bold))
                {
                    string label = Text;
                    var sz = g.MeasureString(label, f);
                    g.DrawString(label, f, b, new PointF(0, 2));

                    int lineX = (int)sz.Width + 8;
                    using (var pen = new Pen(FuturisticTheme.Divider))
                        g.DrawLine(pen, lineX, Height / 2, Width, Height / 2);
                }
                } catch { /* GDI+ transient; repaint will retry */ }
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
                Font           = new WinFont(FuturisticTheme.PrimaryFontFamily, 10f);
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
                    ? Color.FromArgb(0x40, FuturisticTheme.Steel.R, FuturisticTheme.Steel.G, FuturisticTheme.Steel.B)
                    : BgPanel;

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, e.Bounds);

                if (selected)
                    using (var p = new Pen(FuturisticTheme.Steel, 3f))
                        g.DrawLine(p, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

                // Checkbox indicator
                int cbx = e.Bounds.X + 4;
                int cby = e.Bounds.Y + (e.Bounds.Height - 12) / 2;
                var cbRect = new Rectangle(cbx, cby, 12, 12);

                using (var pen = new Pen(ischecked ? FuturisticTheme.Steel : BorderSubtle))
                    g.DrawRectangle(pen, cbRect);

                if (ischecked)
                {
                    using (var pen = new Pen(FuturisticTheme.Steel, 2f))
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
                if (text.IndexOf("POSTE", StringComparison.OrdinalIgnoreCase) >= 0)
                    textColor = FuturisticTheme.Info;
                using (var b = new SolidBrush(textColor))
                using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 10f))
                    g.DrawString(text, f, b, textRect, fmt);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;
                try
                {
                base.OnPaint(e);
                bool focused = ContainsFocus;
                using (var pen = new Pen(focused ? BorderFocus : BorderSubtle))
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
                } catch { /* GDI+ transient; repaint will retry */ }
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                try
                {
                    using (var b = new SolidBrush(BgPanel))
                        e.Graphics.FillRectangle(b, ClientRectangle);
                }
                catch { /* GDI+ transient; repaint will retry */ }
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
                Font      = new WinFont(FuturisticTheme.PrimaryFontFamily, 10f);
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
                        using (var chevPen = new Pen(FuturisticTheme.Steel, 1.5f))
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
                            using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f, FontStyle.Italic))
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
                Font           = new WinFont(FuturisticTheme.PrimaryFontFamily, 10f);
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

                Color bg = flash   ? FuturisticTheme.AccentGlow
                         : selected ? Color.FromArgb(0x40, FuturisticTheme.Steel.R, FuturisticTheme.Steel.G, FuturisticTheme.Steel.B)
                         : hover   ? BgPanelHover
                         :           BgPanel;

                using (var b = new SolidBrush(bg))
                    g.FillRectangle(b, e.Bounds);

                if (selected)
                    using (var p = new Pen(FuturisticTheme.Steel, 3f))
                        g.DrawLine(p, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);

                string text = Items[e.Index] as string ?? string.Empty;

                if (Items.Count == 0)
                {
                    using (var b = new SolidBrush(TextMuted))
                    using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f, FontStyle.Italic))
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
                using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 10f))
                    g.DrawString(text, f, b, textRect, fmt);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 0 || Height <= 0) return;
                try
                {
                base.OnPaint(e);
                if (Items.Count == 0)
                {
                    using (var b = new SolidBrush(TextMuted))
                    using (var f = new WinFont(FuturisticTheme.PrimaryFontFamily, 9f, FontStyle.Italic))
                        e.Graphics.DrawString("Sin códigos. Agregá uno arriba.", f, b,
                            new RectangleF(12, 8, Width - 24, Height - 8));
                }

                bool focused = ContainsFocus;
                using (var pen = new Pen(focused ? BorderFocus : BorderSubtle))
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
                } catch { /* GDI+ transient; repaint will retry */ }
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
