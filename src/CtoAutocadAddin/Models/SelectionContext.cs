using Autodesk.AutoCAD.DatabaseServices;

namespace Koovra.Cto.AutocadAddin.Models
{
    /// <summary>
    /// Guarda las selecciones entre comandos (postes, manzanas, segmentos)
    /// para que el flujo CTO_* pueda ser encadenado sin re-seleccionar.
    /// Singleton por proceso de AutoCAD.
    /// </summary>
    public class SelectionContext
    {
        private static readonly SelectionContext _instance = new SelectionContext();
        public static SelectionContext Instance => _instance;

        public ObjectId[] Postes { get; private set; }
        public ObjectIdCollection Manzanas { get; private set; }
        public ObjectIdCollection Segmentos { get; private set; }

        public void SetPostes(ObjectId[] ids) => Postes = ids;
        public void SetManzanas(ObjectIdCollection ids) => Manzanas = ids;
        public void SetSegmentos(ObjectIdCollection ids) => Segmentos = ids;

        public bool TryGetPostes(out ObjectId[] ids)
        {
            ids = Postes;
            return ids != null && ids.Length > 0;
        }

        /// <summary>
        /// Limpia solo manzanas y segmentos (se recargan desde capas al próximo CTO_ASOCIAR_POSTES).
        /// </summary>
        public void ClearGeometry()
        {
            Manzanas = null;
            Segmentos = null;
        }

        /// <summary>
        /// Limpia todo el contexto.
        /// </summary>
        public void Clear()
        {
            Postes = null;
            Manzanas = null;
            Segmentos = null;
        }
    }
}
