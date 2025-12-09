using System.Runtime.InteropServices;

namespace Onyx.CodeGen.TreeSitter
{
    public sealed class TsCpp
    {
        [DllImport("tree-sitter-cpp.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr tree_sitter_cpp();
    }
}
