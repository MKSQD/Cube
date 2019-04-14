using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public class BitStream {
        byte[] _data;
        public byte[] data {
            get { return _data; }
        }

        int _numberOfBitsUsed = 0;
        int _readOffset = 0;

        public int LengthInBits {
            get { return _numberOfBitsUsed; }
        }
        public int Length {
            get { return BitsToBytes(_numberOfBitsUsed); }
        }

        int Capacity {
            get { return _data.Length; }
        }

        /// <summary>
        /// Current read position in bits.
        /// </summary>
        public int Position {
            get { return _readOffset; }
            set {
                if (value > _numberOfBitsUsed)
                    throw new IndexOutOfRangeException();

                _readOffset = value;
            }
        }

        public BitStream() : this(64) { }

        public BitStream(int lengthInBytes) {
            _data = new byte[lengthInBytes];
        }

        BitStream(byte[] buffer, int lengthInBits) {
            _data = buffer;
            _numberOfBitsUsed = lengthInBits;
        }
        
        public static BitStream CreateWithExistingBuffer(byte[] data, int lengthInBits) {
            return new BitStream(data, lengthInBits);
        }
        
        public void Reset() {
            _numberOfBitsUsed = 0;
            _readOffset = 0;
        }
        
        public void ResetWithBuffer(byte[] data, int lengthInBits) {
            var lengthInBytes = BitsToBytes(lengthInBits);
            if (lengthInBytes > Capacity) {
                Array.Resize(ref _data, lengthInBytes);
            }

            Array.Copy(data, 0, _data, 0, lengthInBytes);
            _numberOfBitsUsed = lengthInBits;
        }

        #region Read
        public void Read(ref bool val) {
            val = ReadBool();
        }

        public unsafe bool ReadBool() {
            var val = new bool();
            Read((byte*)&val, 1);
            return val;
        }

        public void Read(ref byte val) {
            val = ReadByte();
        }

        public unsafe byte ReadByte() {
            var val = new byte();
            Read(&val, 8);
            return val;
        }

        public void Read(ref ushort val) {
            val = ReadUShort();
        }

        public unsafe ushort ReadUShort() {
            var val = new ushort();
            Read((byte*)&val, 16);
            return Endian.SwapUInt16(val);
        }

        public void Read(ref float val) {
            val = ReadFloat();
        }

        public unsafe float ReadFloat() {
            float val;
            Read((byte*)&val, 32);
            return val;
        }

        public unsafe float DecompressFloat(float min, float max, float precision = 0.1f) {
            var inv = 1 / precision;
            var val = DecompressInt((int)(min * inv), (int)(max * inv));
            return val * precision;
        }

        public void Read(ref int val) {
            val = ReadInt();
        }

        public unsafe int ReadInt() {
            var val = new int();
            Read((byte*)&val, 32);
            return Endian.SwapInt32(val);
        }

        public unsafe int DecompressInt(int min, int max) {
            var bits = ComputeRequiredIntBits(min, max);

            var val = new uint();
            Read((byte*)&val, bits);

            return (int)(val + min);
        }

        public void Read(ref uint val) {
            val = ReadUInt();
        }

        public unsafe uint ReadUInt() {
            var val = new uint();
            Read((byte*)&val, 32);
            return Endian.SwapUInt32(val);
        }

        public void Read(ref long val) {
            val = ReadLong();
        }

        public unsafe long ReadLong() {
            var val = new long();
            Read((byte*)&val, 64);
            return Endian.SwapInt64(val);
        }

        public void Read(ref ulong val) {
            val = ReadULong();
        }

        public unsafe ulong ReadULong() {
            var val = new ulong();
            Read((byte*)&val, 64);
            return Endian.SwapUInt64(val);
        }

        public unsafe string ReadString() {
            var length = ReadUShort();
            if (length == 0)
                return "";

            var chars = new char[length];
            fixed (char* charPtr = &chars[0]) {
                Read((byte*)charPtr, length * 16);
            }
            return new string(chars);
        }

        public void Read(ref Vector2 val) {
            val = ReadVector2();
        }

        public Vector2 ReadVector2() {
            var val = new Vector2 {
                x = ReadFloat(),
                y = ReadFloat()
            };
            return val;
        }

        public void Read(ref Vector3 val) {
            val = ReadVector3();
        }

        public Vector3 ReadVector3() {
            var val = new Vector3 {
                x = ReadFloat(),
                y = ReadFloat(),
                z = ReadFloat()
            };
            return val;
        }
        
        public void Read(ref Quaternion val) {
            val = ReadQuaternion();
        }

        public Quaternion ReadQuaternion() {
            uint readedData = ReadUInt();

            int largest = (int)(readedData >> 30);
            float a, b, c;

            const float minimum = -1f / 1.414214f;        // note: 1.0f / sqrt(2)
            const float maximum = +1f / 1.414214f;
            const float delta = maximum - minimum;
            const uint maxIntegerValue = (1 << 10) - 1; // 10 bits
            const float maxIntegerValueF = (float)maxIntegerValue;
            uint integerValue;
            float normalizedValue;
            // a
            integerValue = (readedData >> 20) & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            a = (normalizedValue * delta) + minimum;
            // b
            integerValue = (readedData >> 10) & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            b = (normalizedValue * delta) + minimum;
            // c
            integerValue = readedData & maxIntegerValue;
            normalizedValue = (float)integerValue / maxIntegerValueF;
            c = (normalizedValue * delta) + minimum;

            Quaternion value = Quaternion.identity;
            float d = Mathf.Sqrt(1f - a * a - b * b - c * c);
            value[largest] = d;
            value[(largest + 1) % 4] = a;
            value[(largest + 2) % 4] = b;
            value[(largest + 3) % 4] = c;

            return value;
        }
        
        public void Read(ref Connection val) {
            val = ReadConnection();
        }

        public Connection ReadConnection() {
            // TODO lookup connection
            var id = ReadULong();
            return new Connection(id);
        }
        
        public void Read(ref ISerializable obj) {
            ReadSerializable(obj);
        }

        public void ReadSerializable(ISerializable obj) {
            obj.Deserialize(this);
        }

        public unsafe int Read(byte[] buffer, int count) {
            if (count > buffer.Length * 8)
                throw new Exception("Buffer overflow");

            fixed (byte* dst = &buffer[0]) {
                return Read(dst, count);
            }
        }

        public unsafe int Read(byte* buffer, int count) {
            if (count <= 0)
                return -1;

            if (_readOffset + count > _numberOfBitsUsed)
                throw new IndexOutOfRangeException("Read over end: " + (_readOffset + count) + " > " + _numberOfBitsUsed);

            int readOffsetMod8 = (int)(_readOffset & 7);

            if ((_readOffset & 7) == 0 && (count & 7) == 0) {
                fixed (byte* data = &_data[0]) {
                    memcpy(buffer, data + (_readOffset >> 3), count >> 3);
                }
                _readOffset += count;
                return count;
            }

            memset(buffer, 0, count >> 3);

            int read = 0;
            while (count > 0) {
                buffer[read >> 3] |= (byte)(_data[_readOffset >> 3] << readOffsetMod8);

                if (readOffsetMod8 > 0 && count > (8 - readOffsetMod8))
                    buffer[read >> 3] |= (byte)(_data[(_readOffset >> 3) + 1] >> (8 - readOffsetMod8));

                if (count >= 8) {
                    count -= 8;
                    _readOffset += 8;
                    read += 8;
                } else {
                    int neg = count - 8;
                    if (neg < 0) {
                        buffer[read >> 3] >>= -neg;
                        _readOffset += 8 + neg;
                    } else {
                        _readOffset += 8;
                    }
                    read += count;
                    count = 0;
                }
            }
            return read;
        }
        #endregion

        #region Write
        public unsafe void WriteByte(byte val) {
            Write(&val, 8);
        }

        public unsafe void Write(byte val) {
            Write(&val, 8);
        }

        public unsafe void Write(ushort val) {
            val = Endian.SwapUInt16(val);
            Write((byte*)&val, 16);
        }

        public unsafe void Write(int val) {
            val = Endian.SwapInt32(val);
            Write((byte*)&val, 32);
        }

        public unsafe void CompressInt(int val, int min, int max) {
            if (val < min || val > max) {
#if UNITY_EDITOR
                Debug.LogWarning("Clamped value " + val + " to (" + min + "," + max + ")");
#endif
                val = Mathf.Clamp(val, min, max);
            }

            var bits = ComputeRequiredIntBits(min, max);
            var mask = (uint)((1L << bits) - 1);
            var data = (uint)(val - min) & mask;

            Write((byte*)&data, bits);
        }

        public unsafe void Write(uint val) {
            val = Endian.SwapUInt32(val);
            Write((byte*)&val, 32);
        }

        public unsafe void Write(float val) {
            Write((byte*)&val, 32);
        }

        public unsafe void CompressFloat(float val, float min, float max, float precision = 0.1f) {
            if (val < min || val > max) {
#if UNITY_EDITOR
                Debug.LogWarning("Clamped value " + val + " to (" + min + "," + max + ")");
#endif
                val = Mathf.Clamp(val, min, max);
            }

            var inv = 1 / precision;
            CompressInt((int)(val * inv), (int)(min * inv), (int)(max * inv));
        }

        public unsafe void Write(long val) {
            val = Endian.SwapInt64(val);
            Write((byte*)&val, 64);
        }

        public unsafe void Write(ulong val) {
            val = Endian.SwapUInt64(val);
            Write((byte*)&val, 64);
        }

        public unsafe void Write(bool val) {
            if (val)
                Write1();
            else
                Write0();
        }

        public unsafe void Write(string val) {
            var chars = val.ToCharArray();
            Write((ushort)chars.Length);

            if (chars.Length == 0)
                return;

            fixed (char* charPtr = &chars[0]) {
                Write((byte*)charPtr, chars.Length * 16);
            }
        }

        public void Write(Vector2 val) {
            Write(val.x);
            Write(val.y);
        }

        public void Write(Vector3 val) {
            Write(val.x);
            Write(val.y);
            Write(val.z);
        }
        
        public void Write(Quaternion val) {
            int largest = 0;
            float a, b, c;

            float abs_w = Mathf.Abs(val.w);
            float abs_x = Mathf.Abs(val.x);
            float abs_y = Mathf.Abs(val.y);
            float abs_z = Mathf.Abs(val.z);

            float largest_value = abs_x;

            if (abs_y > largest_value) {
                largest = 1;
                largest_value = abs_y;
            }
            if (abs_z > largest_value) {
                largest = 2;
                largest_value = abs_z;
            }
            if (abs_w > largest_value) {
                largest = 3;
                largest_value = abs_w;
            }
            if (val[largest] >= 0f) {
                a = val[(largest + 1) % 4];
                b = val[(largest + 2) % 4];
                c = val[(largest + 3) % 4];
            } else {
                a = -val[(largest + 1) % 4];
                b = -val[(largest + 2) % 4];
                c = -val[(largest + 3) % 4];
            }

            // serialize
            const float minimum = -1f / 1.414214f;        // note: 1.0f / sqrt(2)
            const float maximum = +1f / 1.414214f;
            const float delta = maximum - minimum;
            const uint maxIntegerValue = (1 << 10) - 1; // 10 bits
            const float maxIntegerValueF = (float)maxIntegerValue;
            float normalizedValue;
            uint integerValue;

            uint sentData = ((uint)largest) << 30;
            // a
            normalizedValue = Mathf.Clamp01((a - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | ((integerValue & maxIntegerValue) << 20);
            // b
            normalizedValue = Mathf.Clamp01((b - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | ((integerValue & maxIntegerValue) << 10);
            // c
            normalizedValue = Mathf.Clamp01((c - minimum) / delta);
            integerValue = (uint)Mathf.Floor(normalizedValue * maxIntegerValueF + 0.5f);
            sentData = sentData | (integerValue & maxIntegerValue);

            Write(sentData);
        }
        
        public void Write(Connection connection) {
            Write(connection.id);
        }
        
        /// <summary>
        /// Write float in the range [0,1].
        /// </summary>
        public void WriteNormalised(float val) {
            Assert.IsTrue(val > -1.01f && val < 1.01f);

            val = Mathf.Clamp(val, -1f, 1f);
            Write((ushort)((val + 1f) * 32767.5f));
        }

        public void Write(ISerializable obj) {
            obj.Serialize(this);
        }

        public unsafe void Write(BitStream bs) {
            Resize(bs.LengthInBits);

            int readBitPos = 0;

            if ((_numberOfBitsUsed & 7) == 0) {
                int bytes = (int)(bs.LengthInBits >> 3);
                fixed (byte* data = &_data[0]) {
                    fixed (byte* otherData = &bs._data[0]) {
                        memcpy(data + (_numberOfBitsUsed >> 3), otherData, bytes);
                    }
                }
                _numberOfBitsUsed += bytes << 3;
                readBitPos += bytes << 3;
            }

            int numberOfBitsUsedMod8 = 0;
            while (readBitPos < bs.LengthInBits) {
                numberOfBitsUsedMod8 = (int)(_numberOfBitsUsed & 7);

                int bytePos = (int)(readBitPos & 7);

                if (numberOfBitsUsedMod8 == 0) {
                    // New byte
                    if ((bs._data[readBitPos >> 3] & (0x80 >> bytePos)) != 0)
                        data[_numberOfBitsUsed >> 3] = 0x80;
                    //else 
                    //    data[_numberOfBitsUsed >> 3] = 0;
                } else {
                    // Existing byte
                    if ((bs._data[readBitPos >> 3] & (0x80 >> bytePos)) != 0)
                        data[_numberOfBitsUsed >> 3] |= (byte)(0x80 >> (numberOfBitsUsedMod8));
                    //else
                    //    data[_numberOfBitsUsed >> 3] = 0;
                }

                _numberOfBitsUsed++;
                readBitPos++;
            }
        }

        public unsafe void Write(byte* inByteArray, int numberOfBitsToWrite) {
            Resize(numberOfBitsToWrite);

            int numberOfBitsUsedMod8 = (int)(_numberOfBitsUsed & 7);

            if (numberOfBitsUsedMod8 == 0 && (numberOfBitsToWrite & 7) == 0) {
                fixed (byte* data = &_data[0]) {
                    memcpy(data + (_numberOfBitsUsed >> 3), inByteArray, numberOfBitsToWrite >> 3);
                }
                _numberOfBitsUsed += numberOfBitsToWrite;
                return;
            }

            byte dataByte;
            int write = 0;

            while (numberOfBitsToWrite > 0) {
                dataByte = inByteArray[write++];

                if (numberOfBitsToWrite < 8)
                    dataByte <<= 8 - numberOfBitsToWrite;

                if (numberOfBitsUsedMod8 == 0) {
                    _data[_numberOfBitsUsed >> 3] = dataByte;
                } else {
                    _data[_numberOfBitsUsed >> 3] |= (byte)(dataByte >> numberOfBitsUsedMod8);

                    if (8 - (numberOfBitsUsedMod8) < 8 && 8 - (numberOfBitsUsedMod8) < numberOfBitsToWrite)
                        _data[(_numberOfBitsUsed >> 3) + 1] = (byte)(dataByte << (8 - numberOfBitsUsedMod8));
                }

                if (numberOfBitsToWrite >= 8) {
                    _numberOfBitsUsed += 8;
                    numberOfBitsToWrite -= 8;
                } else {
                    _numberOfBitsUsed += numberOfBitsToWrite;
                    numberOfBitsToWrite = 0;
                }
            }
        }

        void Write0() {
            Resize(1);

            if ((_numberOfBitsUsed & 7) == 0) {
                _data[_numberOfBitsUsed >> 3] = 0;
            }
            ++_numberOfBitsUsed;
        }

        void Write1() {
            Resize(1);

            int numberOfBitsMod8 = (int)(_numberOfBitsUsed & 7);
            if (numberOfBitsMod8 == 0)
                _data[_numberOfBitsUsed >> 3] = 0x80;
            else
                _data[_numberOfBitsUsed >> 3] |= (byte)(0x80 >> (numberOfBitsMod8));

            _numberOfBitsUsed++;
        }
        #endregion

        void Resize(int numberOfBitsToWrite) {
            if (numberOfBitsToWrite == 0)
                return;

            int newNumberOfBytesAllocated = BitsToBytes(_numberOfBitsUsed + numberOfBitsToWrite) + 128;
            if (_data.Length < newNumberOfBytesAllocated) {
                Array.Resize(ref _data, newNumberOfBytesAllocated);
            }
        }

        public override string ToString() {
            var buffer = new byte[Length];
            Array.Copy(_data, buffer, Length);

            var s = BitConverter.ToString(buffer).Replace("-", " ");
            return "{bits = " + LengthInBits + ", offset = " + Position + ", data = " + s + "}";
        }

        static int BytesToBits(int bytes) {
            return bytes << 3;
        }

        static int BitsToBytes(int bits) {
            return (int)((bits >> 3) + ((bits & 7) == 0 ? 0 : 1));
        }

        int ComputeRequiredFloatBits(float min, float max, float precision) {
            float range = max - min;
            float maxVal = range * precision;
            return FastMath.Log2((uint)(maxVal + 0.5f)) + 1;
        }

        int ComputeRequiredIntBits(int min, int max) {
            if (min > max)
                return 0;

            var minLong = (long)min;
            var maxLong = (long)max;
            uint range = (uint)(maxLong - minLong);
            return FastMath.Log2(range) + 1;
        }

        [DllImport("msvcrt.dll", SetLastError = false)]
        static unsafe extern void* memcpy(void* dest, void* src, int count);

        [DllImport("msvcrt.dll", SetLastError = false)]
        static unsafe extern void* memset(void* dest, int c, int count);
    }
}
