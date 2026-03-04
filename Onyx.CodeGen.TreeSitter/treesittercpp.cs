using System.Runtime.InteropServices;

namespace Onyx.CodeGen.TreeSitter
{
    public sealed class TreeSitterLanguageCpp
    {
        [DllImport("tree-sitter-cpp", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr tree_sitter_cpp();
    }
}
