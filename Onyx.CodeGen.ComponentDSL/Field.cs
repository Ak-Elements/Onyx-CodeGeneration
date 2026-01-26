using Onyx.CodeGen.Core;

namespace Onyx.CodeGen.ComponentDSL
{
    internal struct Field
    {
        internal string Name { get; set; }
        
        internal Core.Type? Type { get; set; }
        internal List<Core.Type> SpecializedTemplateTypes { get; set; }

        internal string DefaultValue { get; set; }
        internal List<Attribute> Attributes { get; set; }

        internal bool IsTransient => HasAttribute<TransientAttribute>();
        internal bool IsReadOnly => HasAttribute<ReadOnlyAttribute>();
        internal bool IsHidden => HasAttribute<HiddenAttribute>();
        internal bool IsEditorOnly => HasAttribute<EditorOnlyAttribute>();

        internal string DisplayName => GetAttribute<NameAttribute>()?.Value ?? Name;

        internal Build BuildType => GetAttribute<BuildAttribute>()?.Type ?? Build.All;

        // raw type name as written in the raw OCD file
        internal string OcdTypeName { get; set; }

        internal string GetTrimmedTypeName(TypeDatabase typeDatabase, IEnumerable<string> namespaceContext)
        {
            if (Type == null)
            {
                return OcdTypeName.TrimFullyQualifiedName(namespaceContext);
            }

            var typeName = Type.FullyQualifiedName.TrimFullyQualifiedName(namespaceContext);
            if (Type is TemplateType)
            {
                typeName = $"{typeName}<{string.Join(", ", SpecializedTemplateTypes.Select(type => type.FullyQualifiedName.TrimFullyQualifiedName(namespaceContext)))}>";
            }

            return typeName;
        }

        internal IEnumerable<string> GetIncludePaths()
        {
            if (Type == null)
                return Enumerable.Empty<string>();

            var includes = SpecializedTemplateTypes.Where(t => t.AbsolutePath.Contains("onyx/modules/core") == false).Select(t => t.IncludePath);
            if (Type.AbsolutePath.Contains("onyx/modules/core") == false)
            {
                includes = includes.Append(Type.IncludePath);
            }
            return includes;
        }

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
