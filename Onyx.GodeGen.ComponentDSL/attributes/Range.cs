namespace Onyx.CodeGen.ComponentDSL
{
    [Precedence(typeof(MinAttribute), typeof(MinAttribute))]
    internal class RangeAttribute : Attribute
    {
        internal object Min { get; }
        internal object Max { get; }

        protected RangeAttribute( object min, object max )
        {
            Min = min;
            Max = max;
        }

        public override string ToString() => $"Range({ Min.ToString() }, { Max.ToString() })";
    }
}
