using System.Collections.Generic;

namespace Cube.Transport {
    public class BitStreamPool {
        List<BitStream> _pool = new List<BitStream>();
        int _current;

        public BitStream Create() {
            if (_current == _pool.Count) {
                for (int i = 0; i < 32; ++i)
                    _pool.Add(new BitStream());
            }

            var bs = _pool[_current++];
            bs.Reset();

            return bs;
        }

        public void FrameReset() {
            _current = 0;
        }
    }
}