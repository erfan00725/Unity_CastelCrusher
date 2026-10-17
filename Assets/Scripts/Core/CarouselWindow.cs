using System;

namespace Core
{
    public class CarouselWindow
    {
        // Returns the ring of type indices to display, in left-to-right order.
        // The selected type always lands in the middle slot, so the whole row
        // rotates with the selection (the "swap" carousel behaviour) instead
        // of showing a fixed natural-order list.
        public static int[] Window(int selectedIndex, int typeCount, int visiblePerSide)
        {
            if (typeCount <= 0) return Array.Empty<int>();

            int width = Math.Min(typeCount, visiblePerSide * 2 + 1);
            int middle = (width - 1) / 2;
            int start = selectedIndex - middle;

            int[] result = new int[width];
            for (int i = 0; i < width; i++)
            {
                int idx = (start + i) % typeCount;
                if (idx < 0) idx += typeCount;
                result[i] = idx;
            }
            return result;
        }
    }
}
