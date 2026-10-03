using System.Collections.Generic;

namespace Core
{
    public static class LeftoverScoreCalculator
    {
        public static float Compute(IReadOnlyList<(int remaining, float multiplier)> entries, float scorePerUnusedShot)
        {
            float total = 0f;
            foreach (var entry in entries)
            {
                total += entry.remaining * scorePerUnusedShot * entry.multiplier;
            }
            return total;
        }
    }
}