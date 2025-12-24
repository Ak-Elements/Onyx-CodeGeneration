namespace Onyx.CodeGen.ComponentDSL
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class NameAttribute : Attribute
    {
        internal string Value { get; set; } = "";
        public NameAttribute(string name)
        {
            Value = name;
        }

        public override string ToString()
        { 
            return $"Name: \"{Value}\"";
        }
    }
}
