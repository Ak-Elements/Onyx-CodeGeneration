namespace Onyx.CodeGen.ComponentDSL
{
    [Name("Range"), MutuallyExclusive(typeof(Min), typeof(Max))]
    internal class Range : Attribute
    {
        internal object Min { get; }
        internal object Max { get; }

        protected Range(object min, object max)
        {
            Min = min;
            Max = max;
        }

        public override string ToString() => $"Range({ Min.ToString() }, { Max.ToString() })";
    }
}
