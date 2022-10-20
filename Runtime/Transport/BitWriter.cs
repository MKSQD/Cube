using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public sealed class BitWriter : IBitWriter {
        // All goodness from yojimbo network library. All bugs belong to us.

        public ReadOnlySpan<byte> DataWritten {
            get {
                Assert.IsTrue(_scratchBits == 0, "call FlushBits first");
                return MemoryMarshal.AsBytes(_data.Span).Slice(0, BytesWritten);
            }
        }

        public Memory<uint> RawData => _data;

        public int AlignBits => (8 - (_bitsWritten % 8)) % 8;

        public int BitsWritten => _bitsWritten;

        public int BitsAvailable => _numBits - _bitsWritten;

        public int BytesWritten {
            get {
                Assert.IsTrue(_scratchBits == 0, "call FlushBits first");
                return (_bitsWritten + 7) / 8;
            }
        }

        ulong _scratch = 0;
        int _scratchBits = 0;
        int _wordIndex = 0;
        int _bitsWritten = 0;
        int _numBits => _data.Length * 32;
        Memory<uint> _data;

        public BitWriter(int numWords = 16) {
            _data = new uint[numWords];
        }

        public BitWriter(Memory<uint> preallocatedMemory) {
            Assert.IsTrue(preallocatedMemory.Length > 0);
            _data = preallocatedMemory;
        }

        public void WriteAlign() {
            int remainderBits = _bitsWritten % 8;
            if (remainderBits != 0) {
                uint zero = 0;
                WriteBits(zero, 8 - remainderBits);

                Assert.IsTrue((_bitsWritten % 8) == 0);
            }
        }

        public void FlushBits() {
            if (_scratchBits != 0) {
                Assert.IsTrue(_scratchBits <= 32);
                Assert.IsTrue(_wordIndex < _data.Length);

                _data.Span[_wordIndex] = host_to_network((uint)(_scratch & 0xFFFFFFFF));
                _scratch >>= 32;
                _scratchBits = 0;
                _wordIndex++;
            }
        }

        public void Clear() {
            _scratch = 0;
            _scratchBits = 0;
            _wordIndex = 0;
            _bitsWritten = 0;
        }


        public void WriteBool(bool val) => WriteBits((uint)(val ? 0x00000001 : 0x00000000), 1);
        public void WriteByte(byte val) => WriteBits((uint)val, 8);

        public void WriteByte(int val) {
            Assert.IsTrue(val >= 0);
            Assert.IsTrue(val < 255);
            WriteByte((byte)val);
        }

        public void WriteUShort(ushort value) => WriteBits(value, 16);
        public void WriteFloat(float value) => WriteBits(BitUtil.CastFloatToUInt(value), 32);


        public void WriteLossyFloat(float value, float min, float max, float precision = 0.1f) {
            value = Mathf.Clamp(value, min, max);

            var inv = 1 / precision;
            WriteIntInRange((int)(value * inv), (int)(min * inv), (int)(max * inv));
        }

        public static float QuantizeFloat(float value, float min, float max, float precision = 0.1f) {
            value = Mathf.Clamp(value, min, max);

            var inv = 1 / precision;

            var bits = BitUtil.ComputeRequiredIntBits((int)(min * inv), (int)(max * inv));
            var mask = (uint)((1L << bits) - 1);
            var data = (uint)((int)(value * inv) - (int)(min * inv)) & mask;

            return (data + (int)(min * inv)) * precision;
        }

        public void WriteInt(int value) => WriteBits((uint)value, 32);

        public void WriteIntInRange(int value, int minInclusive, int maxInclusive) {
#if UNITY_EDITOR
            if (value < minInclusive || value > maxInclusive) {

                Debug.LogWarning("Clamped value " + value + " to (" + minInclusive + "," + maxInclusive + ")");
            }
#endif
            value = Mathf.Clamp(value, minInclusive, maxInclusive);

            var bits = BitUtil.ComputeRequiredIntBits(minInclusive, maxInclusive);
            var data = (uint)(value - minInclusive) & (uint)((1L << bits) - 1);
            WriteBits(data, bits);
        }

        public void WriteUInt(uint value) => WriteBits(value, 32);

        public void WriteString(string value) {
            if (value.Length <= 32) {
                WriteBool(true);
                WriteIntInRange(value.Length, 0, 32);
            } else if (value.Length <= 256) {
                WriteBool(false);
                WriteBool(true);
                WriteIntInRange(value.Length, 0, 256);
            } else {
                WriteBool(false);
                WriteBool(false);
                WriteUShort((ushort)value.Length);
            }

            for (int i = 0; i < value.Length; ++i) {
                WriteIntInRange(value[i], 0x0020, 0x007E);
            }
        }

        public void WriteVector2(Vector2 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
        }

        public void WriteVector3(Vector3 value) {
            WriteFloat(value.x);
            WriteFloat(value.y);
            WriteFloat(value.z);
        }

        public static Quaternion QuantizeQuaternion(Quaternion value) {
            uint readData;
            {
                int largest = 0;
                float a, b, c;

                float abs_w = Mathf.Abs(value.w);
                float abs_x = Mathf.Abs(value.x);
                float abs_y = Mathf.Abs(value.y);
                float abs_z = Mathf.Abs(value.z);

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
                if (value[largest] >= 0f) {
                    a = value[(largest + 1) % 4];
                    b = value[(largest + 2) % 4];
                    c = value[(largest + 3) % 4];
                } else {
                    a = -value[(largest + 1) % 4];
                    b = -value[(largest + 2) % 4];
                    c = -value[(largest + 3) % 4];
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

                readData = sentData;
            }
            {
                int largest = (int)(readData >> 30);
                float a, b, c;

                const float minimum = -1f / 1.414214f;        // note: 1.0f / sqrt(2)
                const float maximum = +1f / 1.414214f;
                const float delta = maximum - minimum;
                const uint maxIntegerValue = (1 << 10) - 1; // 10 bits
                const float maxIntegerValueF = (float)maxIntegerValue;
                uint integerValue;
                float normalizedValue;
                // a
                integerValue = (readData >> 20) & maxIntegerValue;
                normalizedValue = (float)integerValue / maxIntegerValueF;
                a = (normalizedValue * delta) + minimum;
                // b
                integerValue = (readData >> 10) & maxIntegerValue;
                normalizedValue = (float)integerValue / maxIntegerValueF;
                b = (normalizedValue * delta) + minimum;
                // c
                integerValue = readData & maxIntegerValue;
                normalizedValue = (float)integerValue / maxIntegerValueF;
                c = (normalizedValue * delta) + minimum;

                Quaternion result = Quaternion.identity;
                float d = Mathf.Sqrt(1f - a * a - b * b - c * c);
                result[largest] = d;
                result[(largest + 1) % 4] = a;
                result[(largest + 2) % 4] = b;
                result[(largest + 3) % 4] = c;

                return result;
            }
        }

        public void WriteQuaternion(Quaternion value) {
            int largest = 0;
            float a, b, c;

            float abs_w = Mathf.Abs(value.w);
            float abs_x = Mathf.Abs(value.x);
            float abs_y = Mathf.Abs(value.y);
            float abs_z = Mathf.Abs(value.z);

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
            if (value[largest] >= 0f) {
                a = value[(largest + 1) % 4];
                b = value[(largest + 2) % 4];
                c = value[(largest + 3) % 4];
            } else {
                a = -value[(largest + 1) % 4];
                b = -value[(largest + 2) % 4];
                c = -value[(largest + 3) % 4];
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

            WriteUInt(sentData);
        }

        public void WriteSerializable(IBitSerializable obj) {
            obj.Serialize(this);
        }

        void WriteBits(uint value, int bits) {
            Assert.IsTrue(bits > 0);
            Assert.IsTrue(bits <= 32);
            Assert.IsTrue(_bitsWritten + bits <= _numBits, "exhausted");
            Assert.IsTrue(((ulong)value) <= (((ulong)(1) << bits) - 1));

            _scratch |= (ulong)(value) << _scratchBits;
            _scratchBits += bits;

            if (_scratchBits >= 32) {
                Assert.IsTrue(_wordIndex < _data.Length);
                _data.Span[_wordIndex] = host_to_network((uint)_scratch & 0xFFFFFFFF);
                _scratch >>= 32;
                _scratchBits -= 32;
                _wordIndex++;
            }

            _bitsWritten += bits;
        }

        void WriteBytes(Span<byte> data) {
            Assert.IsTrue(AlignBits == 0);
            Assert.IsTrue(_bitsWritten + data.Length * 8 <= _numBits);
            Assert.IsTrue((_bitsWritten % 32) == 0 || (_bitsWritten % 32) == 8 || (_bitsWritten % 32) == 16 || (_bitsWritten % 32) == 24);

            int headBytes = (4 - (_bitsWritten % 32) / 8) % 4;
            if (headBytes > data.Length)
                headBytes = data.Length;
            for (int i = 0; i < headBytes; ++i)
                WriteBits(data[i], 8);
            if (headBytes == data.Length)
                return;

            FlushBits();

            Assert.IsTrue(AlignBits == 0);

            int numWords = (data.Length - headBytes) / 4;
            if (numWords > 0) {
                Assert.IsTrue((_bitsWritten % 32) == 0);

                data.Slice(headBytes, numWords * 4).CopyTo(MemoryMarshal.AsBytes(_data.Span.Slice(_wordIndex)));
                //memcpy(&m_data[m_wordIndex], data + headBytes, numWords * 4);
                _bitsWritten += numWords * 32;
                _wordIndex += numWords;
                _scratch = 0;
            }

            Assert.IsTrue(AlignBits == 0);

            int tailStart = headBytes + numWords * 4;
            int tailBytes = data.Length - tailStart;
            Assert.IsTrue(tailBytes >= 0 && tailBytes < 4);
            for (int i = 0; i < tailBytes; ++i)
                WriteBits(data[tailStart + i], 8);

            Assert.IsTrue(AlignBits == 0);

            Assert.IsTrue(headBytes + numWords * 4 + tailBytes == data.Length);
        }

        // #todo
        static uint host_to_network(uint value) => value;
    }
}
