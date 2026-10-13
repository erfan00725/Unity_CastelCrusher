using System;

namespace Core
{
    public class CarouselWindow
    {
        public static int[] Window(int selectedIndex, int typeCount, int visiblePerSide)
        {
            if (typeCount <= 0) return Array.Empty<int>();

            // All types fit -> show them all in natural order
            if (typeCount <= visiblePerSide * 2 + 1)
            {
                int[] all = new int[typeCount];
                for (int i = 0; i < typeCount; i++) all[i] = i;
                return all;
            }

            int width = visiblePerSide * 2 + 1;
            int start = selectedIndex - visiblePerSide;
            int[] result = new int[width];
            for (int i = 0; i < width; i++)
            {
                int idx = start + i;
                result[i] = ((idx % typeCount) + typeCount) % typeCount;
            }
            return result;
        }
    }
}