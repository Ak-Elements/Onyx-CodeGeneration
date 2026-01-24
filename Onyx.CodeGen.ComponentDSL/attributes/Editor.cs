namespace Onyx.CodeGen.ComponentDSL
{
    public class EditorAttribute : Attribute
    {
        public string Value { get; } = "";

        public EditorAttribute(string editor) => Value = editor;

        public override string ToString() => $"Editor(\"{Value}\")";
    }
}
