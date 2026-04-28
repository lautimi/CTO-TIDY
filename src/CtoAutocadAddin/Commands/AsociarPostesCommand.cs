using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Map;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Services;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class AsociarPostesCommand
    {
        [CommandMethod("CTO_ASOCIAR_POSTES", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                AcadLogger.Warn("No hay postes seleccionados. Ejecuta CTO_SELECCIONAR_POSTES primero.");
                return;
            }

            // ── Auto-selección por capa ──────────────────────────────────────
            // Las manzanas (capa MANZANA) y segmentos (capa SEGMENTO) se seleccionan
            // automáticamente para no requerir selección manual del operador.

            ObjectIdCollection manzanas = SelectionContext.Instance.Manzanas;
            if (manzanas == null || manzanas.Count == 0)
            {
                manzanas = SelectionService.SelectManzanas(ed);
                if (manzanas.Count == 0)
                {
                    AcadLogger.Warn("No se encontraron entidades en la capa MANZANA. Verificar que la capa esté visible.");
                    return;
                }
                SelectionContext.Instance.SetManzanas(manzanas);
            }

            ObjectIdCollection segmentos = SelectionContext.Instance.Segmentos;
            if (segmentos == null || segmentos.Count == 0)
            {
                segmentos = SelectionService.SelectSegmentos(ed);
                if (segmentos.Count == 0)
                {
                    AcadLogger.Warn("No se encontraron entidades en la capa SEGMENTO. Verificar que la capa esté visible.");
                    return;
                }
                SelectionContext.Instance.SetSegmentos(segmentos);
            }

            // Lingas de acero (auto-selección por capa)
            SelectionService.LingaSelection lingas = SelectionService.SelectLingas(ed);

            AcadLogger.Info(
                $"Manzanas: {manzanas.Count} | Segmentos: {segmentos.Count} | " +
                $"Lingas PRI: {lingas.Prioridad.Count} SEC: {lingas.Secundaria.Count} | " +
                $"Postes: {polesIds.Length}");

            // Lectura OD fuera de la transacción de procesamiento (sin DB open)
            Dictionary<ObjectId, string> calleByOid;
            if (CtoCache.IsInitialized && CtoCache.CalleByOid != null)
            {
                calleByOid = CtoCache.CalleByOid;
                AcadLogger.Info($"CALLE_1 (cache): {calleByOid.Count}/{segmentos.Count}");
            }
            else
            {
                calleByOid = ObjectDataReader.ReadCalle1Bulk(segmentos);
                AcadLogger.Info($"CALLE_1 leído: {calleByOid.Count}/{segmentos.Count} segmentos con nombre.");
            }

            int ok = 0, sinSegmento = 0;
            int pri = 0, sec = 0, sinLinga = 0;
            int warnSinManzana = 0;
            int cntV4 = 0, cntV3Proj = 0, cntV2 = 0, cntFrenteNotFound = 0;

            // Cache para el sanity check posterior: poste → (idLinga, idFrente, lingaObjectId)
            var lingaPorPoste  = new Dictionary<ObjectId, string>();
            var frentePorPoste = new Dictionary<ObjectId, string>();
            var lingaIdByHex   = new Dictionary<string, ObjectId>();

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var index       = new SpatialIndex(tr, manzanas);
                var associator  = new PoleSegmentAssociator(index, segmentos);
                var lingAssoc   = new PoleLingaAssociator();

                StreetCornerLibrary cornerLib;
                if (CtoCache.IsInitialized && CtoCache.CornerLib != null)
                {
                    cornerLib = CtoCache.CornerLib;
                    AcadLogger.Info($"Esquinas (cache): {cornerLib.CornerCount} en {cornerLib.StreetCount} calles.");
                }
                else
                {
                    cornerLib = StreetCornerLibrary.Build(tr, calleByOid);
                    AcadLogger.Info($"Esquinas (build): {cornerLib.CornerCount} en {cornerLib.StreetCount} calles.");
                }

                var pm = new ProgressMeter();
                pm.Start($"Asociando {polesIds.Length} postes...");
                pm.SetLimit(polesIds.Length);

                foreach (ObjectId poleId in polesIds)
                {
                    // 1. Asociación al SEGMENTO (para HP y fallback)
                    PoleSegmentAssociator.Outcome outcome = associator.AssociatePole(tr, poleId);

                    // 2. Asociación a LINGA (para agrupamiento de CTOs y rotación)
                    PoleLingaAssociator.Outcome lo = lingAssoc.AssociatePole(
                        tr, poleId, lingas.Prioridad, lingas.Secundaria);

                    // 3. Cálculo del FRENTE DE MANZANA (el LARGO real para la tabla CTO)
                    string idFrente    = string.Empty;
                    double largoFrente = 0.0;
                    if (outcome.Estado == AddressMatcher.OK
                        && !outcome.ManzanaObjectId.IsNull
                        && outcome.PointOnManzana.HasValue)
                    {
                        var manzanaPl = tr.GetObject(outcome.ManzanaObjectId, OpenMode.ForRead) as Polyline;
                        if (manzanaPl != null)
                        {
                            Curve segCurve = null;
                            if (!outcome.SegmentObjectId.IsNull)
                                segCurve = tr.GetObject(outcome.SegmentObjectId, OpenMode.ForRead) as Curve;

                            string calleSegmento = null;
                            if (!outcome.SegmentObjectId.IsNull)
                                calleByOid.TryGetValue(outcome.SegmentObjectId, out calleSegmento);

                            FrenteMethod frenteMethod;
                            var fo = FrenteManzanaCalculator.ComputeFrente(
                                manzanaPl, outcome.PointOnManzana.Value, segCurve,
                                cornerLib, calleSegmento, out frenteMethod);
                            if (fo.Found)
                            {
                                // ID_FRENTE: manzanaHandle#segmentHandle para V4/V3_Projection.
                                // Para V2_DetectCorners usamos el frenteIndex legacy (estabilidad).
                                if (frenteMethod == FrenteMethod.V2_DetectCorners)
                                    idFrente = $"{manzanaPl.Handle}#{fo.FrenteIndex}";
                                else
                                {
                                    string segSuffix = outcome.SegmentId ?? "0";
                                    idFrente = $"{manzanaPl.Handle}#{segSuffix}";
                                }
                                largoFrente = fo.Largo;

                                // Auditoría visual: sub-capa según método
                                if (fo.CornerA.HasValue && fo.CornerB.HasValue)
                                {
                                    bool isV4 = frenteMethod == FrenteMethod.V4_StreetCorners;
                                    string auditLayer = isV4 ? "CTO_AUDIT_FRENTES_V4" : "CTO_AUDIT_FRENTES_V3";
                                    short  auditColor = isV4 ? (short)3 : (short)6;

                                    EnsureAuditLayer(tr, db, auditLayer, auditColor);

                                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                                    void AddAudit(Entity e)
                                    {
                                        e.Layer = auditLayer;
                                        e.ColorIndex = auditColor;
                                        ms.AppendEntity(e);
                                        tr.AddNewlyCreatedDBObject(e, true);
                                    }

                                    AddAudit(new Circle(fo.CornerA.Value, Vector3d.ZAxis, 1.0));
                                    AddAudit(new Circle(fo.CornerB.Value, Vector3d.ZAxis, 1.0));

                                    if (fo.ProjA.HasValue && fo.ProjB.HasValue)
                                    {
                                        AddAudit(new Circle(fo.ProjA.Value, Vector3d.ZAxis, 0.5));
                                        AddAudit(new Circle(fo.ProjB.Value, Vector3d.ZAxis, 0.5));
                                        AddAudit(new Line(fo.CornerA.Value, fo.ProjA.Value));
                                        AddAudit(new Line(fo.CornerB.Value, fo.ProjB.Value));
                                    }
                                }

                                // Contadores por método
                                switch (frenteMethod)
                                {
                                    case FrenteMethod.V4_StreetCorners: cntV4++;      break;
                                    case FrenteMethod.V3_Projection:    cntV3Proj++;  break;
                                    case FrenteMethod.V2_DetectCorners: cntV2++;      break;
                                    default:                            cntFrenteNotFound++; break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Poste sin manzana → warning navegable
                        Entity ent = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            Point3d pos = Extensions.GetInsertionOrPosition(ent);
                            AcadLogger.Warn(
                                $"Poste <H:{ent.Handle}> sin manzana asociada. " +
                                $"Pos: ({pos.X:F2}, {pos.Y:F2}). " +
                                $"CTO_ZOOM_HANDLE {ent.Handle}");
                        }
                        warnSinManzana++;
                    }

                    XDataManager.SetValues(tr, poleId, new (string, object)[]
                    {
                        (XDataKeys.ID_SEGMENT,   outcome.SegmentId ?? string.Empty),
                        (XDataKeys.REVISAR,      outcome.Estado    ?? AddressMatcher.SIN_SEGMENTO),
                        (XDataKeys.LARGO,        (object)outcome.SegmentLength),
                        (XDataKeys.ID_LINGA,     lo.LingaHandleHex ?? string.Empty),
                        (XDataKeys.LINGA_TIPO,   lo.LingaTipo      ?? string.Empty),
                        (XDataKeys.LARGO_LINGA,  (object)lo.LingaLargo),
                        (XDataKeys.ID_FRENTE,    idFrente),
                        (XDataKeys.LARGO_FRENTE, (object)largoFrente),
                    });

                    if (outcome.Estado == AddressMatcher.OK) ok++; else sinSegmento++;

                    if      (lo.EncontradaPrioridad)  pri++;
                    else if (lo.EncontradaSecundaria) sec++;
                    else                              sinLinga++;

                    // Cache para sanity check (solo PRIORIDAD nos interesa)
                    if (lo.EncontradaPrioridad && !string.IsNullOrEmpty(lo.LingaHandleHex))
                    {
                        lingaPorPoste[poleId]  = lo.LingaHandleHex;
                        frentePorPoste[poleId] = idFrente;
                        if (!lingaIdByHex.ContainsKey(lo.LingaHandleHex))
                            lingaIdByHex[lo.LingaHandleHex] = lo.LingaId;
                    }

                    pm.MeterProgress();
                }

                pm.Stop();

                // ── Sanity check: ¿alguna linga PRIORIDAD cruza esquina? ─────
                int warnLingaCruzando = SanityCheckLingasEnDosFrentes(
                    tr, lingaPorPoste, frentePorPoste, lingaIdByHex);

                tr.Commit();

                AcadLogger.Info(
                    $"Asociación completa. SEG: OK={ok} sin={sinSegmento} | " +
                    $"FRENTE: V4={cntV4} V3={cntV3Proj} V2={cntV2} noEnc={cntFrenteNotFound} | " +
                    $"LINGA: PRI={pri} SEC={sec} sin={sinLinga} | " +
                    $"⚠Postes sin manzana={warnSinManzana} ⚠Lingas cruzando esquina={warnLingaCruzando}");
            }
        }

        /// <summary>
        /// Verifica que todos los postes de una misma linga PRIORIDAD caigan al mismo
        /// frente de manzana. Si no, loggea un warning navegable por cada linga problemática.
        /// Retorna la cantidad de lingas con inconsistencia.
        /// </summary>
        internal static int SanityCheckLingasEnDosFrentes(
            Transaction tr,
            Dictionary<ObjectId, string> lingaPorPoste,
            Dictionary<ObjectId, string> frentePorPoste,
            Dictionary<string, ObjectId> lingaIdByHex)
        {
            int warn = 0;
            var grupos = lingaPorPoste
                .GroupBy(kv => kv.Value)
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var g in grupos)
            {
                var frentes = g.Select(kv => frentePorPoste.TryGetValue(kv.Key, out var f) ? f : string.Empty)
                               .Where(f => !string.IsNullOrEmpty(f))
                               .Distinct()
                               .ToList();
                if (frentes.Count <= 1) continue;

                if (!lingaIdByHex.TryGetValue(g.Key, out ObjectId lingaOid) || lingaOid.IsNull) continue;
                Line lingaLine = tr.GetObject(lingaOid, OpenMode.ForRead) as Line;
                if (lingaLine == null) continue;

                Point3d mid = lingaLine.StartPoint + (lingaLine.EndPoint - lingaLine.StartPoint) * 0.5;
                AcadLogger.Warn(
                    $"Linga <H:{lingaLine.Handle}> cruza esquina ({frentes.Count} frentes distintos). " +
                    $"Mid: ({mid.X:F2}, {mid.Y:F2}). " +
                    $"CTO_ZOOM_HANDLE {lingaLine.Handle}");
                warn++;
            }
            return warn;
        }

        private static void EnsureAuditLayer(Transaction tr, Database db, string name, short colorIdx)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name  = name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx),
            };
            lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }
    }
}
