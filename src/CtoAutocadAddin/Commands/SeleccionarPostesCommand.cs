using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Services;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class SeleccionarPostesCommand
    {
        // Capas de postes conocidas. El filtro DXF soporta el comodín "*" en LayerName.
        private const string CapaPosteWildcard = "POSTE_*";

        [CommandMethod("CTO_SELECCIONAR_POSTES", CommandFlags.Modal)]
        public void Execute()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            // Auto-seleccionar todos los INSERT en capas POSTE_*
            ObjectIdCollection ids = SelectionService.SelectAllOnLayer(ed, CapaPosteWildcard, "INSERT");

            if (ids.Count == 0)
            {
                // Fallback: pedir selección manual si no se encontró nada
                AcadLogger.Warn($"No se encontraron bloques en capas '{CapaPosteWildcard}'. " +
                                "Seleccioná los postes manualmente:");
                ObjectId[] manual = SelectionService.PromptForPostes(ed);
                if (manual == null || manual.Length == 0)
                {
                    AcadLogger.Warn("Selección cancelada.");
                    return;
                }
                ids = new ObjectIdCollection(manual);
            }

            // Limpiar contexto anterior (manzanas y segmentos se re-cargan al asociar)
            var arr = new ObjectId[ids.Count];
            ids.CopyTo(arr, 0);
            SelectionContext.Instance.SetPostes(arr);
            SelectionContext.Instance.ClearGeometry();

            AcadLogger.Info($"{ids.Count} postes seleccionados en capas POSTE_*.");
        }
    }
}
