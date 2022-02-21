using UnityEngine;

namespace Cube.Transport {
    // #todo Tests die sicher stellen dass alles zu BitWriter passt
    // #todo Naming
    public sealed class DummyBitWriter : IBitWriter {
        public int BitsWritten => _bitsWritten;
        public int BytesWritten => (_bitsWritten + 7) / 8;

        int _bitsWritten = 0;

        public void WriteBool(bool value) => _bitsWritten += 1;

        public void WriteByte(byte value) => _bitsWritten += 8;

        public void WriteUShort(ushort value) => _bitsWritten += 16;

        public void WriteInt(int value) => _bitsWritten += 32;

        public void WriteIntInRange(int value, int minInclusive, int maxInclusive) {
            value = Mathf.Clamp(value, minInclusive, maxInclusive);

            var bits = BitUtil.ComputeRequiredIntBits(minInclusive, maxInclusive);
            _bitsWritten += bits;
        }

        public void WriteUInt(uint value) => _bitsWritten += 32;

        public void WriteFloat(float value) => _bitsWritten += 32;

        public void WriteLossyFloat(float value, float min, float max, float precision = 0.1f) {
            value = Mathf.Clamp(value, min, max);

            var inv = 1 / precision;
            WriteIntInRange((int)(value * inv), (int)(min * inv), (int)(max * inv));
        }

        public void WriteVector3(Vector3 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        public void WriteQuaternion(Quaternion value) => WriteUInt(0);

        public void WriteSerializable(IBitSerializable obj) => obj.Serialize(this);
    }
}