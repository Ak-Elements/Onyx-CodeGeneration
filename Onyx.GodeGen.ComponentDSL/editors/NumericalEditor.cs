using Onyx.CodeGen.Core;
using Onyx.CodeGen.Core.Math;

namespace Onyx.CodeGen.ComponentDSL
{
    [AllowedTypes("onyxU8",
            "onyxU16",
            "onyxU32",
            "onyxU64",
            "onyxU128",
            "onyxS8",
            "onyxS16",
            "onyxS32",
            "onyxS64",
            "onyxS128",
            "onyxF32",
            "onyxF64",
            "Vector2u8",
            "Vector2u16",
            "Vector2u32",
            "Vector2u64",
            "Vector2s8",
            "Vector2s16",
            "Vector2s32",
            "Vector2s64",
            "Vector2f32",
            "Vector2f64",
            "Vector3u8",
            "Vector3u16",
            "Vector3u32",
            "Vector3u64",
            "Vector3s8",
            "Vector3s16",
            "Vector3s32",
            "Vector3s64",
            "Vector3f32",
            "Vector3f64",
            "Vector4u8",
            "Vector4u16",
            "Vector4u32",
            "Vector4u64",
            "Vector4s8",
            "Vector4s16",
            "Vector4s32",
            "Vector4s64",
            "Vector4f32",
            "Vector4f64")
    ]
    internal class NumericalEditor : IFieldEditor
    {
        public void Generate(CodeGenerator codeGenerator, Field field)
        {
            object? min = null;
            object? max = null;
            if (field.GetAttribute<Range>() is Range range)
            {
                min = range.Min;
                max = range.Max;
            }

            if (field.GetAttribute<Min>() is Min minAttribute)
            {
                min = minAttribute.Value;
            }

            if (field.GetAttribute<Max>() is Max maxAttribute)
            {
                max = maxAttribute.Value;
            }

            List<string> numericOptions = new List<string>();
            if (min != null)
            {
                numericOptions.Add($".Min = {min.ToString()}");
            }

            if (max != null)
            {
                numericOptions.Add($".Max = {max.ToString()}");
            }

            if (numericOptions.Any())
            {
                codeGenerator.Append($"isModified |= PropertyGrid::DrawProperty(\"{field.DisplayName}\", {field.Name}, {{ { string.Join(", " ,numericOptions) } }} );");
            }
            else
            {
                codeGenerator.Append($"isModified |= PropertyGrid::DrawProperty(\"{field.DisplayName}\", {field.Name});");
            }
        }  
    }
}
