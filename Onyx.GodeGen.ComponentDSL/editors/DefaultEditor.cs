using Onyx.CodeGen.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Onyx.CodeGen.ComponentDSL
{
    internal class DefaultEditor : IFieldEditor
    {
        public void Generate(CodeGenerator codeGenerator, Field field)
        {
            codeGenerator.Append($"isModified |= PropertyGrid::DrawProperty(\"{field.DisplayName}\", {field.Name});");
        }  
    }
}
