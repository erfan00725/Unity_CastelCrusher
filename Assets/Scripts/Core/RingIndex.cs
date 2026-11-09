namespace Core
{
    public static class RingIndex
    {
        // Wrap-around index maths for a ring of `count` items.
        public static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;   // empty ring: nothing to wrap into, stay at 0
            return ((index % count) + count) % count;
        }

        public static int Next(int current, int count, int delta)
        {
            return Wrap(current + delta, count);
        }
    }
}