namespace Onyx.CodeGen.ComponentDSL
{
    internal class MaxAttribute : Attribute
    {
        internal object Value { get; private set; }
        public MaxAttribute(object value) { Value = value; }

        public override string ToString() => $"Max({Value.ToString()})";
    }
}
