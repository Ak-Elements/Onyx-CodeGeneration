namespace Onyx.CodeGen.ComponentDSL
{
    public class Editor : Attribute
    {
        public string Value { get; } = "";

        public Editor(string editor) => Value = editor;

        public override string ToString() => $"Editor(\"{Value}\")";
    }
}
