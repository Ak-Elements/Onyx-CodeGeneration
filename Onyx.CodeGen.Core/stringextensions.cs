namespace Onyx.CodeGen.Core
{
    public static class StringExtensions
    {
        static public string TrimFullyQualifiedName(this string typeName, string namespaceToTrim)
        {
            if (typeName.StartsWith(namespaceToTrim))
            {
                // + 2 to remove ::
                typeName = typeName.Substring(namespaceToTrim.Length + 2);
            }

            return typeName;
        }

        static public string TrimFullyQualifiedName(this string typeName, IEnumerable<string> namespaceStack)
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
