using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Koovra.Cto.AutocadAddin.Models
{
    public class PoleInfo
    {
        public ObjectId Id { get; set; }
        public Point3d Position { get; set; }
        public string SegmentId { get; set; }
        public ObjectId SegmentObjectId { get; set; } = ObjectId.Null;
        public string Estado { get; set; }
        public string Comentarios { get; set; }
        public int Hp { get; set; }
        public double LargoSegmento { get; set; }
        public int CDesp { get; set; }
        public int CCrec { get; set; }

        public int TotalCtos => CDesp + CCrec;
    }
}
