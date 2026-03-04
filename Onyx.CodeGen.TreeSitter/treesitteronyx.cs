using System.Runtime.InteropServices;

namespace Onyx.CodeGen.TreeSitter
{
    public sealed class TreeSitterLanguageOnyx
    {
        [DllImport("tree-sitter-onyx", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr tree_sitter_onyx();
    }
}
