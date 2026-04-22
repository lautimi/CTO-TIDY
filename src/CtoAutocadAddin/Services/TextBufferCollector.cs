using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Persistence;
using Koovra.Cto.Core;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Paso 3 del workflow — dos responsabilidades independientes:
    ///
    ///   a) HP  — asocia cada bloque CONT_HP al segmento de calle más cercano
    ///            (igual que los postes, el CONT_HP está en el medio de la cuadra).
    ///            Construye un mapa  segmentHandle → HP_total  que luego cada
    ///            poste consulta por su propio ID_SEGMENT.
    ///
    ///   b) Comentarios — captura MText de la capa OBSERVACIONES alrededor del POSTE.
    /// </summary>
    public class TextBufferCollector
    {
        // ── Constantes ───────────────────────────────────────────────────────

        private const string CapaConteoHp = "Conteo HP";
        private const string NombreBloque = "CONT_HP";
        private const string TagSdu       = "SDU";
        private const string TagMdu       = "MDU";

        /// <summary>Radio máximo (m) para asociar un CONT_HP a un segmento.</summary>
        public const double HpToSegmentMaxRadius = 100.0;

        // Filtro para MText/DBText de OBSERVACIONES
        private static readonly SelectionFilter ObservacionesFilter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.LayerName, "OBSERVACIONES"),
        });

        private static readonly Regex MTextTagsRegex = new Regex(
            @"\{\\[^}]*\}|\\[A-Za-z];?|%%.|\{|\}", RegexOptions.Compiled);

        private readonly Editor _editor;
        private readonly double _radius;

        // ── Tipos públicos ───────────────────────────────────────────────────

        /// <summary>
        /// Representa un bloque CONT_HP leído del DWG,
        /// ya asociado al segmento de calle más cercano.
        /// </summary>
        public class HpBlock
        {
            public Point3d Position;
            public int     Hp;           // SDU + MDU
            public string  SegmentId;    // Handle hex del segmento asociado (puede ser null si no encontró)
        }

        public class Capture
        {
            public List<string> RawTexts   = new List<string>();
            public List<string> KnownCodes = new List<string>();

            public string CommentsCsv => CommentParser.JoinCsv(KnownCodes);
        }

        public TextBufferCollector(Editor editor, double radius)
        {
            _editor = editor;
            _radius = radius;
        }

        // ── API pública — HP ─────────────────────────────────────────────────

        /// <summary>
        /// Carga todos los bloques CONT_HP del DWG y los asocia al segmento
        /// de calle más cercano dentro de <see cref="HpToSegmentMaxRadius"/>.
        ///
        /// Llamar UNA SOLA VEZ al inicio del paso 3, fuera del loop de postes.
        /// </summary>
        /// <param name="segmentIds">
        ///   Colección de ObjectId de los segmentos de calle (capa SEGMENTO).
        ///   Se usa para buscar cuál está más cerca de cada bloque CONT_HP.
        ///   Puede ser null o vacía: en ese caso no se asigna SegmentId y
        ///   el mapa HP resultante estará vacío.
        /// </param>
        public static List<HpBlock> LoadAllHpBlocks(
            Transaction        tr,
            Editor             ed,
            ObjectIdCollection segmentIds = null)
        {
            var result = new List<HpBlock>();

            // 1. Seleccionar todos los INSERT en capa "Conteo HP"
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start,     "INSERT"),
                new TypedValue((int)DxfCode.LayerName, CapaConteoHp),
            });

            PromptSelectionResult sel = ed.SelectAll(filter);
            if (sel.Status != PromptStatus.OK || sel.Value == null) return result;

            foreach (SelectedObject so in sel.Value)
            {
                if (so == null) continue;
                BlockReference br = tr.GetObject(so.ObjectId, OpenMode.ForRead) as BlockReference;
                if (br == null) continue;
                if (!IsBlockNamed(tr, br, NombreBloque)) continue;

                int sdu = 0, mdu = 0;
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    AttributeReference att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (att == null) continue;
                    string tag = att.Tag?.ToUpperInvariant();
                    int val = TryParseInt(att.TextString);
                    if (tag == TagSdu) sdu = val;
                    else if (tag == TagMdu) mdu = val;
                }

                // 2. Asociar este bloque al segmento más cercano
                string segId = null;
                if (segmentIds != null && segmentIds.Count > 0)
                    segId = FindNearestSegmentHandle(tr, br.Position, segmentIds);

                result.Add(new HpBlock
                {
                    Position  = br.Position,
                    Hp        = sdu + mdu,
                    SegmentId = segId,
                });
            }

            return result;
        }

        /// <summary>
        /// Construye un diccionario  segmentHandle → HP_total
        /// sumando todos los bloques CONT_HP asociados a cada segmento.
        /// </summary>
        public static Dictionary<string, int> BuildHpPerSegment(List<HpBlock> blocks)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (blocks == null) return map;

            foreach (HpBlock blk in blocks)
            {
                if (string.IsNullOrEmpty(blk.SegmentId)) continue;
                map.TryGetValue(blk.SegmentId, out int cur);
                map[blk.SegmentId] = cur + blk.Hp;
            }
            return map;
        }

        /// <summary>
        /// Devuelve el HP total del segmento asociado a un poste,
        /// usando el ID_SEGMENT almacenado en XData y el mapa pre-construido.
        /// Retorna null si el poste no tiene segmento asociado o el segmento
        /// no tiene ningún CONT_HP.
        /// </summary>
        public static int? GetHpForPole(
            Transaction            tr,
            ObjectId               poleId,
            Dictionary<string, int> hpPerSegment)
        {
            string segId = XDataManager.GetString(tr, poleId, XDataKeys.ID_SEGMENT);
            if (string.IsNullOrEmpty(segId)) return null;
            return hpPerSegment.TryGetValue(segId, out int hp) ? hp : (int?)null;
        }

        // ── API pública — Comentarios ────────────────────────────────────────

        /// <summary>
        /// Captura MText/DBText de la capa OBSERVACIONES alrededor del poste.
        /// (No lee HP — eso lo hace GetHpForPole con el mapa pre-construido.)
        /// </summary>
        public Capture CollectObservaciones(Transaction tr, Point3d polePoint)
        {
            var result = new Capture();

            Point3d min = new Point3d(polePoint.X - _radius, polePoint.Y - _radius, 0);
            Point3d max = new Point3d(polePoint.X + _radius, polePoint.Y + _radius, 0);

            PromptSelectionResult sel = _editor.SelectCrossingWindow(min, max, ObservacionesFilter);
            if (sel.Status != PromptStatus.OK || sel.Value == null) return result;

            foreach (SelectedObject so in sel.Value)
            {
                if (so == null) continue;
                Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (Extensions.GetInsertionOrPosition(ent).DistanceTo(polePoint) > _radius) continue;

                string raw = ExtractText(ent);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                result.RawTexts.Add(raw);
                foreach (string code in CommentParser.ExtractKnownCodes(raw))
                    if (!result.KnownCodes.Contains(code)) result.KnownCodes.Add(code);
            }

            return result;
        }

        // ── Helpers privados ─────────────────────────────────────────────────

        /// <summary>
        /// Encuentra el Handle (hex string) del segmento más cercano a <paramref name="point"/>
        /// dentro de <see cref="HpToSegmentMaxRadius"/> metros.
        /// Devuelve null si ninguno está dentro del radio.
        /// </summary>
        private static string FindNearestSegmentHandle(
            Transaction        tr,
            Point3d            point,
            ObjectIdCollection segmentIds)
        {
            double   bestDist = double.MaxValue;
            ObjectId bestId   = ObjectId.Null;

            foreach (ObjectId segId in segmentIds)
            {
                Curve seg = null;
                try { seg = tr.GetObject(segId, OpenMode.ForRead) as Curve; } catch { }
                if (seg == null) continue;

                // Distancia desde el punto al segmento (punto más cercano sobre la curva)
                Point3d closest;
                try   { closest = seg.GetClosestPointTo(point, false); }
                catch { continue; }

                double d = closest.DistanceTo(point);
                if (d < bestDist && d <= HpToSegmentMaxRadius)
                {
                    bestDist = d;
                    bestId   = segId;
                }
            }

            return bestId.IsNull ? null : bestId.Handle.ToString();
        }

        private static bool IsBlockNamed(Transaction tr, BlockReference br, string name)
        {
            try
            {
                var def = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                return def != null && string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string ExtractText(Entity ent)
        {
            if (ent is DBText t) return t.TextString?.Trim();
            if (ent is MText m)
            {
                string plain = m.Text;
                if (string.IsNullOrWhiteSpace(plain))
                    plain = MTextTagsRegex.Replace(m.Contents ?? string.Empty, string.Empty);
                return plain?.Trim();
            }
            return null;
        }

        private static int TryParseInt(string s)
        {
            return int.TryParse(s?.Trim(), out int v) ? v : 0;
        }
    }
}
