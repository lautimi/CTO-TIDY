namespace Koovra.Cto.Core
{
    public static class HpDistributor
    {
        public static int[] Distribute(int hpEje, int nCajas)
        {
            if (nCajas <= 0) return new int[0];
            if (hpEje <= 0) return new int[nCajas];
            int[] result = new int[nCajas];
            int base_ = hpEje / nCajas;
            int resto = hpEje % nCajas;
            for (int i = 0; i < nCajas; i++)
                result[i] = base_ + (i < resto ? 1 : 0);
            return result;
        }
    }
}
