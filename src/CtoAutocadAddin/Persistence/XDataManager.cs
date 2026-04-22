using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace Koovra.Cto.AutocadAddin.Persistence
{
    /// <summary>
    /// Helper para leer/escribir XData con AppName <see cref="AppNames.KOOVRA_CTO"/>.
    /// El formato almacenado es una secuencia plana de pares clave/valor:
    ///   [1001 "KOOVRA_CTO"] [1000 "ID_SEGMENT"] [1000 "value"] [1000 "HP"] [1071 12] ...
    /// - Strings: DxfCode 1000 (AsciiString)
    /// - Enteros: DxfCode 1071 (Int32)
    /// - Reales:  DxfCode 1040 (Real)
    /// </summary>
    public static class XDataManager
    {
        private const short DxfAppName = 1001;
        private const short DxfAsciiString = 1000;
        private const short DxfInt32 = 1071;
        private const short DxfReal = 1040;

        public static void EnsureRegApp(Transaction tr, Database db)
        {
            var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!table.Has(AppNames.KOOVRA_CTO))
            {
                table.UpgradeOpen();
                using (var r = new RegAppTableRecord { Name = AppNames.KOOVRA_CTO })
                {
                    table.Add(r);
                    tr.AddNewlyCreatedDBObject(r, true);
                }
            }
        }

        public static void SetString(Transaction tr, ObjectId id, string key, string value)
        {
            SetValues(tr, id, new[] { (key, (object)(value ?? string.Empty)) });
        }

        public static void SetInt(Transaction tr, ObjectId id, string key, int value)
        {
            SetValues(tr, id, new[] { (key, (object)value) });
        }

        public static void SetReal(Transaction tr, ObjectId id, string key, double value)
        {
            SetValues(tr, id, new[] { (key, (object)value) });
        }

        /// <summary>
        /// Escribe múltiples campos en una sola operación (hace merge con los existentes).
        /// </summary>
        public static void SetValues(Transaction tr, ObjectId id, IEnumerable<(string key, object value)> pairs)
        {
            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
            if (ent == null) return;

            EnsureRegApp(tr, ent.Database);
            Dictionary<string, object> existing = ReadAll(ent);

            foreach (var (k, v) in pairs)
            {
                existing[k] = v;
            }

            ent.XData = BuildBuffer(existing);
        }

        public static string GetString(Transaction tr, ObjectId id, string key)
        {
            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent == null) return null;
            Dictionary<string, object> all = ReadAll(ent);
            return all.TryGetValue(key, out object v) ? v as string : null;
        }

        public static int? GetInt(Transaction tr, ObjectId id, string key)
        {
            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent == null) return null;
            Dictionary<string, object> all = ReadAll(ent);
            return all.TryGetValue(key, out object v) && v is int i ? i : (int?)null;
        }

        public static double? GetReal(Transaction tr, ObjectId id, string key)
        {
            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
            if (ent == null) return null;
            Dictionary<string, object> all = ReadAll(ent);
            return all.TryGetValue(key, out object v) && v is double d ? d : (double?)null;
        }

        private static Dictionary<string, object> ReadAll(Entity ent)
        {
            var result = new Dictionary<string, object>();
            ResultBuffer rb = ent.GetXDataForApplication(AppNames.KOOVRA_CTO);
            if (rb == null) return result;

            using (rb)
            {
                TypedValue[] values = rb.AsArray();
                // Primer valor es el AppName (1001). Saltamos al siguiente.
                int i = 1;
                while (i < values.Length - 1)
                {
                    TypedValue kv = values[i];
                    TypedValue vv = values[i + 1];
                    if (kv.TypeCode != DxfAsciiString) { i++; continue; }

                    string key = (string)kv.Value;
                    switch (vv.TypeCode)
                    {
                        case DxfAsciiString: result[key] = (string)vv.Value;  break;
                        case DxfInt32:       result[key] = (int)vv.Value;     break;
                        case DxfReal:        result[key] = (double)vv.Value;  break;
                    }
                    i += 2;
                }
            }
            return result;
        }

        private static ResultBuffer BuildBuffer(Dictionary<string, object> pairs)
        {
            var list = new List<TypedValue> { new TypedValue(DxfAppName, AppNames.KOOVRA_CTO) };

            foreach (var kv in pairs)
            {
                list.Add(new TypedValue(DxfAsciiString, kv.Key));
                switch (kv.Value)
                {
                    case string s: list.Add(new TypedValue(DxfAsciiString, s)); break;
                    case int i: list.Add(new TypedValue(DxfInt32, i)); break;
                    case double d: list.Add(new TypedValue(DxfReal, d)); break;
                    default: list.Add(new TypedValue(DxfAsciiString, kv.Value?.ToString() ?? string.Empty)); break;
                }
            }

            return new ResultBuffer(list.ToArray());
        }
    }
}
