using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.UI;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class ConfigCommand
    {
        [CommandMethod("CTO_CONFIG", CommandFlags.Modal)]
        public void Execute()
        {
            try
            {
                using (var dlg = new SettingsDialog())
                {
                    AcApp.ShowModalDialog(null, dlg, false);
                }
            }
            catch (System.Exception ex)
            {
                AcadLogger.Error($"CTO_CONFIG falló: {ex.GetType().Name}: {ex.Message}");
                AcadLogger.Error(ex.StackTrace ?? string.Empty);
            }
        }
    }
}
