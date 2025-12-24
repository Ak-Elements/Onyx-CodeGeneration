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

        internal bool IsRuntimeOnly => HasAttribute<RuntimeOnlyAttribute>();
        internal bool IsReadOnly => HasAttribute<ReadOnlyAttribute>();
        internal bool IsHidden => HasAttribute<HiddenAttribute>();

        internal string DisplayName => GetAttribute<NameAttribute>()?.Value ?? Name;

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
