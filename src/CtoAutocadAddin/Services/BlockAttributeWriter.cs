using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Koovra.Cto.AutocadAddin.Infrastructure;

namespace Koovra.Cto.AutocadAddin.Services
{
    /// <summary>
    /// Crea las AttributeReference de un BlockReference recién insertado, tomando las
    /// AttributeDefinition de su BlockTableRecord. Único responsable de escribir atributos
    /// (hoy solo se leen — ver TextBufferCollector.LoadAllHpBlocks).
    /// </summary>
    public static class BlockAttributeWriter
    {
        /// <summary>
        /// Crea las AttributeReference del BlockReference a partir de las AttributeDefinition
        /// de su BlockTableRecord. DEBE llamarse después de ms.AppendEntity(br) y antes de
        /// cerrar el using del br. Devuelve cuántos atributos matchearon un tag del diccionario.
        /// </summary>
        public static int AppendAttributes(Transaction tr, BlockReference br, IDictionary<string, string> valuesByTag)
        {
            int matched = 0;
            var byTag = new Dictionary<string, string>(valuesByTag, StringComparer.OrdinalIgnoreCase);

            var btr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return 0;

            foreach (ObjectId id in btr)
            {
                AttributeDefinition attDef = null;
                try { attDef = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition; }
                catch { continue; }
                if (attDef == null || attDef.Constant) continue;

                try
                {
                    var attRef = new AttributeReference();
                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);

                    if (byTag.TryGetValue(attDef.Tag, out string value))
                    {
                        attRef.TextString = value;
                        matched++;
                    }

                    br.AttributeCollection.AppendAttribute(attRef);
                    tr.AddNewlyCreatedDBObject(attRef, true);
                }
                catch (Exception ex)
                {
                    AcadLogger.Warn($"BlockAttributeWriter: no se pudo escribir atributo '{attDef.Tag}': {ex.Message}");
                }
            }

            return matched;
        }

        /// <summary>true si el BlockTableRecord tiene al menos una AttributeDefinition (no constante).</summary>
        public static bool HasAttributeDefinitions(Transaction tr, ObjectId btrId)
        {
            var btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return false;

            foreach (ObjectId id in btr)
            {
                AttributeDefinition attDef = null;
                try { attDef = tr.GetObject(id, OpenMode.ForRead) as AttributeDefinition; }
                catch { continue; }
                if (attDef != null && !attDef.Constant) return true;
            }
            return false;
        }

        /// <summary>Lee el valor de un atributo por tag de un BlockReference existente. null si no está.</summary>
        public static string ReadAttribute(Transaction tr, BlockReference br, string tag)
        {
            foreach (ObjectId attId in br.AttributeCollection)
            {
                AttributeReference att = null;
                try { att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference; }
                catch { continue; }
                if (att == null) continue;
                if (string.Equals(att.Tag, tag, StringComparison.OrdinalIgnoreCase)) return att.TextString;
            }
            return null;
        }
    }
}
