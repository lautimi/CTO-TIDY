using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace Koovra.Cto.AutocadAddin.Infrastructure
{
    public static class AcadLogger
    {
        private static Editor Editor => Application.DocumentManager.MdiActiveDocument?.Editor;

        public static void Info(string msg) => Editor?.WriteMessage($"\n[CTO] {msg}");
        public static void Warn(string msg) => Editor?.WriteMessage($"\n[CTO][WARN] {msg}");
        public static void Error(string msg) => Editor?.WriteMessage($"\n[CTO][ERROR] {msg}");
    }
}
