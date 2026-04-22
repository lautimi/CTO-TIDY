using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Helpers para selección de entidades: auto-selección por capa y prompts manuales con filtros DXF.
    /// </summary>
    public static class SelectionService
    {
        // ── Filtros estáticos ────────────────────────────────────────────────

        private static readonly SelectionFilter PosteFilter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.Operator, "<or"),
            new TypedValue((int)DxfCode.Start, "INSERT"),
            new TypedValue((int)DxfCode.Start, "POINT"),
            new TypedValue((int)DxfCode.Operator, "or>"),
        });

        private static readonly SelectionFilter ObservacionesFilter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.LayerName, "OBSERVACIONES"),
        });

        // ── Auto-selección por capa ──────────────────────────────────────────

        /// <summary>
        /// Selecciona automáticamente todas las entidades visibles en la capa indicada.
        /// Devuelve colección vacía si no hay entidades.
        /// </summary>
        public static ObjectIdCollection SelectAllOnLayer(Editor ed, string layerName, string dxfEntityType = null)
        {
            var values = new List<TypedValue>
            {
                new TypedValue((int)DxfCode.LayerName, layerName)
            };
            if (!string.IsNullOrEmpty(dxfEntityType))
                values.Add(new TypedValue((int)DxfCode.Start, dxfEntityType));

            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(values.ToArray()));
            if (res.Status == PromptStatus.OK && res.Value != null)
                return new ObjectIdCollection(res.Value.GetObjectIds());

            return new ObjectIdCollection();
        }

        /// <summary>
        /// Selecciona todas las polilíneas cerradas de la capa MANZANA.
        /// </summary>
        public static ObjectIdCollection SelectManzanas(Editor ed)
        {
            // Las manzanas son LWPOLYLINE/POLYLINE en capa MANZANA
            var values = new[]
            {
                new TypedValue((int)DxfCode.LayerName, "MANZANA"),
            };
            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(values));
            if (res.Status == PromptStatus.OK && res.Value != null)
                return new ObjectIdCollection(res.Value.GetObjectIds());
            return new ObjectIdCollection();
        }

        /// <summary>
        /// Selecciona todas las líneas de la capa SEGMENTO.
        /// </summary>
        public static ObjectIdCollection SelectSegmentos(Editor ed)
        {
            var values = new[]
            {
                new TypedValue((int)DxfCode.LayerName, "SEGMENTO"),
            };
            PromptSelectionResult res = ed.SelectAll(new SelectionFilter(values));
            if (res.Status == PromptStatus.OK && res.Value != null)
                return new ObjectIdCollection(res.Value.GetObjectIds());
            return new ObjectIdCollection();
        }

        /// <summary>
        /// Resultado de selección de lingas — ya separadas por tipo según el layer.
        /// </summary>
        public class LingaSelection
        {
            public ObjectIdCollection Prioridad  = new ObjectIdCollection();
            public ObjectIdCollection Secundaria = new ObjectIdCollection();

            public int TotalCount => Prioridad.Count + Secundaria.Count;
        }

        /// <summary>
        /// Selecciona todas las líneas de las capas
        ///   "LINGA DE ACERO_PRIORIDAD"   → frente principal (donde van los CTOs)
        ///   "LINGA DE ACERO_SECUNDARIA" → frente secundario (sin CTOs)
        /// </summary>
        public static LingaSelection SelectLingas(Editor ed)
        {
            return new LingaSelection
            {
                Prioridad  = SelectAllOnLayer(ed, "LINGA DE ACERO_PRIORIDAD",  "LINE"),
                Secundaria = SelectAllOnLayer(ed, "LINGA DE ACERO_SECUNDARIA", "LINE"),
            };
        }

        // ── Prompts manuales (fallback) ──────────────────────────────────────

        public static ObjectId[] PromptForPostes(Editor ed)
        {
            var opts = new PromptSelectionOptions { MessageForAdding = "\nSelecciona los postes:" };
            PromptSelectionResult r = ed.GetSelection(opts, PosteFilter);
            return r.Status == PromptStatus.OK ? r.Value.GetObjectIds() : null;
        }

        public static SelectionFilter GetObservacionesFilter() => ObservacionesFilter;
    }
}
