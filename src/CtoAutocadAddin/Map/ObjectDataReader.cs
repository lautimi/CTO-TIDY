using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Koovra.Cto.AutocadAddin.Infrastructure;
using OdTable = Autodesk.Gis.Map.ObjectData.Table;

namespace Koovra.Cto.AutocadAddin.Map
{
    public static class ObjectDataReader
    {
        public const string TABLE_SEGMENTO   = "SEGMENTO";
        public const string FIELD_CALLE_1    = "CALLE_1";
        public const string CALLE_SIN_NOMBRE = "CALLE SIN NOMBRE";

        /// <summary>
        /// Lee el campo CALLE_1 de la tabla SEGMENTO para una entidad.
        /// Devuelve null si no existe la tabla, no hay registro, o el campo está vacío.
        /// </summary>
        public static string TryGetCalle1(ObjectId entityId,
            string tableName = TABLE_SEGMENTO,
            string fieldName = FIELD_CALLE_1)
        {
            try
            {
                MapApplication app = HostMapApplicationServices.Application;
                if (app == null) return null;

                Tables tables = app.ActiveProject.ODTables;
                OdTable table;
                try { table = tables[tableName]; }
                catch { return null; }
                if (table == null) return null;

                int fieldIdx = GetFieldIndex(table, fieldName);
                if (fieldIdx < 0) return null;

                Records records = table.GetObjectTableRecords(
                    0, entityId, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false);
                if (records == null) return null;

                foreach (Record r in records)
                {
                    string val = TryReadStringCell(r, fieldIdx);
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                return null;
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataReader.TryGetCalle1: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Bulk: para cada ObjectId del set, lee CALLE_1. Entidades sin OD se omiten.
        /// </summary>
        public static Dictionary<ObjectId, string> ReadCalle1Bulk(
            ObjectIdCollection ids,
            string tableName = TABLE_SEGMENTO,
            string fieldName = FIELD_CALLE_1)
        {
            var result = new Dictionary<ObjectId, string>();
            if (ids == null || ids.Count == 0) return result;

            OdTable table = null;
            int fieldIdx = -1;

            try
            {
                MapApplication app = HostMapApplicationServices.Application;
                if (app == null) return result;

                Tables tables = app.ActiveProject.ODTables;
                try { table = tables[tableName]; }
                catch { return result; }
                if (table == null) return result;

                fieldIdx = GetFieldIndex(table, fieldName);
                if (fieldIdx < 0) return result;
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataReader.ReadCalle1Bulk (init): {ex.Message}");
                return result;
            }

            foreach (ObjectId id in ids)
            {
                try
                {
                    Records records = table.GetObjectTableRecords(
                        0, id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false);
                    if (records == null) continue;

                    foreach (Record r in records)
                    {
                        string val = TryReadStringCell(r, fieldIdx);
                        if (!string.IsNullOrEmpty(val))
                        {
                            result[id] = val;
                            break;
                        }
                    }
                }
                catch
                {
                    // Entidad sin OD — se omite silenciosamente
                }
            }

            return result;
        }

        /// <summary>
        /// Lee la celda en <paramref name="fieldIdx"/> del Record como string.
        /// Usa reflexión para tolerar variaciones de la API de ManagedMapApi entre
        /// versiones de Map 3D (algunas exponen indexer Record[i], otras
        /// RetrieveCellAtIndex(int), y MapValue puede estar en distintos namespaces).
        /// </summary>
        private static string TryReadStringCell(Record r, int fieldIdx)
        {
            if (r == null) return null;
            object cell = null;

            // Intento 1: indexer Item[int]
            try
            {
                var indexer = r.GetType().GetProperty("Item", new[] { typeof(int) });
                if (indexer != null)
                    cell = indexer.GetValue(r, new object[] { fieldIdx });
            }
            catch { }

            // Intento 2: método RetrieveCellAtIndex(int)
            if (cell == null)
            {
                try
                {
                    var mi = r.GetType().GetMethod("RetrieveCellAtIndex", new[] { typeof(int) });
                    if (mi != null)
                        cell = mi.Invoke(r, new object[] { fieldIdx });
                }
                catch { }
            }

            // Intento 3: GetValue(int)
            if (cell == null)
            {
                try
                {
                    var mi = r.GetType().GetMethod("GetValue", new[] { typeof(int) });
                    if (mi != null)
                        cell = mi.Invoke(r, new object[] { fieldIdx });
                }
                catch { }
            }

            if (cell == null) return null;

            // Extraer string de la celda. Puede ser MapValue (con .StrValue) o string directo.
            if (cell is string s) return s;

            try
            {
                var pStr = cell.GetType().GetProperty("StrValue");
                if (pStr != null)
                {
                    object v = pStr.GetValue(cell);
                    if (v is string s2) return s2;
                }
            }
            catch { }

            try
            {
                var pStr2 = cell.GetType().GetProperty("StringValue");
                if (pStr2 != null)
                {
                    object v = pStr2.GetValue(cell);
                    if (v is string s3) return s3;
                }
            }
            catch { }

            // Fallback: ToString() — los wrappers Map suelen tener ToString razonable.
            return cell.ToString();
        }

        private static int GetFieldIndex(OdTable table, string fieldName)
        {
            FieldDefinitions defs = table.FieldDefinitions;
            for (int i = 0; i < defs.Count; i++)
            {
                if (string.Equals(defs[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
