using UnityEngine;

namespace Cube.Transport {
    public interface IBitWriter {
        int BitsWritten { get; }

        void WriteBool(bool value);
        void WriteByte(byte value);
        void WriteUShort(ushort value);
        void WriteInt(int value);
        void WriteIntInRange(int value, int minInclusive, int maxInclusive);
        void WriteUInt(uint value);
        void WriteVector3(Vector3 value);
        void WriteFloat(float value);
        void WriteLossyFloat(float value, float min, float max, float precision = 0.1f);
        void WriteQuaternion(Quaternion value);
    }
}