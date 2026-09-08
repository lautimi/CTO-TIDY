using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Services;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Paso opcional posterior al 5: reparte las propiedades servicio entre las cajas CTO
    /// desplegadas respetando el cupo de HP por caja, y dibuja el spider de acometida
    /// propiedad → caja.
    ///
    /// El reparto va en orden paramétrico sobre el eje de calle, no por cercanía: asignar
    /// por distancia cruza las acometidas de propiedades vecinas.
    /// </summary>
    public class SpidersAcometidaCommand
    {
        private class Axis
        {
            public Point3d  Origin;
            public Vector3d Dir;
        }

        [CommandMethod("CTO_SPIDERS_ACOMETIDA", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            AddinSettings settings = AddinSettings.Current;
            int capacity = settings.BoxCapacityHp > 0
                ? settings.BoxCapacityHp
                : CapacityAllocator.DEFAULT_CAPACITY;

            int dibujados = 0, excedidas = 0, sinCaja = 0;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // ── Precondición 1: existen cajas desplegadas ────────────────────────
                    var cajaIds = new List<ObjectId>();
                    AddRange(cajaIds, SelectionService.SelectAllOnLayer(ed, settings.CtoLayerNameDesp, "INSERT"));
                    AddRange(cajaIds, SelectionService.SelectAllOnLayer(ed, settings.CtoLayerNameCrec, "INSERT"));

                    if (cajaIds.Count == 0)
                    {
                        AcadLogger.Warn("No hay bloques de caja en el plano. Ejecutá el paso 5 (CTO_DESPLEGAR) primero.");
                        return;
                    }

                    var axisBySegment = new Dictionary<string, Axis>(StringComparer.OrdinalIgnoreCase);
                    var boxes         = new List<CapacityAllocator.BoxRef>();
                    var boxPosition   = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

                    foreach (ObjectId id in cajaIds)
                    {
                        var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        string seg = XDataManager.GetString(tr, id, XDataKeys.ID_SEGMENT);
                        if (string.IsNullOrEmpty(seg)) continue;

                        Axis axis = GetAxis(tr, db, seg, axisBySegment);
                        if (axis == null) continue;

                        string boxId = id.Handle.ToString();
                        boxPosition[boxId] = br.Position;
                        boxes.Add(new CapacityAllocator.BoxRef
                        {
                            Id        = boxId,
                            X         = br.Position.X,
                            Y         = br.Position.Y,
                            SegmentId = seg,
                            Param     = ParamOn(axis, br.Position),
                            Side      = SideOf(axis, br.Position),
                        });
                    }

                    // ── Precondición 2: las cajas saben a qué segmento pertenecen ────────
                    if (boxes.Count == 0)
                    {
                        AcadLogger.Warn($"Las {cajaIds.Count} cajas del plano no tienen ID_SEGMENT. " +
                                        "Volvé a ejecutar el paso 5 con esta versión del add-in para que queden marcadas.");
                        return;
                    }

                    // ── Precondición 3: las propiedades están asociadas ──────────────────
                    List<PropertyCollector.Property> collected = PropertyCollector.Collect(
                        tr, ed, settings.PropertyLayerName, settings.PropertyCountTags);

                    var props       = new List<CapacityAllocator.PropRef>();
                    var propObject  = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                    var propPoint   = new Dictionary<string, Point3d>(StringComparer.OrdinalIgnoreCase);

                    foreach (PropertyCollector.Property p in collected)
                    {
                        string seg = XDataManager.GetString(tr, p.Id, XDataKeys.ID_SEGMENT);
                        if (string.IsNullOrEmpty(seg)) continue;

                        Axis axis = GetAxis(tr, db, seg, axisBySegment);
                        if (axis == null) continue;

                        int? hp = XDataManager.GetInt(tr, p.Id, XDataKeys.VIVIENDAS);
                        string propId = p.Id.Handle.ToString();
                        propObject[propId] = p.Id;
                        propPoint[propId]  = p.Position;

                        props.Add(new CapacityAllocator.PropRef
                        {
                            Id        = propId,
                            Hp        = hp.HasValue && hp.Value > 0 ? hp.Value : p.Viviendas,
                            X         = p.Position.X,
                            Y         = p.Position.Y,
                            SegmentId = seg,
                            Param     = ParamOn(axis, p.Position),
                            Side      = SideOf(axis, p.Position),
                        });
                    }

                    if (props.Count == 0)
                    {
                        AcadLogger.Warn("Ninguna propiedad servicio tiene segmento asociado. " +
                                        "Ejecutá CTO_GENERAR_CONTEOS primero.");
                        return;
                    }

                    AcadLogger.Info($"Reparto: {props.Count} propiedades, {boxes.Count} cajas, cupo {capacity} HP/caja.");

                    // ── Reparto por capacidad (lógica pura en Core) ──────────────────────
                    CapacityAllocator.AllocationResult alloc =
                        CapacityAllocator.Allocate(props, boxes, capacity);

                    var excedidasSet = new HashSet<string>(alloc.OverCapacity, StringComparer.OrdinalIgnoreCase);
                    var lines = new List<SpiderDrawer.SpiderLine>();

                    foreach (CapacityAllocator.PropRef p in props)
                    {
                        string boxId;
                        if (!alloc.BoxByProperty.TryGetValue(p.Id, out boxId)) continue;

                        XDataManager.SetValues(tr, propObject[p.Id], new (string, object)[]
                        {
                            (XDataKeys.ID_CAJA, boxId),
                        });

                        lines.Add(new SpiderDrawer.SpiderLine
                        {
                            From    = propPoint[p.Id],
                            To      = boxPosition[boxId],
                            Flagged = excedidasSet.Contains(p.Id),
                        });
                    }

                    SpiderDrawer.PurgeLayer(tr, db, SpiderDrawer.LAYER_SPIDER_ACOMETIDA);
                    dibujados = SpiderDrawer.DrawLines(tr, db, SpiderDrawer.LAYER_SPIDER_ACOMETIDA,
                                                       SpiderDrawer.COLOR_ACOMETIDA, lines);
                    excedidas = alloc.OverCapacity.Count;
                    sinCaja   = alloc.Unassigned.Count;

                    ReportSegmentosSinCaja(props, alloc);

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                AcadLogger.Error($"CTO_SPIDERS_ACOMETIDA abortado sin escribir nada: {ex.Message}");
                return;
            }

            AcadLogger.Info($"Acometidas: {dibujados} spiders dibujados | " +
                            $"{excedidas} por encima del cupo | {sinCaja} sin caja en su segmento.");

            if (excedidas > 0)
                AcadLogger.Warn($"{excedidas} propiedad(es) exceden el cupo de su segmento — " +
                                "se dibujaron igual, en rojo.");
        }

        private static void ReportSegmentosSinCaja(
            List<CapacityAllocator.PropRef> props, CapacityAllocator.AllocationResult alloc)
        {
            if (alloc.Unassigned.Count == 0) return;

            var sinCaja = new HashSet<string>(alloc.Unassigned, StringComparer.OrdinalIgnoreCase);
            var segmentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CapacityAllocator.PropRef p in props)
                if (sinCaja.Contains(p.Id) && !string.IsNullOrEmpty(p.SegmentId))
                    segmentos.Add(p.SegmentId);

            foreach (string seg in segmentos)
                AcadLogger.Warn($"Segmento <H:{seg}> tiene propiedades pero ninguna caja desplegada. " +
                                $"CTO_ZOOM_HANDLE {seg}");
        }

        private static void AddRange(List<ObjectId> target, ObjectIdCollection source)
        {
            if (source == null) return;
            foreach (ObjectId id in source) target.Add(id);
        }

        private static Axis GetAxis(
            Transaction tr, Database db, string segHandleHex, Dictionary<string, Axis> cache)
        {
            Axis axis;
            if (cache.TryGetValue(segHandleHex, out axis)) return axis;

            cache[segHandleHex] = null;   // negativo cacheado: no reintentar por cada entidad

            long handleValue;
            if (!long.TryParse(segHandleHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out handleValue))
                return null;

            ObjectId segId;
            if (!db.TryGetObjectId(new Handle(handleValue), out segId)) return null;

            var curve = tr.GetObject(segId, OpenMode.ForRead) as Curve;
            if (curve == null) return null;

            Vector3d raw = curve.EndPoint - curve.StartPoint;
            if (raw.Length < 1e-6) return null;

            axis = new Axis { Origin = curve.StartPoint, Dir = raw.GetNormal() };
            cache[segHandleHex] = axis;
            return axis;
        }

        /// <summary>Posición proyectada sobre el eje de calle (metros desde el inicio).</summary>
        private static double ParamOn(Axis axis, Point3d p)
        {
            return axis.Dir.DotProduct(p - axis.Origin);
        }

        /// <summary>Vereda: signo del producto cruz respecto del eje. Mismo criterio que el deploy.</summary>
        private static int SideOf(Axis axis, Point3d p)
        {
            Vector3d to = p - axis.Origin;
            double cross = axis.Dir.X * to.Y - axis.Dir.Y * to.X;
            return cross >= 0 ? 1 : -1;
        }
    }
}
