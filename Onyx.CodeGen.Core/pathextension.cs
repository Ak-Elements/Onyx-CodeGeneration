namespace Onyx.CodeGen.Core
{
    public static class PathExtension
    {
        /// <summary>
        /// Computes the shortest relative path from the given include directories to the target path.
        /// Only include directories that are ancestors of the target path are considered.
        /// </summary>
        public static string GetShortestRelativePath(IEnumerable<string> includeDirs, string target)
        {
            if (target == null)
                return "";

            if (includeDirs.IsNullOrEmpty())
                return target;

            string? shortest = null;

            foreach (var dir in includeDirs)
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string relative;

                try
                {
                    relative = Path.GetRelativePath(dir, target);
                }
                catch
                {
                    continue; // Ignore invalid paths
                }

                // Only keep paths that stay within the include directory
                if (relative.StartsWith(".."))
                    continue;

                if (string.IsNullOrEmpty(shortest) || relative.Length < shortest.Length)
                    shortest = relative;
            }

            var result = shortest ?? target;
            return result.Replace('\\', '/');
        }
    }
}