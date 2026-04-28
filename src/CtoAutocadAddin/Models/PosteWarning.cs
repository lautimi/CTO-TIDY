namespace Koovra.Cto.AutocadAddin.Models
{
    public class PosteWarning
    {
        public string HandleHex;           // handle hex del poste (para zoom)
        public string Calle;               // calleSegmento (puede ser empty)
        public double LargoFrenteOriginal; // valor antes del cap
        public double LargoCap;            // = LARGO (después del cap)
        public string FrenteMethod;        // "V4_StreetCorners" / "V3_Projection" / "V2_DetectCorners"
    }
}
