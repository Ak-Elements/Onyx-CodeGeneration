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
            "onyxF64")
    ]

    [Editor("Slider")]
    internal class SliderEditor : IFieldEditor
    {
        public void Generate(CodeGenerator codeGenerator, Field field)
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
                numericOptions.Add($".Unit = Units::{unitAttribute.Unit}");
                numericOptions.Add($".DisplayUnit = Units::{unitAttribute.DisplayUnit}");
            }

            numericOptions.Add(".IsSlider = true");
            codeGenerator.Append($"isModified |= PropertyGrid::DrawProperty(\"{field.DisplayName}\", {field.Name}, {{ { string.Join(", " ,numericOptions) } }} );");
        }  
    }
}
