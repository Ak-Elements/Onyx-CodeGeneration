namespace Onyx.CodeGen.ComponentDSL
{
    internal struct Component
    {
        public Component()
        {
        }

        internal string Name { get; set; } = string.Empty;
        internal string FullyQualifiedName { get; set; } = string.Empty;
        internal List<Attribute> Attributes { get; set; } = [];
        internal List<Field> Fields { get; set; } = [];

        internal bool IsRuntimeOnly => HasAttribute<RuntimeOnlyAttribute>();
        //internal bool IsTransient => HasAttribute<TransientAttribute>();
        internal bool IsReadOnly => HasAttribute<ReadOnlyAttribute>();
        internal bool IsHidden => HasAttribute<HiddenAttribute>();

        //internal bool IsSerializable => (IsRuntimeOnly == false) && (IsTransient == false);

        internal bool HasAttribute<T>() where T : Attribute
        {
            return Attributes.Any(attribute => attribute is T);
        }
    }
}
