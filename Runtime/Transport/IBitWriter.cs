using UnityEngine;

namespace Cube.Transport {
    /// <summary>
    /// This is an interface so we can do "dummy writes" to estimate the required size.
    /// </summary>
    public interface IBitWriter {
        int BitsWritten { get; }

        void WriteBool(bool value);
        void WriteByte(byte value);
        void WriteUShort(ushort value);
        void WriteInt(int value);
        void WriteIntInRange(int value, int minInclusive, int maxInclusive);
        void WriteUInt(uint value);
        void WriteString(string value);
        void WriteVector3(Vector3 value);
        void WriteFloat(float value);
        void WriteLossyFloat(float value, float min, float max, float precision = 0.1f);
        void WriteQuaternion(Quaternion value);
        void WriteSerializable(IBitSerializable obj);
    }
}