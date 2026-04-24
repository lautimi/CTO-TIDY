// Aliases para resolver ambigüedad entre namespaces de AutoCAD y WinForms
using AcApp      = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFont    = System.Drawing.Font;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Services;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.UI
{
    /// <summary>
    /// Panel modeless que orquesta el workflow CTO completo.
    /// Abrir con: CTO_PANEL
    /// </summary>
    public class CtoPanel : Form
    {
        private StepRow       _rowPostes, _rowAsociar, _rowComentarios, _rowCalcular, _rowDesplegar;
        private FuturisticTheme.BtnFuturista _btnRunAll;
        private RichTextBox   _log;
        private NumericUpDown _nudRadius;

        // Animation
        private Timer _fadeTimer;
        private Timer _pulseTimer;

        public CtoPanel()
        {
            this.Opacity = 0.0;
            BuildUI();
            Load += OnFormLoad;
        }

        // ── Form Load ────────────────────────────────────────────────────────

        private void OnFormLoad(object sender, EventArgs e)
        {
            _fadeTimer = new Timer { Interval = 16 };
            _fadeTimer.Tick += (s, ev) =>
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
            };
            _fadeTimer.Start();

            // Pulse timer: drives Running dot animations in all StepRows
            _pulseTimer = new Timer { Interval = 30 };
            _pulseTimer.Tick += (s, ev) =>
            {
                _rowPostes.PulseTick();
                _rowAsociar.PulseTick();
                _rowComentarios.PulseTick();
                _rowCalcular.PulseTick();
                _rowDesplegar.PulseTick();
            };
            _pulseTimer.Start();
        }

        // ── Construcción de UI ───────────────────────────────────────────────

        private void BuildUI()
        {
            Text            = "CTO Workflow";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition   = FormStartPosition.Manual;
            Location        = new Point(100, 100);
            Size            = new Size(370, 600);
            MinimumSize     = new Size(330, 500);
            BackColor       = FuturisticTheme.BgBase;
            ForeColor       = FuturisticTheme.TextPrimary;
            Font            = new WinFont("Segoe UI", 9f);
            DoubleBuffered  = true;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(0),
                BackColor   = Color.Transparent,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddRow(int h) => layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            AddRow(48);  // header
            AddRow(44);  // paso 1
            AddRow(44);  // paso 2
            AddRow(44);  // paso 3
            AddRow(44);  // paso 4
            AddRow(44);  // paso 5
            AddRow(36);  // radio
            AddRow(34);  // inspeccionar
            AddRow(34);  // configuración
            AddRow(42);  // run all
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log

            // ── Header compacto ──────────────────────────────────────────────
            var header = new FuturisticTheme.HeaderPanel(
                this,
                getGlowPhase: null,
                getShimmerX:  null,
                title:        "⚡  CTO — Workflow FTTH",
                subtitle:     null,
                tag:          null,
                showClose:    false)
            {
                Dock   = DockStyle.Fill,
                Height = 48,
            };
            layout.Controls.Add(header, 0, 0);

            // ── Pasos ────────────────────────────────────────────────────────
            _rowPostes      = new StepRow("1. Seleccionar postes");
            _rowAsociar     = new StepRow("2. Asociar postes");
            _rowComentarios = new StepRow("3. Leer comentarios (HP)");
            _rowCalcular    = new StepRow("4. Calcular CTOs");
            _rowDesplegar   = new StepRow("5. Desplegar CTOs");

            _rowPostes.Button.Click      += (s, e) => RunStep(_rowPostes,      StepSeleccionar);
            _rowAsociar.Button.Click     += (s, e) => RunStep(_rowAsociar,     StepAsociar);
            _rowComentarios.Button.Click += (s, e) => RunStep(_rowComentarios, StepComentarios);
            _rowCalcular.Button.Click    += (s, e) => RunStep(_rowCalcular,    StepCalcular);
            _rowDesplegar.Button.Click   += (s, e) => RunStep(_rowDesplegar,   StepDesplegar);

            layout.Controls.Add(_rowPostes,      0, 1);
            layout.Controls.Add(_rowAsociar,     0, 2);
            layout.Controls.Add(_rowComentarios, 0, 3);
            layout.Controls.Add(_rowCalcular,    0, 4);
            layout.Controls.Add(_rowDesplegar,   0, 5);

            // ── Radio buffer ─────────────────────────────────────────────────
            var radioRow = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = FuturisticTheme.BgPanel,
                Padding   = new Padding(8, 4, 8, 4),
            };
            radioRow.Paint += (s, e) =>
            {
                using (var pen = new Pen(FuturisticTheme.BorderSubtle))
                    e.Graphics.DrawLine(pen, 0, radioRow.Height - 1, radioRow.Width, radioRow.Height - 1);
            };

            var lblRadius = new Label
            {
                Text      = "Radio buffer (m):",
                ForeColor = FuturisticTheme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(8, 9),
                Font      = new WinFont("Segoe UI", 8.5f),
            };

            _nudRadius = new NumericUpDown
            {
                Minimum       = 1,
                Maximum       = 50,
                Value         = (decimal)AddinSettings.Current.TextBufferRadius,
                DecimalPlaces = 1,
                Increment     = 0.5m,
                Width         = 60,
                BackColor     = FuturisticTheme.BgPanel,
                ForeColor     = FuturisticTheme.TextPrimary,
                BorderStyle   = BorderStyle.None,
                Location      = new Point(140, 6),
                Font          = new WinFont("Segoe UI", 8.5f),
            };
            _nudRadius.Paint += (s, e) =>
            {
                using (var pen = new Pen(FuturisticTheme.BorderSubtle))
                    e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, _nudRadius.Width - 1, _nudRadius.Height - 1));
            };

            radioRow.Controls.Add(lblRadius);
            radioRow.Controls.Add(_nudRadius);
            layout.Controls.Add(radioRow, 0, 6);

            // ── Inspeccionar ─────────────────────────────────────────────────
            var btnInspect = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Secondary)
            {
                Text   = "Inspeccionar poste (diagnóstico)",
                Dock   = DockStyle.Fill,
                Margin = new Padding(8, 4, 8, 2),
                Font   = new WinFont("Segoe UI", 8.5f, FontStyle.Bold),
            };
            btnInspect.Click += (s, e) =>
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute("CTO_INSPECCIONAR ", true, false, false);
            };
            layout.Controls.Add(btnInspect, 0, 7);

            // ── Configuración ────────────────────────────────────────────────
            var btnConfig = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Secondary)
            {
                Text   = "Configuración",
                Dock   = DockStyle.Fill,
                Margin = new Padding(8, 2, 8, 4),
                Font   = new WinFont("Segoe UI", 8.5f, FontStyle.Bold),
            };
            btnConfig.Click += (s, e) =>
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute("CTO_CONFIG ", true, false, false);
            };
            layout.Controls.Add(btnConfig, 0, 8);

            // ── Ejecutar Todo ────────────────────────────────────────────────
            _btnRunAll = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Primary)
            {
                Text   = "EJECUTAR TODO  (pasos 1 → 5)",
                Dock   = DockStyle.Fill,
                Height = 42,
                Margin = new Padding(8, 4, 8, 4),
                Font   = new WinFont("Segoe UI", 9f, FontStyle.Bold),
            };
            _btnRunAll.SetColorOverride(
                Color.FromArgb(0x00, 0xC8, 0x96),
                Color.FromArgb(0x00, 0xA8, 0x7D));
            _btnRunAll.Click += (s, e) => RunAll();
            layout.Controls.Add(_btnRunAll, 0, 9);

            // ── Log ──────────────────────────────────────────────────────────
            _log = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = FuturisticTheme.BgBase,
                ForeColor   = FuturisticTheme.TextSecondary,
                Font        = new WinFont("Consolas", 8f),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
            };
            layout.Controls.Add(_log, 0, 10);

            Controls.Add(layout);
            AppendLog("Panel listo. Ejecutá los pasos en orden o usá 'Ejecutar Todo'.", LogLevel.Info);
        }

        // ── Ejecución ────────────────────────────────────────────────────────

        private void RunAll()
        {
            RunStep(_rowPostes,      StepSeleccionar);
            RunStep(_rowAsociar,     StepAsociar);
            RunStep(_rowComentarios, StepComentarios);
            RunStep(_rowCalcular,    StepCalcular);
            RunStep(_rowDesplegar,   StepDesplegar);
        }

        private void RunStep(StepRow row, Func<StepResult> action)
        {
            row.SetStatus(StepStatus.Running);
            Refresh();
            try
            {
                StepResult r = action();
                row.SetStatus(r.Ok ? StepStatus.Ok : StepStatus.Warning);
                AppendLog(r.Message, r.Ok ? LogLevel.Ok : LogLevel.Warn);
            }
            catch (Exception ex)
            {
                row.SetStatus(StepStatus.Error);
                AppendLog($"ERROR: {ex.Message}", LogLevel.Error);
            }
        }

        // ── Paso 1 ───────────────────────────────────────────────────────────

        private StepResult StepSeleccionar()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
            var poleLayerNames = AddinSettings.Current.PoleLayerNames;
            var combined = new ObjectIdCollection();
            foreach (string poleLayer in poleLayerNames)
            {
                var layerIds = SelectionService.SelectAllOnLayer(ed, poleLayer, "INSERT");
                foreach (ObjectId id in layerIds)
                    combined.Add(id);
            }

            if (combined.Count == 0)
            {
                _rowPostes.SetStatus(StepStatus.Warning);
                _rowPostes.SetInfo("0");
                return new StepResult(false, "No se encontraron bloques en los layers de postes configurados");
            }

            var arr = new ObjectId[combined.Count];
            combined.CopyTo(arr, 0);
            SelectionContext.Instance.SetPostes(arr);
            SelectionContext.Instance.ClearGeometry();

            _rowPostes.SetStatus(StepStatus.Ok);
            _rowPostes.SetInfo($"{combined.Count} postes");
            return new StepResult(true, $"Paso 1 OK — {combined.Count} postes");
        }

        // ── Paso 2 ───────────────────────────────────────────────────────────

        private StepResult StepAsociar()
        {
            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                _rowAsociar.SetStatus(StepStatus.Warning);
                return new StepResult(false, "Ejecutá el paso 1 primero");
            }

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            var manzanas  = SelectionService.SelectManzanas(ed);
            var segmentos = SelectionService.SelectSegmentos(ed);

            if (manzanas.Count == 0)  { _rowAsociar.SetStatus(StepStatus.Warning); return new StepResult(false, "Sin entidades en capa MANZANA"); }
            if (segmentos.Count == 0) { _rowAsociar.SetStatus(StepStatus.Warning); return new StepResult(false, "Sin entidades en capa SEGMENTO"); }

            SelectionContext.Instance.SetManzanas(manzanas);
            SelectionContext.Instance.SetSegmentos(segmentos);

            // Lingas de acero (auto-selección por capa)
            SelectionService.LingaSelection lingas = SelectionService.SelectLingas(ed);

            int ok = 0, sin = 0;
            int pri = 0, sec = 0, sinLinga = 0;
            int warnSinManzana = 0, warnLingaCruzando = 0;

            var lingaPorPoste  = new Dictionary<ObjectId, string>();
            var frentePorPoste = new Dictionary<ObjectId, string>();
            var lingaIdByHex   = new Dictionary<string, ObjectId>();

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var idx       = new SpatialIndex(tr, manzanas);
                var asoc      = new PoleSegmentAssociator(idx, segmentos);
                var lingAssoc = new PoleLingaAssociator();
                foreach (ObjectId pid in polesIds)
                {
                    var out_ = asoc.AssociatePole(tr, pid);
                    var lo   = lingAssoc.AssociatePole(tr, pid, lingas.Prioridad, lingas.Secundaria);

                    string idFrente    = string.Empty;
                    double largoFrente = 0.0;
                    if (out_.Estado == AddressMatcher.OK
                        && !out_.ManzanaObjectId.IsNull
                        && out_.PointOnManzana.HasValue)
                    {
                        var mpl = tr.GetObject(out_.ManzanaObjectId, OpenMode.ForRead) as Polyline;
                        if (mpl != null)
                        {
                            var fo = FrenteManzanaCalculator.ComputeFrente(mpl, out_.PointOnManzana.Value);
                            if (fo.Found)
                            {
                                idFrente    = $"{mpl.Handle}#{fo.FrenteIndex}";
                                largoFrente = fo.Largo;
                            }
                        }
                    }
                    else
                    {
                        var ent = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            var pos = Extensions.GetInsertionOrPosition(ent);
                            AcadLogger.Warn(
                                $"Poste <H:{ent.Handle}> sin manzana asociada. " +
                                $"Pos: ({pos.X:F2}, {pos.Y:F2}). " +
                                $"CTO_ZOOM_HANDLE {ent.Handle}");
                        }
                        warnSinManzana++;
                    }

                    XDataManager.SetValues(tr, pid, new (string, object)[]
                    {
                        (XDataKeys.ID_SEGMENT,   out_.SegmentId     ?? string.Empty),
                        (XDataKeys.REVISAR,      out_.Estado        ?? AddressMatcher.SIN_SEGMENTO),
                        (XDataKeys.LARGO,        (object)out_.SegmentLength),
                        (XDataKeys.ID_LINGA,     lo.LingaHandleHex  ?? string.Empty),
                        (XDataKeys.LINGA_TIPO,   lo.LingaTipo       ?? string.Empty),
                        (XDataKeys.LARGO_LINGA,  (object)lo.LingaLargo),
                        (XDataKeys.ID_FRENTE,    idFrente),
                        (XDataKeys.LARGO_FRENTE, (object)largoFrente),
                    });
                    if (out_.Estado == AddressMatcher.OK) ok++; else sin++;

                    if      (lo.EncontradaPrioridad)  pri++;
                    else if (lo.EncontradaSecundaria) sec++;
                    else                              sinLinga++;

                    if (lo.EncontradaPrioridad && !string.IsNullOrEmpty(lo.LingaHandleHex))
                    {
                        lingaPorPoste[pid]  = lo.LingaHandleHex;
                        frentePorPoste[pid] = idFrente;
                        if (!lingaIdByHex.ContainsKey(lo.LingaHandleHex))
                            lingaIdByHex[lo.LingaHandleHex] = lo.LingaId;
                    }
                }

                warnLingaCruzando = Commands.AsociarPostesCommand.SanityCheckLingasEnDosFrentes(
                    tr, lingaPorPoste, frentePorPoste, lingaIdByHex);

                tr.Commit();
            }

            _rowAsociar.SetStatus(ok > 0 ? StepStatus.Ok : StepStatus.Warning);
            _rowAsociar.SetInfo($"OK={ok} PRI={pri} SEC={sec} ⚠P={warnSinManzana} ⚠L={warnLingaCruzando}");
            return new StepResult(ok > 0,
                $"Paso 2 — SEG: OK={ok} sin={sin} | LINGA: PRI={pri} SEC={sec} sin={sinLinga} | " +
                $"⚠ Postes sin manzana={warnSinManzana} ⚠ Lingas cruzando esquina={warnLingaCruzando} " +
                $"(Manzanas={manzanas.Count} Segmentos={segmentos.Count} Lingas={lingas.TotalCount})");
        }

        // ── Paso 3 ───────────────────────────────────────────────────────────

        private StepResult StepComentarios()
        {
            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                _rowComentarios.SetStatus(StepStatus.Warning);
                return new StepResult(false, "Ejecutá el paso 1 primero");
            }

            double radius = (double)_nudRadius.Value;
            AddinSettings.Current.TextBufferRadius = radius;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed  = doc.Editor;

            ObjectIdCollection segmentos = SelectionContext.Instance.Segmentos;
            if (segmentos == null || segmentos.Count == 0)
                segmentos = SelectionService.SelectSegmentos(ed);

            int conHp = 0, conCod = 0;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var allHpBlocks  = TextBufferCollector.LoadAllHpBlocks(tr, ed, segmentos);
                var hpPerSegment = TextBufferCollector.BuildHpPerSegment(allHpBlocks);

                var col = new TextBufferCollector(ed, radius);
                foreach (ObjectId pid in polesIds)
                {
                    int? hp = TextBufferCollector.GetHpForPole(tr, pid, hpPerSegment);
                    if (hp.HasValue) { XDataManager.SetInt(tr, pid, XDataKeys.HP, hp.Value); conHp++; }

                    var ent = tr.GetObject(pid, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    var cap = col.CollectObservaciones(tr, Extensions.GetInsertionOrPosition(ent));
                    XDataManager.SetString(tr, pid, XDataKeys.COMENTARIOS, cap.CommentsCsv);
                    if (cap.KnownCodes.Count > 0) conCod++;
                }
                tr.Commit();
            }

            string hpDetail = $"HP={conHp}/{polesIds.Length}";
            _rowComentarios.SetStatus(conHp > 0 ? StepStatus.Ok : StepStatus.Warning);
            _rowComentarios.SetInfo($"{hpDetail} Cod={conCod}");
            return new StepResult(conHp > 0,
                $"Paso 3 — {hpDetail} postes con HP, códigos en {conCod} (radio {radius}m)");
        }

        // ── Paso 4 ───────────────────────────────────────────────────────────

        private StepResult StepCalcular()
        {
            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                _rowCalcular.SetStatus(StepStatus.Warning);
                return new StepResult(false, "Ejecutá el paso 1 primero");
            }

            var doc   = AcApp.DocumentManager.MdiActiveDocument;
            var stats = new Commands.CtoDistributor.Stats();

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                Commands.CtoDistributor.DistributeAndStore(
                    tr, doc.Database, polesIds, stats, logInfo: null);
                tr.Commit();
            }

            string warn = stats.OutOfRange > 0 ? $" ⚠{stats.OutOfRange}HP>64" : string.Empty;
            _rowCalcular.SetStatus(StepStatus.Ok);
            _rowCalcular.SetInfo($"{stats.TotalCtos} CTOs / {stats.Segmentos} segmentos");
            return new StepResult(true,
                $"Paso 4 — {stats.Segmentos} segmentos, {stats.TotalCtos} CTOs (D/C intercalados){warn}");
        }

        // ── Paso 5 ───────────────────────────────────────────────────────────

        private StepResult StepDesplegar()
        {
            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                _rowDesplegar.SetStatus(StepStatus.Warning);
                return new StepResult(false, "Ejecutá el paso 1 primero");
            }

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var s   = AddinSettings.Current;
            int total = 0;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var dep    = new CtoBlockDeployer(s.BlockNameDesp, s.BlockNameCrec, s.CtoLayerName);
                int purged = dep.PurgeExistingBlocks(tr, doc.Database);
                foreach (ObjectId pid in polesIds)
                    total += dep.DeployForPole(tr, doc.Database, pid);
                tr.Commit();

                if (purged > 0)
                    AppendLog($"Purga previa: {purged} bloques en capa '{s.CtoLayerName}' borrados antes del deploy.", LogLevel.Info);
            }

            _rowDesplegar.SetStatus(StepStatus.Ok);
            _rowDesplegar.SetInfo($"{total} bloques");
            return new StepResult(true, $"Paso 5 — {total} bloques CTO insertados ✓");
        }

        // ── Log ──────────────────────────────────────────────────────────────

        private enum LogLevel { Info, Ok, Warn, Error }

        private void AppendLog(string msg, LogLevel level)
        {
            if (_log.InvokeRequired) { _log.Invoke(new Action<string, LogLevel>(AppendLog), msg, level); return; }

            Color c; string pfx;
            switch (level)
            {
                case LogLevel.Ok:    c = Color.FromArgb(0x00, 0xC8, 0x96); pfx = "✓ "; break;
                case LogLevel.Warn:  c = Color.FromArgb(0xFF, 0xB3, 0x47); pfx = "⚠ "; break;
                case LogLevel.Error: c = Color.FromArgb(0xFF, 0x55, 0x77); pfx = "✗ "; break;
                default:             c = FuturisticTheme.TextSecondary;     pfx = "  "; break;
            }
            _log.SelectionStart  = _log.TextLength;
            _log.SelectionColor  = FuturisticTheme.TextMuted;
            _log.AppendText($"{DateTime.Now:HH:mm:ss} ");
            _log.SelectionColor  = c;
            _log.AppendText($"{pfx}{msg}\n");
            _log.ScrollToCaret();

            try { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n[CTO] {msg}"); } catch { }
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
                if (_pulseTimer != null)
                {
                    _pulseTimer.Stop();
                    _pulseTimer.Dispose();
                    _pulseTimer = null;
                }
            }
            base.Dispose(disposing);
        }

        // ── StepRow ──────────────────────────────────────────────────────────

        private class StepRow : Panel
        {
            public FuturisticTheme.BtnFuturista Button { get; }
            private readonly DotIndicator _dot;
            private readonly Label        _info;

            public StepRow(string label)
            {
                Dock      = DockStyle.Fill;
                BackColor = FuturisticTheme.BgPanel;
                Margin    = new Padding(0);
                Height    = 44;

                _dot = new DotIndicator { Location = new Point(8, 18) };

                Button = new FuturisticTheme.BtnFuturista(FuturisticTheme.BtnStyle.Primary)
                {
                    Text     = label,
                    Size     = new Size(196, 28),
                    Location = new Point(24, 8),
                    Font     = new WinFont("Segoe UI", 8.5f, FontStyle.Bold),
                };

                _info = new Label
                {
                    Text      = "—",
                    ForeColor = FuturisticTheme.TextSecondary,
                    Size      = new Size(120, 28),
                    Location  = new Point(226, 8),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font      = new WinFont("Consolas", 7.5f),
                };

                Controls.Add(_dot);
                Controls.Add(Button);
                Controls.Add(_info);

                // Borde bottom
                Paint += (s, e) =>
                {
                    using (var pen = new Pen(FuturisticTheme.BorderSubtle))
                        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                };
            }

            public void SetStatus(StepStatus s)
            {
                _dot.Status = s;
                _dot.Invalidate();
            }

            public void SetInfo(string text) { _info.Text = text; _info.Refresh(); }

            public void PulseTick() { _dot.PulseTick(); }
        }

        // ── DotIndicator ─────────────────────────────────────────────────────

        private class DotIndicator : Control
        {
            public StepStatus Status { get; set; } = StepStatus.Pending;

            private int  _pulseAlpha    = 80;
            private bool _pulseDir      = true; // true = going up

            public DotIndicator()
            {
                Size          = new Size(8, 8);
                DoubleBuffered = true;
            }

            public void PulseTick()
            {
                if (Status != StepStatus.Running) return;

                if (_pulseDir)
                {
                    _pulseAlpha += 10;
                    if (_pulseAlpha >= 255) { _pulseAlpha = 255; _pulseDir = false; }
                }
                else
                {
                    _pulseAlpha -= 10;
                    if (_pulseAlpha <= 80) { _pulseAlpha = 80; _pulseDir = true; }
                }
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Color baseColor;
                switch (Status)
                {
                    case StepStatus.Running: baseColor = Color.FromArgb(_pulseAlpha, 0x00, 0xBF, 0xFF); break;
                    case StepStatus.Ok:      baseColor = Color.FromArgb(0x00, 0xC8, 0x96);               break;
                    case StepStatus.Warning: baseColor = Color.FromArgb(0xFF, 0xB3, 0x47);               break;
                    case StepStatus.Error:   baseColor = Color.FromArgb(0xFF, 0x55, 0x77);               break;
                    default:                 baseColor = Color.FromArgb(0x5A, 0x6B, 0x85);               break;
                }
                using (var b = new SolidBrush(baseColor))
                    e.Graphics.FillRectangle(b, new Rectangle(0, 0, Width, Height));
            }

            protected override void OnPaintBackground(PaintEventArgs e) { /* suppress */ }
        }

        private enum StepStatus { Pending, Running, Ok, Warning, Error }

        private class StepResult
        {
            public bool   Ok      { get; }
            public string Message { get; }
            public StepResult(bool ok, string msg) { Ok = ok; Message = msg; }
        }
    }
}
