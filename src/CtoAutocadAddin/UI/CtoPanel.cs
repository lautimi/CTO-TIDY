// Aliases para resolver ambigüedad entre namespaces de AutoCAD y WinForms
using AcApp      = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFont    = System.Drawing.Font;
using WinFlow    = System.Windows.Forms.FlowDirection;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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
        private StepRow     _rowPostes, _rowAsociar, _rowComentarios, _rowCalcular, _rowDesplegar;
        private Button      _btnRunAll;
        private RichTextBox _log;
        private NumericUpDown _nudRadius;

        public CtoPanel()
        {
            BuildUI();
        }

        // ── Construcción de UI ───────────────────────────────────────────────

        private void BuildUI()
        {
            Text            = "CTO Workflow";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition   = FormStartPosition.Manual;
            Location        = new Point(100, 100);
            Size            = new Size(370, 570);
            MinimumSize     = new Size(330, 480);
            BackColor       = Color.FromArgb(45, 45, 48);
            ForeColor       = Color.White;
            Font            = new WinFont("Segoe UI", 9f);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                Padding     = new Padding(8),
                BackColor   = Color.Transparent,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddRow(int h) => layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            AddRow(34);  // título
            AddRow(40);  // paso 1
            AddRow(40);  // paso 2
            AddRow(40);  // paso 3
            AddRow(40);  // paso 4
            AddRow(40);  // paso 5
            AddRow(34);  // radio
            AddRow(32);  // inspeccionar (diagnóstico)
            AddRow(42);  // run all
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // log

            // Título
            var title = new Label
            {
                Text      = "⚡  CTO — Workflow FTTH",
                Dock      = DockStyle.Fill,
                ForeColor = Color.FromArgb(0, 188, 212),
                Font      = new WinFont("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            layout.Controls.Add(title, 0, 0);

            // Pasos
            _rowPostes      = new StepRow("1. Seleccionar postes",    Color.FromArgb(33,150,243));
            _rowAsociar     = new StepRow("2. Asociar postes",        Color.FromArgb(33,150,243));
            _rowComentarios = new StepRow("3. Leer comentarios (HP)", Color.FromArgb(33,150,243));
            _rowCalcular    = new StepRow("4. Calcular CTOs",         Color.FromArgb(33,150,243));
            _rowDesplegar   = new StepRow("5. Desplegar CTOs",        Color.FromArgb(33,150,243));

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

            // Radio buffer
            var radioRow = new FlowLayoutPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                FlowDirection = WinFlow.LeftToRight,
            };
            radioRow.Controls.Add(new Label
            {
                Text      = "Radio buffer comentarios (m):",
                ForeColor = Color.Silver,
                AutoSize  = true,
                Margin    = new Padding(0, 7, 6, 0),
            });
            _nudRadius = new NumericUpDown
            {
                Minimum       = 1,
                Maximum       = 50,
                Value         = (decimal)AddinSettings.Current.TextBufferRadius,
                DecimalPlaces = 1,
                Increment     = 0.5m,
                Width         = 65,
                BackColor     = Color.FromArgb(60, 60, 65),
                ForeColor     = Color.White,
            };
            radioRow.Controls.Add(_nudRadius);
            layout.Controls.Add(radioRow, 0, 6);

            // Inspeccionar poste (diagnóstico)
            var btnInspect = new Button
            {
                Text      = "🔍  Inspeccionar poste (diagnóstico)",
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(55, 55, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new WinFont("Segoe UI", 8.5f),
                Margin    = new Padding(0, 2, 0, 2),
            };
            btnInspect.FlatAppearance.BorderSize = 0;
            btnInspect.Click += (s, e) =>
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                // SendStringToExecute: forma oficial de invocar un comando desde UI thread.
                // El espacio final cierra el nombre del comando; el comando se ocupa del resto.
                doc?.SendStringToExecute("CTO_INSPECCIONAR ", true, false, false);
            };
            layout.Controls.Add(btnInspect, 0, 7);

            // Ejecutar Todo
            _btnRunAll = new Button
            {
                Text      = "▶   EJECUTAR TODO  (pasos 1 → 5)",
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new WinFont("Segoe UI", 9f, FontStyle.Bold),
                Margin    = new Padding(0, 4, 0, 4),
            };
            _btnRunAll.FlatAppearance.BorderSize = 0;
            _btnRunAll.Click += (s, e) => RunAll();
            layout.Controls.Add(_btnRunAll, 0, 8);

            // Log
            _log = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.FromArgb(28, 28, 28),
                ForeColor   = Color.FromArgb(200, 200, 200),
                Font        = new WinFont("Consolas", 8f),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
            };
            layout.Controls.Add(_log, 0, 9);

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
            var ids = SelectionService.SelectAllOnLayer(ed, "POSTE_*", "INSERT");

            if (ids.Count == 0)
            {
                _rowPostes.SetStatus(StepStatus.Warning);
                _rowPostes.SetInfo("0");
                return new StepResult(false, "No se encontraron bloques en capas POSTE_*");
            }

            var arr = new ObjectId[ids.Count];
            ids.CopyTo(arr, 0);
            SelectionContext.Instance.SetPostes(arr);
            SelectionContext.Instance.ClearGeometry();

            _rowPostes.SetStatus(StepStatus.Ok);
            _rowPostes.SetInfo($"{ids.Count} postes");
            return new StepResult(true, $"Paso 1 OK — {ids.Count} postes");
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

                    // Frente de manzana: el LARGO real para la tabla CTO
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

            // Segmentos: reutilizar del contexto o recargar desde capa SEGMENTO
            ObjectIdCollection segmentos = SelectionContext.Instance.Segmentos;
            if (segmentos == null || segmentos.Count == 0)
                segmentos = SelectionService.SelectSegmentos(ed);

            int conHp = 0, conCod = 0;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                // 1. Cargar todos los CONT_HP y asociarlos a su segmento
                var allHpBlocks = TextBufferCollector.LoadAllHpBlocks(tr, ed, segmentos);

                // 2. Mapa  segmentHandle → HP_total
                var hpPerSegment = TextBufferCollector.BuildHpPerSegment(allHpBlocks);

                // 3. Por cada poste: HP por ID_SEGMENT + comentarios por buffer
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

            var doc = AcApp.DocumentManager.MdiActiveDocument;
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
                var dep = new CtoBlockDeployer(s.BlockNameDesp, s.BlockNameCrec, s.CtoLayerName);
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
                case LogLevel.Ok:    c = Color.FromArgb(102,187,106); pfx = "✓ "; break;
                case LogLevel.Warn:  c = Color.FromArgb(255,183, 77); pfx = "⚠ "; break;
                case LogLevel.Error: c = Color.FromArgb(239, 83, 80); pfx = "✗ "; break;
                default:             c = Color.FromArgb(160,160,160); pfx = "  "; break;
            }
            _log.SelectionStart  = _log.TextLength;
            _log.SelectionColor  = Color.FromArgb(90, 90, 90);
            _log.AppendText($"{DateTime.Now:HH:mm:ss} ");
            _log.SelectionColor  = c;
            _log.AppendText($"{pfx}{msg}\n");
            _log.ScrollToCaret();

            try { AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n[CTO] {msg}"); } catch { }
        }

        // ── StepRow ──────────────────────────────────────────────────────────

        private class StepRow : Panel
        {
            public Button Button { get; }
            private readonly Label _dot;
            private readonly Label _info;

            public StepRow(string label, Color accent)
            {
                Dock      = DockStyle.Fill;
                BackColor = Color.Transparent;
                Margin    = new Padding(0, 2, 0, 2);
                Height    = 36;

                _dot = new Label
                {
                    Text      = "●",
                    ForeColor = Color.FromArgb(70, 70, 70),
                    Size      = new Size(18, 34),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new WinFont("Segoe UI", 11f),
                };

                Button = new Button
                {
                    Text      = label,
                    Size      = new Size(196, 30),
                    BackColor = accent,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new WinFont("Segoe UI", 8.5f),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding   = new Padding(4, 0, 0, 0),
                };
                Button.FlatAppearance.BorderSize = 0;

                _info = new Label
                {
                    Text      = "—",
                    ForeColor = Color.Silver,
                    Size      = new Size(110, 30),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font      = new WinFont("Consolas", 7.5f),
                };

                var flow = new FlowLayoutPanel
                {
                    Dock          = DockStyle.Fill,
                    BackColor     = Color.Transparent,
                    FlowDirection = WinFlow.LeftToRight,
                };
                flow.Controls.Add(_dot);
                flow.Controls.Add(Button);
                flow.Controls.Add(_info);
                Controls.Add(flow);
            }

            public void SetStatus(StepStatus s)
            {
                _dot.ForeColor = s == StepStatus.Ok      ? Color.FromArgb(102,187,106)
                               : s == StepStatus.Running ? Color.FromArgb(255,183, 77)
                               : s == StepStatus.Warning ? Color.FromArgb(255,112, 67)
                               : s == StepStatus.Error   ? Color.FromArgb(239, 83, 80)
                               :                           Color.FromArgb(70,  70, 70);
                _dot.Refresh();
            }

            public void SetInfo(string text) { _info.Text = text; _info.Refresh(); }
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
