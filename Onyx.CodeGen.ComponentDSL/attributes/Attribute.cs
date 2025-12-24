namespace Onyx.CodeGen.ComponentDSL
{
    public class AllowedTypesAttribute : System.Attribute
    {
        public List<string> Types {  get; private set; }
        
        public AllowedTypesAttribute(params string[] types) { Types = types.ToList(); }
    }

    public class PrecedenceAttribute : System.Attribute
    {
        public List<Type> Types { get; private set; }
        public PrecedenceAttribute(params Type[] types) { Types = types.ToList(); }
    }

    public class Attribute : System.Attribute
    {
    }

    internal class ReadOnlyAttribute : Attribute
    {
        public override string ToString()
        {
            return "ReadOnly";
        }
    }

    internal class HiddenAttribute : Attribute
    {
        public override string ToString()
        {
            return "Hidden";
        }
    }

    internal class RuntimeOnlyAttribute : Attribute
    {
        public override string ToString()
        {
            return "RuntimeOnly";
        }
    }

    internal class TransientAttribute : Attribute
    {
        public override string ToString()
        {
            return "Transient";
        }
    }

    internal class EditorOnlyAttribute : Attribute
    {
        public override string ToString()
        {
            return "EditorOnly";
        }
    }

    internal enum Build
    {
        Debug = 1 << 0,
        Release = 1 << 1,
        Retail = 1 << 2,
    }

    internal class BuildAttribute : Attribute
    {
        internal Build Type { get; }

        internal BuildAttribute(Build type)
        {
            this.Type = type;
        }

        public override string ToString()
        {
            return $"Build( { Type.ToString() } )";
        }
    }

    internal class UnitAttribute : Attribute
    {
        internal string Unit { get; set; } = "";
        internal string DisplayUnit { get; set; } = "";
    }
}
