using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Koovra.Cto.AutocadAddin.Models;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Cache de datos pesados precomputados al abrir CTO_PANEL.
    /// AsociarPostesCommand puede consumir estos datos sin recomputar.
    /// Reset() lo limpia (ej. cuando cambia el DWG).
    /// </summary>
    public static class CtoCache
    {
        public static StreetCornerLibrary CornerLib { get; set; }
        public static Dictionary<ObjectId, string> CalleByOid { get; set; }
        public static ObjectIdCollection SegmentosCached { get; set; }
        public static ObjectIdCollection ManzanasCached  { get; set; }
        public static List<PosteWarning> PostesEnEsquina { get; set; } = new List<PosteWarning>();

        public static bool IsInitialized => CornerLib != null;

        public static void Reset()
        {
            CornerLib = null;
            CalleByOid = null;
            SegmentosCached = null;
            ManzanasCached = null;
            PostesEnEsquina = new List<PosteWarning>();
        }
    }
}
