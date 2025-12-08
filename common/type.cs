


namespace onyx_codegen.common
{
    internal class Type
    {
        private IReadOnlyList<Function> functions = new List<Function> ();
        private IReadOnlyList<string> inheritanceList = new List<string>();
        private IReadOnlyList<string> specializedTemplateArguments = new List<string>();

        public string Name { get; set; } = "";
        public string FullyQualifiedName { get; set; } = "";
        public string AbsolutePath { get; set; } = "";
        public string IncludePath { get; set; } = "";
        public string TypeIdentifier { get; set; } = ""; // class / struct /enum
        public string Namespace { get; set; } = "";

        public string AliasedType { get; set; } = "";
        public bool IsAliased { get; set; }
        public bool IsAbstract { get; set; }
        public bool HasTypeId { get; set; }
        public IReadOnlyList<string> Inherits { get => inheritanceList; set => inheritanceList = value; }  
        public IReadOnlyList<Function> Functions { get => functions; set => functions = value; }
        public IReadOnlyList<string> SpecializedTemplateParameters { get => specializedTemplateArguments; set => specializedTemplateArguments = value; }

        public IEnumerable<Function> GetConstructors()
        {
            return functions.Where( function => function.Name == Name ).OrderBy( function => function.Parameters.Count );
        }

        public IEnumerable<Function> GetConstructorsOrStaticCreate()
        {
            var constructors = functions.Where(function => function.Name == Name);
            if (constructors.Any())
            {
                return constructors.OrderBy(function => function.Parameters.Count);
            }

            return functions.Where(function => (function.Name == "Create") && function.IsStatic).OrderBy(function => function.Parameters.Count);
        }

        public IEnumerable<Function> GetFunctions(string name)
        {
            return functions.Where(function => function.Name == name).OrderBy(function => function.Parameters.Count);
        }

        public override string ToString()
        {
            return $"Name: {Name}, FullyQualified: {FullyQualifiedName}, Type: {TypeIdentifier} " +
                $"Path: {AbsolutePath}, IncludePath: {IncludePath}," +
                $"AliasedType: {AliasedType}, IsAliased: {IsAliased}, IsAbstract: {IsAbstract}, " +
                $"Inherits: {string.Join(", ", Inherits)}" +
                $"Functions: {string.Join("\n", functions)}";
        }

        internal bool IsDerivedFrom(string typeName)
        {
            return Inherits.Contains(typeName);
        }
    }
}