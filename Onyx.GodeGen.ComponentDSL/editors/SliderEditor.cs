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

            numericOptions.Add(".IsSlider = true");
            codeGenerator.Append($"isModified |= PropertyGrid::DrawProperty(\"{field.DisplayName}\", {field.Name}, {{ { string.Join(", " ,numericOptions) } }} );");
        }  
    }
}
