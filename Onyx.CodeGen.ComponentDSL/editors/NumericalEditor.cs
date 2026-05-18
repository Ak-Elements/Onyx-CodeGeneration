using Onyx.CodeGen.Core;
using Onyx.CodeGen.Core.Math;

namespace Onyx.CodeGen.ComponentDSL
{
    [AllowedTypes("uint8_t",
            "uint16_t",
            "uint32_t",
            "uint64_t",
            "onyxU128",
            "int8_t",
            "int16_t",
            "int32_t",
            "int64_t",
            "onyxS128",
            "float32",
            "float64",
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
        public void Generate(CodeGenerator codeGenerator, string fieldName, Field field)
        {
            object? min = null;
            object? max = null;
            if (field.GetAttribute<RangeAttribute>() is RangeAttribute range)
            {
                min = range.Min;
                max = range.Max;
            }

            if (field.GetAttribute<MinAttribute>() is MinAttribute minAttribute)
            {
                min = minAttribute.Value;
            }

            if (field.GetAttribute<MaxAttribute>() is MaxAttribute maxAttribute)
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

            if (field.GetAttribute<UnitAttribute>() is UnitAttribute unitAttribute)
            {
                using (codeGenerator.EnterScope())
                {
                    codeGenerator.Append($"auto displayUnit = quantityCast<units::ratios::{unitAttribute.DisplayUnit}, units::ratios::{unitAttribute.Unit}>({fieldName});");

                    var propertyGridCall = numericOptions.Any() ?
                        $"property_grid::drawProperty(\"{field.DisplayName}\", displayUnit, {{ {string.Join(", ", numericOptions)} }} )" :
                        $"property_grid::drawProperty(\"{field.DisplayName}\", displayUnit)";

                    using (codeGenerator.EnterScope($"if( {propertyGridCall} )"))
                    {
                        codeGenerator.Append($"{fieldName} = quantityCast<units::ratios::{unitAttribute.Unit}, units::ratios::{unitAttribute.DisplayUnit}>(displayUnit);");
                        codeGenerator.Append($"isModified = true;");
                    }
                }
            }
            else
            {
                var propertyGridCall = numericOptions.Any() ?
                    $"property_grid::drawProperty(\"{field.DisplayName}\", {fieldName}, {{ {string.Join(", ", numericOptions)} }} )" :
                    $"property_grid::drawProperty(\"{field.DisplayName}\", {fieldName})";

                codeGenerator.Append($"isModified |= {propertyGridCall};");
            }
        }
    }
}
