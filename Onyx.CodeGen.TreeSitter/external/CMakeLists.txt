cmake_minimum_required(VERSION 3.28)
project(tree_sitter LANGUAGES C)

# -----------------------------
# Common settings
# -----------------------------
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

if(CMAKE_INSTALL_PREFIX_INITIALIZED_TO_DEFAULT)
  set_property(CACHE CMAKE_INSTALL_PREFIX PROPERTY VALUE ${CMAKE_CURRENT_SOURCE_DIR}/out)
endif()

# --------------------------------------------------
# tree-sitter core
# --------------------------------------------------

add_library(tree-sitter SHARED
    tree-sitter/lib/src/lib.c
    tree-sitter.def
)

target_include_directories(tree-sitter PRIVATE
    tree-sitter/lib/src
    tree-sitter/lib/include
)

# --------------------------------------------------
# tree-sitter-cpp grammar
# --------------------------------------------------

add_library(tree-sitter-cpp SHARED
    tree-sitter-cpp/src/parser.c
    tree-sitter-cpp/src/scanner.c
    tree-sitter-cpp.def
)

target_include_directories(tree-sitter-cpp PRIVATE
    tree-sitter/lib/include
    tree-sitter-cpp/src
)

target_compile_definitions(tree-sitter-cpp PRIVATE
    NDEBUG
)

# tree-sitter-cpp depends on tree-sitter headers
target_link_libraries(tree-sitter-cpp PRIVATE tree-sitter)

# Installation rules
install(TARGETS tree-sitter tree-sitter-cpp
        RUNTIME DESTINATION bin    # DLLs / executables
        LIBRARY DESTINATION lib    # *.so / *.dylib
        ARCHIVE DESTINATION lib    # *.lib / *.a
)

# Optionally install headers
install(DIRECTORY tree-sitter/lib/include/ DESTINATION include)