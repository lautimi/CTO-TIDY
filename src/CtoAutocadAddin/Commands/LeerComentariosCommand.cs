using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Services;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class LeerComentariosCommand
    {
        [CommandMethod("CTO_LEER_COMENTARIOS", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                AcadLogger.Warn("No hay postes seleccionados. Ejecuta CTO_SELECCIONAR_POSTES primero.");
                return;
            }

            // Segmentos: los que quedaron en contexto del paso CTO_ASOCIAR_POSTES,
            // o los vuelve a cargar desde la capa SEGMENTO si no hay.
            ObjectIdCollection segmentos = SelectionContext.Instance.Segmentos;
            if (segmentos == null || segmentos.Count == 0)
                segmentos = SelectionService.SelectSegmentos(ed);

            var pdo = new PromptDoubleOptions(
                $"\nRadio buffer comentarios [m] <{AddinSettings.Current.TextBufferRadius}>: ")
            {
                AllowNegative = false,
                AllowZero     = false,
                DefaultValue  = AddinSettings.Current.TextBufferRadius,
                UseDefaultValue = true,
            };
            PromptDoubleResult rd = ed.GetDouble(pdo);
            if (rd.Status != PromptStatus.OK) return;
            double radius = rd.Value;
            AddinSettings.Current.TextBufferRadius = radius;

            int conHp = 0, conCod = 0;

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // ── 1. Cargar todos los CONT_HP y asociarlos a su segmento ──────
                List<TextBufferCollector.HpBlock> allHpBlocks =
                    TextBufferCollector.LoadAllHpBlocks(tr, ed, segmentos);

                // ── 2. Construir mapa  segmentHandle → HP_total ──────────────────
                Dictionary<string, int> hpPerSegment =
                    TextBufferCollector.BuildHpPerSegment(allHpBlocks);

                AcadLogger.Info($"CONT_HP encontrados: {allHpBlocks.Count}  " +
                                $"Segmentos con HP: {hpPerSegment.Count}");

                // ── 3. Por cada poste: asignar HP y capturar comentarios ──────────
                var collector = new TextBufferCollector(ed, radius);

                foreach (ObjectId poleId in polesIds)
                {
                    Entity ent = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    // HP: lookup por ID_SEGMENT del poste
                    int? hp = TextBufferCollector.GetHpForPole(tr, poleId, hpPerSegment);
                    if (hp.HasValue)
                    {
                        XDataManager.SetInt(tr, poleId, XDataKeys.HP, hp.Value);
                        conHp++;
                    }

                    // Comentarios: buffer circular alrededor del poste
                    var polePt = Extensions.GetInsertionOrPosition(ent);
                    TextBufferCollector.Capture cap = collector.CollectObservaciones(tr, polePt);
                    XDataManager.SetString(tr, poleId, XDataKeys.COMENTARIOS, cap.CommentsCsv);
                    if (cap.KnownCodes.Count > 0) conCod++;
                }

                tr.Commit();
            }

            AcadLogger.Info(
                $"HP asignado a {conHp} de {polesIds.Length} postes. " +
                $"Comentarios en {conCod} postes (radio {radius} m).");
        }
    }
}
