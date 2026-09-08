using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Koovra.Cto.AutocadAddin.Geometry;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Lee las entidades de propiedad servicio (una por vivienda o lote) de su capa y
    /// resuelve cuántas viviendas aporta cada una.
    ///
    /// El bloque VIVIENDA del template no tiene atributos: en ese caso la propiedad
    /// cuenta 1. Si el bloque sí trae un atributo de cantidad (MDU, comercio con varias
    /// unidades), se usa ese valor.
    /// </summary>
    public static class PropertyCollector
    {
        public class Property
        {
            public ObjectId Id;
            public Point3d  Position;
            public int      Viviendas;
            public string   SegmentId;        // handle hex; null/"" si no se pudo asociar
            public ObjectId SegmentObjectId;
        }

        public static List<Property> Collect(
            Transaction tr, Editor ed, string layerName, IList<string> countTags)
        {
            var result = new List<Property>();

            ObjectIdCollection ids = SelectionService.SelectAllOnLayer(ed, layerName, "INSERT");
            foreach (ObjectId id in ids)
            {
                BlockReference br = null;
                try { br = tr.GetObject(id, OpenMode.ForRead) as BlockReference; }
                catch { continue; }
                if (br == null) continue;

                result.Add(new Property
                {
                    Id        = id,
                    Position  = Extensions.GetInsertionOrPosition(br),
                    Viviendas = ReadCount(tr, br, countTags),
                });
            }

            return result;
        }

        /// <summary>
        /// Cantidad de viviendas de una propiedad: el primer atributo cuyo tag esté en
        /// <paramref name="countTags"/> y parsee como entero positivo. Sin atributos, 1.
        /// </summary>
        private static int ReadCount(Transaction tr, BlockReference br, IList<string> countTags)
        {
            if (countTags == null || countTags.Count == 0) return 1;

            foreach (string tag in countTags)
            {
                string raw = BlockAttributeWriter.ReadAttribute(tr, br, tag);
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int val;
                if (int.TryParse(raw.Trim(), out val) && val > 0) return val;
            }

            return 1;
        }
    }
}
