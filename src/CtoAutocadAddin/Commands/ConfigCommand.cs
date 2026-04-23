using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.UI;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class ConfigCommand
    {
        [CommandMethod("CTO_CONFIG", CommandFlags.Modal)]
        public void Execute()
        {
            using (var dlg = new SettingsDialog())
            {
                AcApp.ShowModalDialog(null, dlg, false);
            }
        }
    }
}
