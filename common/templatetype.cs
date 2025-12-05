


namespace onyx_codegen.common
{
    internal class TemplateType : Type
    {
        private IReadOnlyList<string> templateParamters = new List<string>();
        
        public IReadOnlyList<string> TemplateParameters { get => templateParamters; set => templateParamters = value; }  
    }
}