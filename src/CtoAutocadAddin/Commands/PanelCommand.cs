using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.UI;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class PanelCommand
    {
        private static CtoPanel _panel;

        [CommandMethod("CTO_PANEL", CommandFlags.Modal)]
        public void Execute()
        {
            if (_panel == null || _panel.IsDisposed)
                _panel = new CtoPanel();

            Application.ShowModelessDialog(null, _panel, false);
        }
    }
}
