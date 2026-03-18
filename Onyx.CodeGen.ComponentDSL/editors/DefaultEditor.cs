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
        public void Generate(CodeGenerator codeGenerator, string fieldName, Field field)
        {
            codeGenerator.Append($"isModified |= property_grid::DrawProperty(\"{field.DisplayName}\", {fieldName});");
        }  
    }
}
