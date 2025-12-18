namespace Onyx.CodeGen.ComponentDSL
{
    internal class Min : Attribute
    {
        internal object Value { get; private set; }
        public Min(object value) { Value = value; }

        public override string ToString() => $"Min({Value.ToString()})";
    }
}
