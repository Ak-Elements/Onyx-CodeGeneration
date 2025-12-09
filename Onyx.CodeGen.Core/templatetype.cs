namespace Onyx.CodeGen.Core
{
    public class TemplateType : Type
    {
        private IReadOnlyList<string> templateParameters = new List<string>();
        
        public IReadOnlyList<string> TemplateParameters { get => templateParameters; set => templateParameters = value; }
    }
}