namespace onyx_codegen.common
{
    public struct Function
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public bool IsStatic { get; set; }
        public IReadOnlyList<FunctionParameter> Parameters { get; set; }

        public override string ToString()
        {
            return $"{Namespace}::{Name}( {string.Join(", ", Parameters)} )";
        }
    }

    public struct FunctionParameter
    {
        public bool IsConst { get; set; }
        public bool IsReference { get; set; }
        public bool IsPointer { get; set; }

        public string Name { get; set; }
        public string TypeName { get; set; }

        public override string ToString()
        {
            return (IsConst ? "const " : "") +
                   TypeName +
                   (IsReference ? "&" : "") +
                   (IsPointer ? "*" : "") +
                   " " + Name;
        }

        internal string? ToStringWithoutName()
        {
            return (IsConst ? "const " : "") +
                   TypeName +
                   (IsReference ? "&" : "") +
                   (IsPointer ? "*" : "");
        }
    }

    public static class FunctionParameterExtensions
    {
        /// <summary>
        /// Reduce a sequence of FunctionParameter to a minimal set such that all parameters
        /// are still represented via implicit C++-style conversions (T& -> const T&).
        /// Prefers non-const references when both const and non-const versions exist.
        /// </summary>
        public static IEnumerable<FunctionParameter> ReduceToConvertible( this IEnumerable<FunctionParameter> source )
        {
            return source
                .GroupBy(p => p.TypeName) // group by type
                .Select(group =>
                {
                    // If any non-const exists, pick it
                    var nonConst = group.FirstOrDefault(p => !p.IsConst);
                    return nonConst.IsDefault() ? group.First() : nonConst;
                });
        }

        private static bool IsDefault(this FunctionParameter p)
        {
            // helper for value types; true if default(T)
            return p.Equals(default(FunctionParameter));
        }
    }
}
