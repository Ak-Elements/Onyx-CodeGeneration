using Onyx.CodeGen.Core;

namespace Onyx.CodeGen.ComponentDSL
{
    internal interface IFieldEditor
    {
        void Generate(CodeGenerator codeGenerator, Field field);
    }
}
