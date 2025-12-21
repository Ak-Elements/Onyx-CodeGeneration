using Onyx.CodeGen.Core.Math;

namespace Onyx.CodeGen.ComponentDSL
{
    public static class DSLTypes
    {
        public static Dictionary<string, string> TYPE_TO_LITERAL_SUFFIX = new Dictionary<string, string>()
        {
            { "onyxU8", "u" },
            { "onyxU16", "u" },
            { "onyxU32", "u" },
            { "onyxU64", "ull" },
            { "onyxU128", "ull" },
            { "onyxF32", "f" },
            { "onyxF64", "d"  },
            { "Vector2u8", "u" },
            { "Vector2u16", "u" },
            { "Vector2u32", "u" },
            { "Vector2u64", "u" },
            { "Vector2f32", "f" },
            { "Vector2f64", "d" },
            { "Vector3u8", "u" },
            { "Vector3u16", "u" },
            { "Vector3u32", "u" },
            { "Vector3u64", "u" },
            { "Vector3f32", "f" },
            { "Vector3f64", "d" },
            { "Vector4u8", "u" },
            { "Vector4u16", "u" },
            { "Vector4u32", "u" },
            { "Vector4u64", "u" },
            { "Vector4f32", "f" },
            { "Vector4f64", "d" },
        };
    }
}
