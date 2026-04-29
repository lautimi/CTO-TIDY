using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Koovra.Cto.AutocadAddin.Infrastructure;
using OdTable = Autodesk.Gis.Map.ObjectData.Table;

namespace Koovra.Cto.AutocadAddin.Map
{
    public static class ObjectDataWriter
    {
        public const string TABLE_CAJA_ACCESO = "CAJA_ACCESO";
        public const string FIELD_ACRONIMO    = "ACRÓNIMO";
        public const string FIELD_HP_EJE      = "HP_EJE";
        public const string FIELD_ID_SEGMENTO = "ID_SEGMENTO";

        /// <summary>
        /// Crea la tabla CAJA_ACCESO con sus 3 campos si no existe todavía.
        /// Seguro llamar múltiples veces (idempotente).
        /// </summary>
        public static void EnsureTable()
        {
            try
            {
                MapApplication app = HostMapApplicationServices.Application;
                if (app == null) return;

                Tables tables = app.ActiveProject.ODTables;

                // Si ya existe, no hacer nada
                try
                {
                    var existing = tables[TABLE_CAJA_ACCESO];
                    if (existing != null) return;
                }
                catch { }

                // Crear campos vía reflexión (DataType enum varía entre versiones)
                FieldDefinitions defs = CreateFieldDefinitions();
                if (defs == null) return;

                // Tables.Add(tableName, FieldDefinitions)
                var addMethod = tables.GetType().GetMethod("Add",
                    new[] { typeof(string), defs.GetType() });
                if (addMethod != null)
                {
                    addMethod.Invoke(tables, new object[] { TABLE_CAJA_ACCESO, defs });
                    AcadLogger.Info($"ObjectDataWriter: tabla '{TABLE_CAJA_ACCESO}' creada.");
                }
                else
                {
                    AcadLogger.Warn($"ObjectDataWriter.EnsureTable: no se encontró método Tables.Add.");
                }
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataWriter.EnsureTable: {ex.Message}");
            }
        }

        /// <summary>
        /// Escribe un record CAJA_ACCESO para el BlockReference dado.
        /// Si ya existe un record previo en esa entidad lo elimina primero (idempotencia).
        /// Cualquier excepción se loguea y no se propaga.
        /// </summary>
        public static void WriteCajaAcceso(ObjectId blockRefId, int hpEje)
        {
            try
            {
                MapApplication app = HostMapApplicationServices.Application;
                if (app == null) return;

                Tables tables = app.ActiveProject.ODTables;
                OdTable table;
                try { table = tables[TABLE_CAJA_ACCESO]; }
                catch
                {
                    AcadLogger.Warn($"ObjectDataWriter.WriteCajaAcceso: tabla '{TABLE_CAJA_ACCESO}' no existe.");
                    return;
                }
                if (table == null) return;

                // Eliminar record previo si existe (idempotencia)
                try
                {
                    Records existing = table.GetObjectTableRecords(
                        0, blockRefId, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, false);
                    if (existing != null)
                    {
                        var removeMethod = table.GetType().GetMethod("RemoveObjectTableRecord",
                            new[] { typeof(ObjectId) });
                        if (removeMethod != null)
                            removeMethod.Invoke(table, new object[] { blockRefId });
                    }
                }
                catch { }

                // Agregar nuevo record
                Record record = AddRecord(table, blockRefId);
                if (record == null)
                {
                    AcadLogger.Warn($"ObjectDataWriter.WriteCajaAcceso: no se pudo crear record para {blockRefId}.");
                    return;
                }

                SetCellString(record, 0, string.Empty);   // ACRÓNIMO
                SetCellInt(record, 1, hpEje);              // HP_EJE
                SetCellString(record, 2, string.Empty);    // ID_SEGMENTO
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataWriter.WriteCajaAcceso: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static FieldDefinitions CreateFieldDefinitions()
        {
            try
            {
                // Instanciar FieldDefinitions vía constructor sin parámetros
                FieldDefinitions defs = (FieldDefinitions)Activator.CreateInstance(typeof(FieldDefinitions));

                AddFieldDef(defs, FIELD_ACRONIMO,    "character");
                AddFieldDef(defs, FIELD_HP_EJE,      "integer");
                AddFieldDef(defs, FIELD_ID_SEGMENTO, "character");

                return defs;
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataWriter.CreateFieldDefinitions: {ex.Message}");
                return null;
            }
        }

        private static void AddFieldDef(FieldDefinitions defs, string name, string dataTypeName)
        {
            // Buscar enum DataType por nombre (varía entre versiones de Map)
            Type dataTypeEnum = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                dataTypeEnum = asm.GetType("Autodesk.Gis.Map.Constants.DataType");
                if (dataTypeEnum != null) break;
            }

            object dataTypeValue = null;
            if (dataTypeEnum != null)
            {
                foreach (var enumName in new[] { dataTypeName, dataTypeName.ToUpper() })
                {
                    try
                    {
                        dataTypeValue = Enum.Parse(dataTypeEnum, enumName, ignoreCase: true);
                        break;
                    }
                    catch { }
                }
            }

            if (dataTypeValue == null)
            {
                // Fallback: integer = 2, character = 3 (valores típicos en Map API)
                if (dataTypeName == "integer")       dataTypeValue = 2;
                else if (dataTypeName == "character") dataTypeValue = 3;
                else                                  dataTypeValue = 3;
            }

            // FieldDefinition(string name, DataType type, string defaultValue)
            // o FieldDefinition(string name, DataType type)
            object fieldDef = null;
            try
            {
                fieldDef = Activator.CreateInstance(
                    typeof(FieldDefinition),
                    new object[] { name, dataTypeValue, string.Empty });
            }
            catch { }

            if (fieldDef == null)
            {
                try
                {
                    fieldDef = Activator.CreateInstance(
                        typeof(FieldDefinition),
                        new object[] { name, dataTypeValue });
                }
                catch { }
            }

            if (fieldDef == null) return;

            var addMethod = defs.GetType().GetMethod("Add", new[] { typeof(FieldDefinition) });
            addMethod?.Invoke(defs, new[] { fieldDef });
        }

        private static Record AddRecord(OdTable table, ObjectId entityId)
        {
            // Intentar table.AddObjectTableRecord(ObjectId)
            var addRec = table.GetType().GetMethod("AddObjectTableRecord", new[] { typeof(ObjectId) });
            if (addRec != null)
                return addRec.Invoke(table, new object[] { entityId }) as Record;

            // Fallback: table.GetObjectTableRecords con OpenForWrite crea si no existe
            try
            {
                Records recs = table.GetObjectTableRecords(
                    0, entityId, Autodesk.Gis.Map.Constants.OpenMode.OpenForWrite, true);
                if (recs != null)
                    foreach (Record r in recs) return r;
            }
            catch { }

            return null;
        }

        private static void SetCellString(Record record, int fieldIdx, string value)
        {
            SetCell(record, fieldIdx, value);
        }

        private static void SetCellInt(Record record, int fieldIdx, int value)
        {
            SetCell(record, fieldIdx, value);
        }

        private static void SetCell(Record record, int fieldIdx, object value)
        {
            // Intento 1: indexer Item[int] = value
            try
            {
                var indexer = record.GetType().GetProperty("Item", new[] { typeof(int) });
                if (indexer != null && indexer.CanWrite)
                {
                    indexer.SetValue(record, value, new object[] { fieldIdx });
                    return;
                }
            }
            catch { }

            // Intento 2: SetCellAtIndex(int, object)
            try
            {
                var mi = record.GetType().GetMethod("SetCellAtIndex",
                    new[] { typeof(int), typeof(object) });
                if (mi != null) { mi.Invoke(record, new[] { (object)fieldIdx, value }); return; }
            }
            catch { }

            // Intento 3: SetValue(int, object)
            try
            {
                var mi = record.GetType().GetMethod("SetValue",
                    new[] { typeof(int), typeof(object) });
                if (mi != null) { mi.Invoke(record, new[] { (object)fieldIdx, value }); return; }
            }
            catch { }

            // Intento 4: construir MapValue y asignar
            try
            {
                object mapVal = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Autodesk.Gis.Map.ObjectData.MapValue");
                    if (t == null) t = asm.GetType("Autodesk.Gis.Map.Constants.MapValue");
                    if (t != null)
                    {
                        try { mapVal = Activator.CreateInstance(t, new[] { value }); break; }
                        catch { }
                    }
                }
                if (mapVal != null)
                {
                    var indexer2 = record.GetType().GetProperty("Item", new[] { typeof(int) });
                    if (indexer2 != null && indexer2.CanWrite)
                        indexer2.SetValue(record, mapVal, new object[] { fieldIdx });
                }
            }
            catch { }
        }
    }
}
