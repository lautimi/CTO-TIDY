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

                Record record = Record.Create();
                table.InitRecord(record);
                SetCell(record, 0, string.Empty);   // ACRÓNIMO
                SetCell(record, 1, hpEje);           // HP_EJE
                SetCell(record, 2, string.Empty);    // ID_SEGMENTO
                table.AddRecord(record, blockRefId);
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
            Type dataTypeEnum = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                dataTypeEnum = asm.GetType("Autodesk.Gis.Map.Constants.DataType");
                if (dataTypeEnum != null) break;
            }

            object dataTypeValue = null;
            if (dataTypeEnum != null)
            {
                try { dataTypeValue = Enum.Parse(dataTypeEnum, dataTypeName, ignoreCase: true); }
                catch { }
            }
            if (dataTypeValue == null)
            {
                if (dataTypeName == "integer")        dataTypeValue = 2;
                else if (dataTypeName == "character") dataTypeValue = 3;
                else                                  dataTypeValue = 3;
            }

            object fieldDef = null;
            try
            {
                fieldDef = Activator.CreateInstance(
                    typeof(FieldDefinition), new object[] { name, dataTypeValue, string.Empty });
            }
            catch { }

            if (fieldDef == null)
            {
                try
                {
                    fieldDef = Activator.CreateInstance(
                        typeof(FieldDefinition), new object[] { name, dataTypeValue });
                }
                catch { }
            }

            if (fieldDef == null) return;

            var addMethod = defs.GetType().GetMethod("Add", new[] { typeof(FieldDefinition) });
            addMethod?.Invoke(defs, new[] { fieldDef });
        }

        /// <summary>
        /// Escribe un valor en la celda fieldIdx del record via reflexión pura.
        /// La celda es un MapValue — se busca el setter adecuado según el tipo del valor.
        /// </summary>
        private static void SetCell(Record record, int fieldIdx, object value)
        {
            try
            {
                // Obtener la celda (MapValue) via indexer Item[int]
                var indexer = record.GetType().GetProperty("Item", new[] { typeof(int) });
                if (indexer == null) return;

                object cell = indexer.GetValue(record, new object[] { fieldIdx });
                if (cell == null) return;

                // Intentar asignar mediante Assign(MapValue) construyendo MapValue via reflexión
                Type cellType = cell.GetType();
                object newMapVal = null;
                try { newMapVal = Activator.CreateInstance(cellType, new[] { value }); } catch { }

                if (newMapVal != null)
                {
                    var assignMi = cellType.GetMethod("Assign", new[] { cellType });
                    if (assignMi != null)
                    {
                        assignMi.Invoke(cell, new[] { newMapVal });
                        return;
                    }
                }

                // Fallback: setters de propiedad directos en MapValue
                if (value is string s)
                {
                    var p = cellType.GetProperty("StrValue") ?? cellType.GetProperty("StringValue");
                    if (p != null && p.CanWrite) { p.SetValue(cell, s); return; }
                }
                if (value is int i)
                {
                    var p = cellType.GetProperty("Int32Value")
                         ?? cellType.GetProperty("IntegerValue")
                         ?? cellType.GetProperty("LongValue");
                    if (p != null && p.CanWrite) { p.SetValue(cell, i); return; }
                }
            }
            catch (Exception ex)
            {
                AcadLogger.Warn($"ObjectDataWriter.SetCell[{fieldIdx}]: {ex.Message}");
            }
        }
    }
}
