using System.Runtime.InteropServices;
using UnityEngine.Assertions;

namespace Cube.Transport {
    public static class BitUtil {
        [StructLayout(LayoutKind.Explicit)]
        struct ConverterHelperFloat {
            [FieldOffset(0)]
            public uint U32;
            [FieldOffset(0)]
            public float F32;
        }

        public static uint CastFloatToUInt(float value) {
            var helper = new ConverterHelperFloat();
            helper.F32 = value;
            return helper.U32;
        }

        public static float CastUIntToFloat(uint value) {
            var helper = new ConverterHelperFloat();
            helper.U32 = value;
            return helper.F32;
        }

        public static float NormaliseFloat(float val, float precision = 0.1f) {
            var inv = 1 / precision;
            var temp = (int)(val * inv);
            return temp * precision;
        }

        public static int ComputeRequiredFloatBits(float min, float max, float precision) {
            float range = max - min;
            float maxVal = range * precision;
            return FastMath.Log2((uint)(maxVal + 0.5f)) + 1;
        }

        public static int ComputeRequiredIntBits(int min, int max) {
            Assert.IsTrue(min <= max);

            var minLong = (long)min;
            var maxLong = (long)max;
            uint range = (uint)(maxLong - minLong);
            return FastMath.Log2(range) + 1;
        }
    }
}