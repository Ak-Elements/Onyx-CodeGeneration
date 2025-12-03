namespace onyx_codegen.common
{
    public static class StringExtensions
    {
        static internal string TrimFullyQualifiedName(this string typeName, IEnumerable<string> namespaceStack)
        {
            foreach (var namespaceIdentifier in namespaceStack)
            {
                if (typeName.StartsWith(namespaceIdentifier))
                {
                    // + 2 to remove ::
                    typeName = typeName.Substring(namespaceIdentifier.Length + 2);
                }
                else
                {
                    break;
                }
            }

            return typeName;
        }
    }
}
