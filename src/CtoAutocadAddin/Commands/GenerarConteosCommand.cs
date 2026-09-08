using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.AutocadAddin.Services;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Paso opcional previo al 3: asocia cada propiedad servicio a su segmento de calle
    /// (manzana más cercana → normal → raycast), suma las viviendas por segmento e inserta
    /// el CONT_HP que el paso 3 ya sabe leer. Dibuja además los spiders de trazabilidad.
    ///
    /// No forma parte de "Ejecutar Todo": un plano que ya trae los conteos relevados a mano
    /// no necesita este paso.
    /// </summary>
    public class GenerarConteosCommand
    {
        [CommandMethod("CTO_GENERAR_CONTEOS", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            AddinSettings settings = AddinSettings.Current;

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

            int sinSegmento = 0, generados = 0, omitidosManual = 0, spiders = 0;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<PropertyCollector.Property> props = PropertyCollector.Collect(
                        tr, ed, settings.PropertyLayerName, settings.PropertyCountTags);

                    if (props.Count == 0)
                    {
                        AcadLogger.Warn($"No se encontraron bloques en la capa '{settings.PropertyLayerName}'. " +
                                        "Verificá el nombre de capa en CTO_CONFIG.");
                        return;
                    }

                    AcadLogger.Info($"Propiedades servicio: {props.Count} | Manzanas: {manzanas.Count} | Segmentos: {segmentos.Count}");

                    // ── 1. Asociar cada propiedad a su segmento (manzana → normal → raycast) ──
                    var index      = new SpatialIndex(tr, manzanas);
                    var associator = new PoleSegmentAssociator(index, segmentos);

                    foreach (PropertyCollector.Property p in props)
                    {
                        PoleSegmentAssociator.Outcome o = associator.AssociatePole(tr, p.Id);
                        if (o.Estado == AddressMatcher.OK && !string.IsNullOrEmpty(o.SegmentId))
                        {
                            p.SegmentId       = o.SegmentId;
                            p.SegmentObjectId = o.SegmentObjectId;
                        }
                        else
                        {
                            sinSegmento++;
                        }

                        XDataManager.SetValues(tr, p.Id, new (string, object)[]
                        {
                            (XDataKeys.ID_SEGMENT, p.SegmentId ?? string.Empty),
                            (XDataKeys.VIVIENDAS,  p.Viviendas),
                        });
                    }

                    // ── 2. Purgar los CONT_HP generados antes y detectar los manuales ────────
                    var generator = new HpBlockGenerator();
                    int purgados  = generator.PurgeGenerated(tr, db);
                    HashSet<string> manuales = generator.FindManualSegments(tr, ed, segmentos);

                    AcadLogger.Info($"CONT_HP: {purgados} generados previos purgados | " +
                                    $"{manuales.Count} segmento(s) con conteo manual (se respetan).");

                    // ── 3. Agrupar por segmento y generar ────────────────────────────────────
                    var porSegmento = new Dictionary<string, List<PropertyCollector.Property>>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (PropertyCollector.Property p in props)
                    {
                        if (string.IsNullOrEmpty(p.SegmentId)) continue;
                        List<PropertyCollector.Property> list;
                        if (!porSegmento.TryGetValue(p.SegmentId, out list))
                            porSegmento[p.SegmentId] = list = new List<PropertyCollector.Property>();
                        list.Add(p);
                    }

                    var lines = new List<SpiderDrawer.SpiderLine>();

                    foreach (var kv in porSegmento)
                    {
                        List<PropertyCollector.Property> segProps = kv.Value;

                        int suma = 0;
                        foreach (PropertyCollector.Property p in segProps) suma += p.Viviendas;
                        if (suma <= 0) continue;

                        Curve segCurve = tr.GetObject(segProps[0].SegmentObjectId, OpenMode.ForRead) as Curve;
                        if (segCurve == null) continue;

                        Point3d mid = Midpoint(segCurve);

                        // Spiders de trazabilidad: se dibujan aunque el conteo sea manual.
                        foreach (PropertyCollector.Property p in segProps)
                            lines.Add(new SpiderDrawer.SpiderLine { From = mid, To = p.Position });

                        if (manuales.Contains(kv.Key))
                        {
                            omitidosManual++;
                            AcadLogger.Info($"  Segmento <H:{kv.Key}> ya tiene CONT_HP manual — " +
                                            $"no se genera (habría sumado {suma} HP).");
                            continue;
                        }

                        double rot = CtoBlockDeployer.ComputeReadableAngle(
                            segCurve.EndPoint - segCurve.StartPoint);

                        generator.Insert(tr, db, mid, rot, suma, kv.Key);
                        generados++;
                    }

                    // ── 4. Spiders segmento → propiedad ──────────────────────────────────────
                    SpiderDrawer.PurgeLayer(tr, db, SpiderDrawer.LAYER_SPIDER_CONTEO);
                    spiders = SpiderDrawer.DrawLines(tr, db, SpiderDrawer.LAYER_SPIDER_CONTEO,
                                                     SpiderDrawer.COLOR_CONTEO, lines);

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                AcadLogger.Error($"CTO_GENERAR_CONTEOS abortado sin escribir nada: {ex.Message}");
                return;
            }

            AcadLogger.Info($"Conteos generados: {generados} CONT_HP insertados | " +
                            $"{omitidosManual} omitidos por conteo manual | " +
                            $"{spiders} spiders | {sinSegmento} propiedad(es) sin segmento.");

            if (sinSegmento > 0)
                AcadLogger.Warn($"{sinSegmento} propiedad(es) no se pudieron asociar a ningún segmento " +
                                "(sin línea de vista ortogonal desde la manzana).");
        }

        /// <summary>
        /// Punto medio por longitud de arco. En una polilínea en L el midpoint de cuerda cae
        /// fuera de la curva, y el paso 3 asocia el CONT_HP por curva más cercana: quedaría
        /// atribuido al segmento equivocado.
        /// </summary>
        private static Point3d Midpoint(Curve c)
        {
            try
            {
                double len = c.GetDistanceAtParameter(c.EndParam);
                if (len > 1e-6) return c.GetPointAtDist(len * 0.5);
            }
            catch { }

            return c.StartPoint + (c.EndPoint - c.StartPoint) * 0.5;
        }
    }
}
