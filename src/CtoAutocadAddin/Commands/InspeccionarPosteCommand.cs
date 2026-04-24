using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Koovra.Cto.AutocadAddin.Persistence;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Comando de diagnóstico: mostrar toda la XData KOOVRA_CTO de un poste.
    /// Útil para inspeccionar valores post-asociación/cálculo sin tener que
    /// decodificar a mano la XData con LIST.
    ///
    /// Uso:
    ///   Command: CTO_INSPECCIONAR
    ///   Seleccionar poste.
    /// </summary>
    public class InspeccionarPosteCommand
    {
        [CommandMethod("CTO_INSPECCIONAR", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            var pso = new PromptEntityOptions("\nSeleccioná un poste: ");
            pso.SetRejectMessage("\nDebe ser una entidad.");
            pso.AddAllowedClass(typeof(Entity), false);
            PromptEntityResult per = ed.GetEntity(pso);
            if (per.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var sb = new StringBuilder();
                sb.AppendLine($"\n── XData KOOVRA_CTO para <H:{tr.GetObject(per.ObjectId, OpenMode.ForRead).Handle}> ──");

                AddLine(sb, tr, per.ObjectId, XDataKeys.ID_SEGMENT,   "string");
                AddLine(sb, tr, per.ObjectId, XDataKeys.REVISAR,      "string");
                AddLine(sb, tr, per.ObjectId, XDataKeys.LARGO,        "real");
                AddLine(sb, tr, per.ObjectId, XDataKeys.HP,           "int");
                AddLine(sb, tr, per.ObjectId, XDataKeys.COMENTARIOS,  "string");
                sb.AppendLine();
                AddLine(sb, tr, per.ObjectId, XDataKeys.ID_LINGA,     "string");
                AddLine(sb, tr, per.ObjectId, XDataKeys.LINGA_TIPO,   "string");
                AddLine(sb, tr, per.ObjectId, XDataKeys.LARGO_LINGA,  "real");
                sb.AppendLine();
                AddLine(sb, tr, per.ObjectId, XDataKeys.ID_FRENTE,    "string");
                AddLine(sb, tr, per.ObjectId, XDataKeys.LARGO_FRENTE, "real");
                sb.AppendLine();
                AddLine(sb, tr, per.ObjectId, XDataKeys.C_DESP,       "int");
                AddLine(sb, tr, per.ObjectId, XDataKeys.C_CREC,       "int");

                ed.WriteMessage(sb.ToString());
                tr.Commit();
            }
        }

        private static void AddLine(StringBuilder sb, Transaction tr, ObjectId id, string key, string kind)
        {
            object val = null;
            switch (kind)
            {
                case "string": val = XDataManager.GetString(tr, id, key); break;
                case "real":   val = XDataManager.GetReal  (tr, id, key); break;
                case "int":    val = XDataManager.GetInt   (tr, id, key); break;
            }
            string disp = val == null ? "<null>" : (val is double d ? d.ToString("F2") : val.ToString());
            if (disp == "") disp = "<empty>";
            sb.AppendLine($"  {key,-14} = {disp}");
        }
    }
}
