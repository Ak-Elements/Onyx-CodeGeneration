namespace Onyx.CodeGen.ComponentDSL
{
    public class AllowedTypes : System.Attribute
    {
        public List<string> Types {  get; private set; }
        
        public AllowedTypes(params string[] types) { Types = types.ToList(); }
    }

    public class MutuallyExclusive : System.Attribute
    {
        public List<Type> Types { get; private set; }
        public MutuallyExclusive(params Type[] types) { Types = types.ToList(); }
    }

    public class Attribute : System.Attribute
    {
    }

    internal class ReadOnly : Attribute
    {
        public override string ToString()
        {
            return "ReadOnly";
        }
    }
    internal class Transient : Attribute
    {
        public override string ToString()
        {
            return "Transient";
        }
    }
    internal class Hidden : Attribute
    {
        public override string ToString()
        {
            return "Hidden";
        }
    }
}
