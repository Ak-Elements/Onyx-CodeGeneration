namespace Onyx.CodeGen.ComponentDSL
{
    internal struct Component
    {
        internal string Name { get; set; }
        internal string FullyQualifiedName { get; set; }
        internal List<Attribute> Attributes { get; set; }
        internal List<Field> Fields;

        internal bool IsTransient => HasAttribute<Transient>();
        internal bool IsReadOnly => HasAttribute<ReadOnly>();
        internal bool IsHidden => HasAttribute<Hidden>();

        internal bool HasAttribute<T>() where T : Attribute
        {
            return Attributes.Any(attribute => attribute is T);
        }
    }
}
