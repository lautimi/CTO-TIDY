using System;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace Koovra.Cto.AutocadAddin.Commands
{
    /// <summary>
    /// Comando utilitario para navegar a una entidad por su Handle hex.
    /// Útil para resolver warnings que imprimen handles (ej. postes sin manzana,
    /// lingas que cruzan esquina).
    ///
    /// Uso:
    ///   Command: CTO_ZOOM_HANDLE
    ///   Handle hex a buscar: A3F   (o A3F por ejemplo desde el log)
    /// </summary>
    public class ZoomHandleCommand
    {
        [CommandMethod("CTO_ZOOM_HANDLE", CommandFlags.Modal)]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db  = doc.Database;
            Editor   ed  = doc.Editor;

            PromptStringOptions pso = new PromptStringOptions("\nHandle hex a buscar: ")
            {
                AllowSpaces = false,
            };
            PromptResult pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK) return;

            string raw = pr.StringResult?.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                ed.WriteMessage("\nHandle vacío.");
                return;
            }

            if (!long.TryParse(raw, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out long hv))
            {
                ed.WriteMessage($"\nHandle inválido: \"{raw}\". Debe ser hexadecimal (ej: A3F).");
                return;
            }

            if (!db.TryGetObjectId(new Handle(hv), out ObjectId id) || id.IsNull)
            {
                ed.WriteMessage($"\nHandle {raw} no encontrado en este DWG.");
                return;
            }

            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                {
                    ed.WriteMessage($"\nHandle {raw} existe pero no es una entidad dibujable.");
                    return;
                }

                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch
                {
                    ed.WriteMessage($"\nHandle {raw}: entidad sin extents geométricos.");
                    return;
                }

                Point3d min = ext.MinPoint, max = ext.MaxPoint;
                double dx = max.X - min.X, dy = max.Y - min.Y;

                // Padding mínimo para que entidades puntuales (INSERT) se vean con contexto
                double pad = Math.Max(5.0, Math.Max(dx, dy) * 0.5);
                double width  = Math.Max(dx, 20.0) + pad * 2;
                double height = Math.Max(dy, 20.0) + pad * 2;

                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    view.CenterPoint = new Point2d((min.X + max.X) / 2, (min.Y + max.Y) / 2);
                    view.Width  = width;
                    view.Height = height;
                    ed.SetCurrentView(view);
                }

                ed.WriteMessage($"\nZoom a entidad {ent.GetType().Name} <H:{raw}> " +
                                $"en ({(min.X + max.X) / 2:F2}, {(min.Y + max.Y) / 2:F2}).");

                tr.Commit();
            }
        }

        /// <summary>
        /// Hace zoom a la entidad con el handle hex dado.
        /// Retorna false si el handle no existe o no tiene extents.
        /// </summary>
        public static bool ZoomToHandle(string handleHex)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            Database db = doc.Database;
            Editor   ed = doc.Editor;

            if (!long.TryParse(handleHex?.Trim(),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out long hv))
                return false;

            if (!db.TryGetObjectId(new Handle(hv), out ObjectId id) || id.IsNull)
                return false;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) { tr.Commit(); return false; }

                    Extents3d ext;
                    try { ext = ent.GeometricExtents; }
                    catch { tr.Commit(); return false; }

                    Point3d min = ext.MinPoint, max = ext.MaxPoint;
                    double dx  = max.X - min.X, dy = max.Y - min.Y;
                    double pad = Math.Max(5.0, Math.Max(dx, dy) * 0.5);
                    double w   = Math.Max(dx, 20.0) + pad * 2;
                    double h   = Math.Max(dy, 20.0) + pad * 2;

                    using (ViewTableRecord view = ed.GetCurrentView())
                    {
                        view.CenterPoint = new Point2d((min.X + max.X) / 2, (min.Y + max.Y) / 2);
                        view.Width  = w;
                        view.Height = h;
                        ed.SetCurrentView(view);
                    }
                    tr.Commit();
                }
                return true;
            }
            catch { return false; }
        }
    }
}
