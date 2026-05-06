using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;
using Koovra.Cto.AutocadAddin.Persistence;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Paso 2 complementario: asocia cada poste a la LINGA DE ACERO más cercana.
    ///
    /// Cada linga es una Line entity en una de dos capas:
    ///   - "LINGA DE ACERO_PRIORIDAD"  → frente principal (recibe CTOs)
    ///   - "LINGA DE ACERO_SECUNDARIA" → frente secundario (NO recibe CTOs)
    ///
    /// A diferencia del PoleSegmentAssociator (que hace raycast ortogonal sobre
    /// manzanas cerradas), acá la linga ya es una línea simple; el cierre más
    /// cercano se resuelve directo con Line.GetClosestPointTo.
    /// </summary>
    public class PoleLingaAssociator
    {
        public class Outcome
        {
            public ObjectId LingaId       = ObjectId.Null;
            public string   LingaHandleHex = string.Empty;
            public string   LingaTipo      = string.Empty;  // PRIORIDAD / SECUNDARIA / ""
            public double   LingaLargo;
            public double   Distancia     = double.MaxValue;

            public bool EncontradaPrioridad  => LingaTipo == XDataKeys.LINGA_PRIORIDAD;
            public bool EncontradaSecundaria => LingaTipo == XDataKeys.LINGA_SECUNDARIA;
            public bool Encontrada           => LingaId != ObjectId.Null;
        }

        private readonly double _maxRadius;

        public PoleLingaAssociator(double maxRadius = 1.0)
        {
            _maxRadius = maxRadius;
        }

        /// <summary>
        /// Asocia un poste a la linga más cercana dentro de maxRadius.
        /// Retorna Outcome vacío (Encontrada=false) si ninguna está dentro del radio.
        /// </summary>
        public Outcome AssociatePole(
            Transaction        tr,
            ObjectId           poleId,
            ObjectIdCollection lingasPrioridad,
            ObjectIdCollection lingasSecundaria)
        {
            Entity poleEnt = tr.GetObject(poleId, OpenMode.ForRead) as Entity;
            if (poleEnt == null) return new Outcome();
            Point3d polePt = Extensions.GetInsertionOrPosition(poleEnt);

            var best = new Outcome();

            ScanCollection(tr, polePt, lingasPrioridad,  XDataKeys.LINGA_PRIORIDAD,  best);
            ScanCollection(tr, polePt, lingasSecundaria, XDataKeys.LINGA_SECUNDARIA, best);

            // Si la mejor distancia supera el radio → no se considera encontrada
            if (best.Distancia > _maxRadius) return new Outcome();
            return best;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void ScanCollection(
            Transaction        tr,
            Point3d            polePt,
            ObjectIdCollection lingas,
            string             tipo,
            Outcome            best)
        {
            if (lingas == null) return;

            foreach (ObjectId id in lingas)
            {
                Line line = null;
                try { line = tr.GetObject(id, OpenMode.ForRead) as Line; } catch { }
                if (line == null) continue;

                Point3d cp;
                try { cp = line.GetClosestPointTo(polePt, false); }
                catch { continue; }

                double d = cp.DistanceTo(polePt);
                if (d >= best.Distancia) continue;

                best.Distancia      = d;
                best.LingaId        = id;
                best.LingaHandleHex = line.Handle.ToString();
                best.LingaTipo      = tipo;
                best.LingaLargo     = (line.EndPoint - line.StartPoint).Length;
            }
        }
    }
}
