namespace onyx_codegen.common
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns true if the enumerable is null or contains no elements.
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source == null || !source.Any();
        }
    }
}
