mkdir bin
call zig cc -shared -target x86_64-windows-msvc -shared -O3 -DNDEBUG -o bin/tree-sitter.dll -Itree-sitter/lib/src -Itree-sitter/lib/include tree-sitter/lib/src/lib.c tree-sitter.def

call zig cc -shared -target x86_64-windows-msvc -shared -O3 -DNDEBUG -o bin/tree-sitter-cpp.dll -Itree-sitter-cpp/src  -Itree-sitter/lib/include  tree-sitter-cpp/src/parser.c  tree-sitter-cpp/src/scanner.c tree-sitter-cpp.def