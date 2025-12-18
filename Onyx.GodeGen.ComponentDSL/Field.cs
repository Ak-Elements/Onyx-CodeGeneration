namespace Onyx.CodeGen.ComponentDSL
{
    internal struct Field
    {
        internal string Name { get; set; }
        
        internal Core.Type? Type { get; set; }
        internal string DefaultValue { get; set; }
        internal List<Attribute> Attributes { get; set; }

        internal string TypeName => Type?.FullyQualifiedName ?? FallbackTypeName;

        internal string FallbackTypeName { get; set; }

        internal bool IsTransient => HasAttribute<Transient>();
        internal bool IsReadOnly => HasAttribute<ReadOnly>();
        internal bool IsHidden => HasAttribute<Hidden>();

        internal string DisplayName => GetAttribute<Name>()?.Value ?? Name;

        internal bool HasAttribute<T>() where T : Attribute
        {
            return Attributes.Any(attribute => attribute is T);
        }

        internal T? GetAttribute<T>() where T : Attribute
        {
            return (T?)Attributes.FirstOrDefault(attribute => attribute is T);
        }
    }
}
