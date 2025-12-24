namespace Onyx.CodeGen.Core
{
    public class TemplateType : Type
    {
        private IReadOnlyList<string> templateParameters = new List<string>();
        
        public IReadOnlyList<string> TemplateParameters { get => templateParameters; set => templateParameters = value; }

        public override string ToString()
        {
            return base.ToString() +
                $"Template Parameters: {string.Join("\n", templateParameters)}";
        }
    }
}