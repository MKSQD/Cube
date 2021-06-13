using System.Collections.Generic;

namespace Cube.Transport {
    /// Pool for BitStreams which are used for 1 frame! only.
    public static class BitStreamPool {
        static List<BitStream> pool = new List<BitStream>();
        static int currentIdx;

        public static BitStream Create() {
            if (currentIdx == pool.Count) {
                for (int i = 0; i < 32; ++i)
                    pool.Add(new BitStream());
            }

            var bs = pool[currentIdx++];
            bs.Reset();

            return bs;
        }

        public static void FrameReset() {
            currentIdx = 0;
        }
    }
}