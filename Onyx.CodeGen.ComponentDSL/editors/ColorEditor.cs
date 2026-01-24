using Onyx.CodeGen.Core;

namespace Onyx.CodeGen.ComponentDSL
{
    internal class ColorEditor : IFieldEditor
    {
        [Editor("Color"), AllowedTypes("Vector3f32", "Vector4f32")]
        public void Generate(CodeGenerator codeGenerator, string fieldName, Field field)
        {
            codeGenerator.Append($"isModified |= PropertyGrid::DrawColorProperty(\"{field.DisplayName}\", {fieldName});");
        }  
    }
}
