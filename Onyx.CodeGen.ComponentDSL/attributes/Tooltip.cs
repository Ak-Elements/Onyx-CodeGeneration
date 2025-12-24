namespace Onyx.CodeGen.ComponentDSL
{
    internal class Tooltip : Attribute
    {
        internal string Value { get; set; } = "";
        public Tooltip(string tooltip)
        {
            Value = tooltip;
        }

        public override string ToString() => $"Tooltip(\"{Value}\")";
    }
}
