using Onyx.CodeGen.Core;
using System.ComponentModel;

namespace Onyx.CodeGen.ComponentDSL
{
    internal interface IFieldEditor
    {
        void Generate(CodeGenerator codeGenerator, string fieldName, Field field);
    }
}
