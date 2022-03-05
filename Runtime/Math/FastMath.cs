namespace Cube {
    static class FastMath {
        static readonly int[] _deBruijnLookup = new int[32] {
            0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
            8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
        };

        /// <summary>
        /// Optimized implementation of Log2
        /// </summary>
        public static int Log2(uint v) {
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;

            return _deBruijnLookup[(v * 0x07C4ACDDU) >> 27];
        }
    }
}