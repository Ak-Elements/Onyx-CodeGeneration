namespace Onyx.CodeGen.ComponentDSL
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class Name : Attribute
    {
        internal string Value { get; set; } = "";
        public Name(string name)
        {
            Value = name;
        }

        public override string ToString()
        { 
            return $"Name: \"{Value}\"";
        }
    }
}
