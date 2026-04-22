using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Encadena los pasos 1→5 llamando cada comando en secuencia.
    /// Se espera que CTO_SELECCIONAR_POSTES haya sido ejecutado antes.
    /// </summary>
    public class RunAllCommand
    {
        [CommandMethod("CTO_RUN_ALL", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            AcadLogger.Info("== CTO_RUN_ALL ==");
            ed.Command("CTO_ASOCIAR_POSTES");
            ed.Command("CTO_LEER_COMENTARIOS");
            ed.Command("CTO_CALCULAR");
            ed.Command("CTO_DESPLEGAR");
            AcadLogger.Info("== CTO_RUN_ALL terminado ==");
        }
    }
}
