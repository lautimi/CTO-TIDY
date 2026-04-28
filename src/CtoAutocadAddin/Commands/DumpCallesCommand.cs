using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Infrastructure;
using Koovra.Cto.AutocadAddin.Map;

namespace Koovra.Cto.AutocadAddin.Commands
{
    public class DumpCallesCommand
    {
        [CommandMethod("CTO_DUMP_CALLES", CommandFlags.UsePickSet)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            PromptSelectionResult selRes = ed.GetSelection(
                new PromptSelectionOptions {
                    MessageForAdding = "\nSeleccioná segmentos (Lines) para inspeccionar CALLE_1: "
                });
            if (selRes.Status != PromptStatus.OK) return;

            var ids = new ObjectIdCollection();
            foreach (SelectedObject so in selRes.Value)
                if (so != null) ids.Add(so.ObjectId);

            var dict = ObjectDataReader.ReadCalle1Bulk(ids);

            ed.WriteMessage($"\n[CTO_DUMP_CALLES] Total seleccionados: {ids.Count}");
            ed.WriteMessage($"\n[CTO_DUMP_CALLES] Con CALLE_1 leída: {dict.Count}");
            ed.WriteMessage($"\n[CTO_DUMP_CALLES] Primeros 10:");

            int shown = 0;
            foreach (var kv in dict)
            {
                if (shown++ >= 10) break;
                ed.WriteMessage($"\n  {kv.Key.Handle} → \"{kv.Value}\"");
            }

            var grouped = dict.Values
                .GroupBy(v => v ?? "<null>")
                .OrderByDescending(g => g.Count())
                .Take(10);
            ed.WriteMessage("\n[CTO_DUMP_CALLES] Top 10 calles por frecuencia:");
            foreach (var g in grouped)
                ed.WriteMessage($"\n  \"{g.Key}\" × {g.Count()}");
        }
    }
}
