using System;

namespace Koovra.Cto.Core
{
    /// <summary>
    /// Aplica la tabla oficial de correspondencia HP (Hogares Pasados - Futuro 100%) ×
    /// Largo del eje (≤ 160 m o > 160 m) para obtener la cantidad de CTOs
    /// de Despliegue Inicial (40%) y de Crecimiento Futuro (100%).
    /// Esta clase es pura (sin dependencias a AutoCAD) y totalmente testeable.
    /// </summary>
    public static class CtoCountCalculator
    {
        public struct Result
        {
            public int CDesp;
            public int CCrec;
            public int Total => CDesp + CCrec;

            public override string ToString() => $"C_DESP={CDesp} C_CREC={CCrec} TOTAL={Total}";
        }

        private struct Row
        {
            public int HpMax;
            public int ShortCDesp;
            public int ShortCCrec;
            public int LongCDesp;
            public int LongCCrec;

            public Row(int hpMax, int sd, int sc, int ld, int lc)
            {
                HpMax = hpMax;
                ShortCDesp = sd;
                ShortCCrec = sc;
                LongCDesp = ld;
                LongCCrec = lc;
            }
        }

        private static readonly Row[] Table =
        {
            new Row(2,  0, 1, 0, 0),
            new Row(5,  1, 0, 1, 0),
            new Row(8,  1, 0, 2, 0),
            new Row(16, 1, 1, 2, 0),
            new Row(20, 1, 2, 2, 1),
            new Row(24, 2, 1, 2, 1),
            new Row(32, 2, 2, 2, 2),
            new Row(40, 2, 3, 2, 3),
            new Row(48, 3, 3, 3, 3),
            new Row(56, 3, 4, 3, 4),
            new Row(64, 4, 4, 4, 4),
        };

        public const double LARGO_CORTE = 160.0;

        /// <summary>
        /// Calcula la cantidad de CTOs (despliegue + crecimiento) para un eje con un
        /// total de HP dado y un largo en metros. Para HP &lt;= 0 devuelve (0,0).
        /// Para HP &gt; 64 aplica el último rango (57–64) — ver <see cref="IsOutOfRange"/>.
        /// </summary>
        public static Result Calculate(int hp, double largoMetros)
        {
            if (hp <= 0) return new Result { CDesp = 0, CCrec = 0 };
            bool isLong = largoMetros > LARGO_CORTE;

            foreach (Row row in Table)
            {
                if (hp <= row.HpMax)
                {
                    return new Result
                    {
                        CDesp = isLong ? row.LongCDesp : row.ShortCDesp,
                        CCrec = isLong ? row.LongCCrec : row.ShortCCrec,
                    };
                }
            }

            Row last = Table[Table.Length - 1];
            return new Result
            {
                CDesp = isLong ? last.LongCDesp : last.ShortCDesp,
                CCrec = isLong ? last.LongCCrec : last.ShortCCrec,
            };
        }

        public static bool IsOutOfRange(int hp) => hp > Table[Table.Length - 1].HpMax;
    }
}
