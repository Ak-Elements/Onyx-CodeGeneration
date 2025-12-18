namespace Onyx.CodeGen.ComponentDSL
{
    internal class Max : Attribute
    {
        internal object Value { get; private set; }
        public Max(object value) { Value = value; }

        public override string ToString() => $"Max({Value.ToString()})";
    }
}
