using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public class BitStream {
        byte[] data;
        int numBitsUsed = 0;
        int readBitOffset = 0;

        public byte[] Data => data;

        /// <summary>
        /// Current write position in bits.
        /// </summary>
        public int LengthInBits => numBitsUsed;

        public int Length => BitsToBytes(numBitsUsed);

        /// <summary>
        /// Have we read all of its content?
        /// </summary>
        public bool IsExhausted => readBitOffset >= numBitsUsed;

        int Capacity => data.Length;

        /// <summary>
        /// Current read position in bits.
        /// </summary>
        public int Position {
            get { return readBitOffset; }
            set {
                if (value > numBitsUsed)
                    throw new IndexOutOfRangeException();

                readBitOffset = value;
            }
        }

        public BitStream(int lengthInBytes = 64) {
            data = new byte[lengthInBytes];
        }

        BitStream(byte[] buffer, int offsetInBits, int lengthInBits) {
            data = buffer;
            readBitOffset = offsetInBits;
            numBitsUsed = lengthInBits;
        }

        public static BitStream CreateWithExistingBuffer(byte[] data, int offsetInBits, int lengthInBits) {
            Assert.IsTrue(lengthInBits <= data.Length * 8);
            Assert.IsTrue(offsetInBits <= data.Length * 8, $"Offset {offsetInBits} over {data.Length * 8}");
            return new BitStream(data, offsetInBits, lengthInBits);
        }

        public override string ToString() {
            static string HexStr(byte[] data, int offset, int len, bool space = false) {
                char[] hexchar = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

                int i = 0, k = 2;
                if (space)
                    k++;
                var c = new char[len * k];
                while (i < len) {
                    byte d = data[offset + i];
                    c[i * k] = hexchar[d / 0x10];
                    c[i * k + 1] = hexchar[d % 0x10];
                    if (space && i < len - 1)
                        c[i * k + 2] = ' ';
                    i++;
                }
                return new string(c, 0, c.Length - 1);
            }

            var str = "{" + HexStr(Data, 0, Length, true) + "}";
            return str;
        }

        public void Reset() {
            numBitsUsed = 0;
            readBitOffset = 0;
        }

        public void ResetWithBuffer(byte[] data, int lengthInBits) {
            var lengthInBytes = BitsToBytes(lengthInBits);
            if (lengthInBytes > Capacity) {
                Array.Resize(ref data, lengthInBytes);
            }

            Array.Copy(data, 0, data, 0, lengthInBytes);
            numBitsUsed = lengthInBits;
        }

        public void AlignWriteToByteBoundary() {
            numBitsUsed += 8 - (((numBitsUsed - 1) & 7) + 1);
        }

        public void AlignReadToByteBoundary() {
            readBitOffset += 8 - (((readBitOffset - 1) & 7) + 1);
        }


        public unsafe void Write(bool val) {
            if (val)
                Write1();
            else
                Write0();
        }

        public void Read(ref bool val) {
            val = ReadBool();
        }

        public unsafe bool ReadBool() {
            var val = new bool();
            Read((byte*)&val, 1);
            return val;
        }


        public unsafe void Write(byte val) {
            Write(&val, 8);
        }

        public void Read(ref byte val) {
            val = ReadByte();
        }

        public unsafe byte ReadByte() {
            var val = new byte();
            Read(&val, 8);
            return val;
        }


        public unsafe void Write(ushort val) {
            val = Endian.SwapUInt16(val);
            Write((byte*)&val, 16);
        }

        public void Read(ref ushort val) {
            val = ReadUShort();
        }

        public unsafe ushort ReadUShort() {
            var val = new ushort();
            Read((byte*)&val, 16);
            return Endian.SwapUInt16(val);
        }


        public unsafe void Write(float val) {
            Write((byte*)&val, 32);
        }

        public void Read(ref float val) {
            val = ReadFloat();
        }

        public unsafe float ReadFloat() {
            float val;
            Read((byte*)&val, 32);
            return val;
        }


        public unsafe void WriteLossyFloat(float val, float min, float max, float precision = 0.1f) {
            if (val < min || val > max) {
                val = Mathf.Clamp(val, min, max);
            }

            var inv = 1 / precision;
            WriteIntInRange((int)(val * inv), (int)(min * inv), (int)(max * inv));
        }

        public unsafe float ReadLossyFloat(float min, float max, float precision = 0.1f) {
            var inv = 1 / precision;
            var val = ReadIntInRange((int)(min * inv), (int)(max * inv));
            return val * precision;
        }


        /// <summary>
        /// Write float in the range [0,1].
        /// </summary>
        public void WriteNormalised(float val) {
            Assert.IsTrue(val > -1.01f && val < 1.01f);

            val = Mathf.Clamp(val, -1f, 1f);
            Write((ushort)((val + 1f) * 32767.5f));
        }

        public float ReadNormalisedFloat() {
            var val = ReadUShort();
            var result = (val / 32767.5f) - 1f;
            result = Mathf.Clamp(result, 0, 1);
            return result;
        }

        public void Read(ref int val) {
            val = ReadInt();
        }


        public unsafe void Write(int val) {
            val = Endian.SwapInt32(val);
            Write((byte*)&val, 32);
        }

        public unsafe int ReadInt() {
            var val = new int();
            Read((byte*)&val, 32);
            return Endian.SwapInt32(val);
        }


        public unsafe void WriteIntInRange(int val, int min, int max) {
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

        public unsafe int ReadIntInRange(int min, int max) {
            var bits = ComputeRequiredIntBits(min, max);

            var val = new uint();
            Read((byte*)&val, bits);

            return (int)(val + min);
        }


        public void Read(ref uint val) {
            val = ReadUInt();
        }


        public unsafe void Write(uint val) {
            val = Endian.SwapUInt32(val);
            Write((byte*)&val, 32);
        }

        public unsafe uint ReadUInt() {
            var val = new uint();
            Read((byte*)&val, 32);
            return Endian.SwapUInt32(val);
        }


        public unsafe void Write(long val) {
            val = Endian.SwapInt64(val);
            Write((byte*)&val, 64);
        }

        public void Read(ref long val) {
            val = ReadLong();
        }

        public unsafe long ReadLong() {
            var val = new long();
            Read((byte*)&val, 64);
            return Endian.SwapInt64(val);
        }


        public unsafe void Write(ulong val) {
            val = Endian.SwapUInt64(val);
            Write((byte*)&val, 64);
        }

        public void Read(ref ulong val) {
            val = ReadULong();
        }

        public unsafe ulong ReadULong() {
            var val = new ulong();
            Read((byte*)&val, 64);
            return Endian.SwapUInt64(val);
        }


        public unsafe void Write(string val) {
            var chars = val.ToCharArray();

            if (chars.Length <= 32) {
                Write(true);
                WriteIntInRange(chars.Length, 0, 32);
            } else if (chars.Length <= 256) {
                Write(false);
                Write(true);
                WriteIntInRange(chars.Length, 0, 256);
            } else {
                Write(false);
                Write(false);
                Write((ushort)chars.Length);
            }

            if (chars.Length > 0) {
                fixed (char* charPtr = &chars[0]) {
                    Write((byte*)charPtr, chars.Length * 16);
                }
            }
        }

        public unsafe string ReadString() {
            var length = 0;

            var under32 = ReadBool();
            if (under32) {
                length = ReadIntInRange(0, 32);
            } else {
                var under256 = ReadBool();
                if (under256) {
                    length = ReadIntInRange(0, 256);
                } else {
                    length = ReadUShort();
                }
            }

            if (length == 0)
                return "";

            var chars = new char[length];
            fixed (char* charPtr = &chars[0]) {
                Read((byte*)charPtr, length * 16);
            }
            return new string(chars);
        }


        public void Write(Vector2 val) {
            Write(val.x);
            Write(val.y);
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


        public void Write(Vector3 val) {
            Write(val.x);
            Write(val.y);
            Write(val.z);
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


        public void WriteNormalised(Vector3 val) {
            WriteNormalised(val.x);
            WriteNormalised(val.y);
            WriteNormalised(val.z);
        }

        public Vector3 ReadNormalisedVector3() {
            var result = Vector3.zero;
            result.x = ReadNormalisedFloat();
            result.y = ReadNormalisedFloat();
            result.z = ReadNormalisedFloat();
            return result;
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
            const float maxIntegerValueF = maxIntegerValue;
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


        public void Write(Connection connection) {
            Write(connection.id);
        }

        public Connection ReadConnection() {
            var id = ReadULong();
            return new Connection(id);
        }


        public void Write(ISerializable obj) {
            obj.Serialize(this);
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
            Assert.IsTrue(count > 0);

            if (readBitOffset + count > numBitsUsed)
                throw new InvalidOperationException("BitStream exhausted");

            var readOffsetMod8 = readBitOffset & 7;
            if ((readBitOffset & 7) == 0 && (count & 7) == 0) {
                fixed (byte* Data = &data[0]) {
                    memcpy(buffer, Data + (readBitOffset >> 3), count >> 3);
                }
                readBitOffset += count;
                return count;
            }

            memset(buffer, 0, count >> 3);

            int read = 0;
            while (count > 0) {
                buffer[read >> 3] |= (byte)(data[readBitOffset >> 3] << readOffsetMod8);

                if (readOffsetMod8 > 0 && count > (8 - readOffsetMod8))
                    buffer[read >> 3] |= (byte)(data[(readBitOffset >> 3) + 1] >> (8 - readOffsetMod8));

                if (count >= 8) {
                    count -= 8;
                    readBitOffset += 8;
                    read += 8;
                } else {
                    int neg = count - 8;
                    if (neg < 0) {
                        buffer[read >> 3] >>= -neg;
                        readBitOffset += 8 + neg;
                    } else {
                        readBitOffset += 8;
                    }
                    read += count;
                    count = 0;
                }
            }
            return read;
        }


        public void Write(BitStream other) {
            Write(other, other.LengthInBits - other.Position);
        }

        public unsafe void Write(BitStream other, int numBits) {
            Assert.IsTrue(other != this);

            if (numBits == 0)
                return;

            Resize(numBits);

            if ((other.Position & 7) == 0 && (numBitsUsed & 7) == 0) {
                int readOffsetBytes = other.Position / 8;
                int numBytes = numBits / 8;

                fixed (byte* Data = &data[0]) {
                    fixed (byte* otherData = &other.data[0]) {
                        memcpy(Data + (numBitsUsed >> 3), otherData + readOffsetBytes, numBytes);
                    }
                }

                numBits -= BytesToBits(numBytes);
                other.Position = BytesToBits(numBytes + readOffsetBytes);
                numBitsUsed += BytesToBits(numBytes);
            }

            int numberOfBitsUsedMod8 = 0;
            while (numBits-- > 0 && other.Position + 1 <= other.LengthInBits) {
                numberOfBitsUsedMod8 = numBitsUsed & 7;
                if (numberOfBitsUsedMod8 == 0) {
                    // New byte
                    if ((other.data[other.Position >> 3] & (0x80 >> (other.Position & 7))) != 0) {
                        // Write 1
                        Data[numBitsUsed >> 3] = 0x80;
                    } else {
                        // Write 0
                        Data[numBitsUsed >> 3] = 0;
                    }
                } else {
                    // Existing byte
                    if ((other.data[other.Position >> 3] & (0x80 >> (other.Position & 7))) != 0) {
                        // Set bit to 1
                        Data[numBitsUsed >> 3] |= (byte)(0x80 >> (numberOfBitsUsedMod8));
                    }
                }

                other.Position++;
                numBitsUsed++;
            }
        }

        public unsafe void Write(byte* inByteArray, int numberOfBitsToWrite) {
            Resize(numberOfBitsToWrite);

            int numberOfBitsUsedMod8 = (int)(numBitsUsed & 7);

            if (numberOfBitsUsedMod8 == 0 && (numberOfBitsToWrite & 7) == 0) {
                fixed (byte* Data = &data[0]) {
                    memcpy(Data + (numBitsUsed >> 3), inByteArray, numberOfBitsToWrite >> 3);
                }
                numBitsUsed += numberOfBitsToWrite;
                return;
            }

            byte dataByte;
            int write = 0;

            while (numberOfBitsToWrite > 0) {
                dataByte = inByteArray[write++];

                if (numberOfBitsToWrite < 8)
                    dataByte <<= 8 - numberOfBitsToWrite;

                if (numberOfBitsUsedMod8 == 0) {
                    data[numBitsUsed >> 3] = dataByte;
                } else {
                    data[numBitsUsed >> 3] |= (byte)(dataByte >> numberOfBitsUsedMod8);

                    if (8 - (numberOfBitsUsedMod8) < 8 && 8 - (numberOfBitsUsedMod8) < numberOfBitsToWrite)
                        data[(numBitsUsed >> 3) + 1] = (byte)(dataByte << (8 - numberOfBitsUsedMod8));
                }

                if (numberOfBitsToWrite >= 8) {
                    numBitsUsed += 8;
                    numberOfBitsToWrite -= 8;
                } else {
                    numBitsUsed += numberOfBitsToWrite;
                    numberOfBitsToWrite = 0;
                }
            }
        }

        void Write0() {
            Resize(1);
            ++numBitsUsed;
        }

        void Write1() {
            Resize(1);

            int numberOfBitsMod8 = numBitsUsed & 7;
            if (numberOfBitsMod8 == 0) {
                data[numBitsUsed >> 3] = 0x80;
            } else {
                data[numBitsUsed >> 3] |= (byte)(0x80 >> (numberOfBitsMod8));
            }

            ++numBitsUsed;
        }

        public static float NormaliseFloat(float val, float precision = 0.1f) {
            var inv = 1 / precision;
            var temp = (int)(val * inv);
            return temp * precision;
        }

        void Resize(int numberOfBitsToWrite) {
            if (numberOfBitsToWrite == 0)
                return;

            int newNumberOfBytesAllocated = BitsToBytes(numBitsUsed + numberOfBitsToWrite) + 128;
            if (data.Length < newNumberOfBytesAllocated) {
                Array.Resize(ref data, newNumberOfBytesAllocated);
            }
        }

        static int BytesToBits(int bytes) {
            return bytes << 3;
        }

        static int BitsToBytes(int bits) {
            return (bits >> 3) + ((bits & 7) == 0 ? 0 : 1);
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
