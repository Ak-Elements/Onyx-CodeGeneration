namespace Onyx.CodeGen.ComponentDSL
{
    internal class MinAttribute : Attribute
    {
        internal object Value { get; private set; }
        public MinAttribute(object value) { Value = value; }

        public override string ToString() => $"Min({Value.ToString()})";
    }
}
