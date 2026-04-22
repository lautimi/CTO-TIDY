using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Models;
using Koovra.Cto.AutocadAddin.Services;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class DesplegarCtosCommand
    {
        [CommandMethod("CTO_DESPLEGAR", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db  = doc.Database;

            if (!SelectionContext.Instance.TryGetPostes(out var polesIds))
            {
                AcadLogger.Warn("No hay postes seleccionados. Ejecuta CTO_SELECCIONAR_POSTES primero.");
                return;
            }

            AddinSettings s = AddinSettings.Current;
            AcadLogger.Info($"Desplegando: Acceso='{s.BlockNameDesp}'  Crecimiento='{s.BlockNameCrec}'  Capa='{s.CtoLayerName}'");

            int total = 0;

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var deployer = new CtoBlockDeployer(s.BlockNameDesp, s.BlockNameCrec, s.CtoLayerName);
                int purged = deployer.PurgeExistingBlocks(tr, db);
                if (purged > 0) AcadLogger.Info($"Purga previa: {purged} bloques en capa '{s.CtoLayerName}' eliminados.");
                foreach (ObjectId poleId in polesIds)
                    total += deployer.DeployForPole(tr, db, poleId);
                tr.Commit();
            }

            AcadLogger.Info($"{total} bloques CTO desplegados.");
        }
    }
}
