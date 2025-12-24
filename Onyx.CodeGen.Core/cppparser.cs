using Onyx.CodeGen.TreeSitter;

namespace Onyx.CodeGen.Core
{
    public class CppParser
    {
        private string source = "";
        private string filePath = "";
        IEnumerable<string> includeDirectories = new List<string>();

        public CppParser(IEnumerable<string> includeDirectories)
        {
            this.includeDirectories = includeDirectories;
        }

        public void Parse(string path, out List<Function> outGlobalFunctions, out List<Type> outTypes)
        {
            outTypes = new List<Type>();
            outGlobalFunctions = new List<Function>();
            filePath = path;
            source = File.ReadAllText(path);
            using (var parser = new TSParser())
            {
                var treesitter_cpp = TsCpp.tree_sitter_cpp();
                TSLanguage language = new TSLanguage(treesitter_cpp);
                bool setLanguage = parser.set_language(language);

                using (var tree = parser.parse_string(null, source))
                {
                    if (tree == null)
                        return;

                    var cursor = new TSCursor(tree.root_node(), language);

                    List<string> namespaceStack = new List<string>();
                    Walk(cursor, namespaceStack, outGlobalFunctions, outTypes);
                }
            }
        }

        private ReadOnlySpan<char> GetNodeContent(TSNode node)
        {
            int so = (int)node.start_offset();
            int eo = (int)node.end_offset();
            return source.AsSpan(so, eo - so);
        }

        private string ExtractNamespaceName(TSCursor cursor)
        {
            List<string> parts = new();
            foreach (var child in cursor.children())
            {
                string sym = cursor.current_symbol();
                if (sym == "namespace_identifier")
                {
                    parts.Add(GetNodeContent(cursor.current_node()).ToString());
                }
                else if (sym == "nested_namespace_specifier")
                {
                    parts.Add(ExtractNamespaceName(cursor));
                }
            }

            return string.Join("::", parts);
        }

        private void Walk(TSCursor cursor, IReadOnlyList<string> namespaceStack, List<Function> outGlobalFunctions, List<Type> outTypes, int depth = 0)
        {
            string sym = cursor.current_symbol();

            List<string> localNamespaceStack = namespaceStack.ToList();
            switch (sym)
            {
                case "namespace_definition":
                    string nodeContent = GetNodeContent(cursor.current_node()).ToString();
                    localNamespaceStack.Add(ExtractNamespaceName(cursor));
                    break;
                case "class_specifier":
                case "struct_specifier":
                {
                    Type newType = new Type();
                    if (ExtractType(cursor, sym == "class_specifier" ? "class" : "struct", ref newType, localNamespaceStack))
                    {
                        outTypes.Add(newType);
                    }
                    return;
                }
                case "template_declaration":
                {
                    var templateParameters = GetTemplateParameters(cursor);
                    if (templateParameters.Any())
                    {
                        TemplateType templateType = new TemplateType();
                        templateType.TemplateParameters = templateParameters.ToList();
                        foreach (var child in cursor.children())
                        {
                            switch (child.current_symbol())
                            {
                                case "class_specifier":
                                case "struct_specifier":
                                    var newType = templateType as Type;
                                    if (ExtractType(cursor, child.current_symbol() == "class_specifier" ? "template class" : "template struct", ref newType, localNamespaceStack))
                                    {
                                        outTypes.Add(newType);
                                    }
                                    break;
                            }
                        }
                        return;
                    }
                    break;
                }
                case "alias_declaration":
                case "type_definition":
                {
                    Type newType;
                    ExtractAlias(cursor, out newType, localNamespaceStack);
                    outTypes.Add(newType);
                    break;
                }
                case "declaration_list":
                    List<Function> globalFunctions;
                    ExtractFunctionDefinitions(cursor, namespaceStack, out globalFunctions);
                    outGlobalFunctions.AddRange(globalFunctions);
                    break;
                case "enum_specifier":
                    break;
            }

            // recurse into children for all other nodes
            foreach (var child in cursor.children())
            {
                Walk(cursor, localNamespaceStack, outGlobalFunctions, outTypes, depth + 1);
            }
        }

        private void ExtractAlias(TSCursor cursor, out Type outType, List<string> namespaceStack)
        {
            ReadOnlySpan<char> aliasName = "";
            ReadOnlySpan<char> alisedType = "";
            bool isTemplate = false;
            IEnumerable<string> templateArguments = Enumerable.Empty<string>();

            foreach (var child in cursor.children())
            {
                TSNode node = cursor.current_node();
                string sym = cursor.current_symbol();

                if (sym == "type_identifier")
                {
                    aliasName = GetNodeContent(node);
                }

                if ((sym == "type_descriptor") || (sym == "primitive_type"))
                {
                    alisedType = GetNodeContent(node);
                    foreach (var typeChild in cursor.children())
                    {
                        var typeSymbol = typeChild.current_symbol();
                        if ((typeSymbol == "type_identifier") || (typeSymbol == "qualified_identifier"))
                        {
                            foreach (var typeIdentifierChild in typeChild.children())
                            {
                                if (typeIdentifierChild.current_symbol() == "template_type")
                                {
                                    templateArguments = GetTemplateParameters(typeIdentifierChild);
                                }
                            }
                        }
                        else if (typeSymbol == "template_type")
                        {
                            templateArguments = GetTemplateParameters(typeChild);
                        }
                    }
                    
                }
            }

            string name = aliasName.ToString();
            string namespaceStr = string.Join("::", namespaceStack);
            var fullyQualifiedName = string.IsNullOrEmpty(namespaceStr) ? name : namespaceStr + "::" + name;

            if (isTemplate)
            {
                outType = new TemplateType();
            }
            else
            {
                outType = new Type();
            }

            outType.Name = name;
            outType.FullyQualifiedName = fullyQualifiedName;
            outType.Namespace = namespaceStr;
            outType.TypeIdentifier = "alias";
            outType.AbsolutePath = filePath;
            outType.IsAliased = true;
            outType.AliasedType = alisedType.ToString();
            outType.IncludePath = PathExtension.GetShortestRelativePath(includeDirectories, filePath);
            outType.SpecializedTemplateParameters = templateArguments.ToList();
        }

        bool ExtractType(TSCursor cursor, string type, ref Type outType, IReadOnlyList<string> currentNamespace)
        {
            ReadOnlySpan<char> className = "";
            bool isForwardDeclaration = true;
            bool isFullySpecializedTemplate = true;
            bool isQualifiedName = false;
            List<string> baseClasses = new List<string>();
            List<Function> functions = new List<Function>();

            foreach (var child in cursor.children())
            {
                TSNode node = cursor.current_node();
                string sym = cursor.current_symbol();
                bool isQualifiedIdentifier = sym == "qualified_identifier";
                if (isQualifiedIdentifier || (sym == "type_identifier") || (sym == "template_type"))
                {
                    isQualifiedName = isQualifiedIdentifier;
                    className = GetNodeContent(node);
                }

                if (sym == "field_declaration_list")
                {
                    isForwardDeclaration = false;
                    // look for constructor / create functions
                    ExtractFunctionDefinitions(cursor, currentNamespace, out functions);
                }

                if (sym == "base_class_clause")
                {
                    // Extract inhertiance
                    foreach (var baseClassChild in cursor.children())
                    {
                        if ((cursor.current_symbol() == "type_identifier") ||
                            (cursor.current_symbol() == "qualified_identifier") ||
                            (cursor.current_symbol() == "template_type"))
                        {
                            baseClasses.Add(GetNodeContent(cursor.current_node()).ToString());
                        }
                    }
                }
            }

            if ((isForwardDeclaration == false) && isFullySpecializedTemplate)
            {
                string name = className.ToString();

                var classContent = GetNodeContent(cursor.current_node());

                //TODO: not ideal and maybe makes more sense to actually parse the function def, but this seems simpler for now
                bool hasTypeId = classContent.IndexOf("StringId32 GetTypeId() const".AsSpan()) != -1 && classContent.IndexOf("static constexpr StringId32 TypeId".AsSpan()) != -1;

                string namespaceStr = string.Join("::", currentNamespace);
                var fullyQualifiedName = string.IsNullOrEmpty(namespaceStr) ? name : namespaceStr + "::" + name;
                outType.Name = name;
                outType.FullyQualifiedName = fullyQualifiedName;
                outType.Namespace = namespaceStr;
                outType.TypeIdentifier = type;
                outType.AbsolutePath = filePath;
                outType.Inherits = baseClasses;
                outType.HasTypeId = hasTypeId;

                if (functions.IsNullOrEmpty() == false)
                {
                    outType.Functions = functions;
                }
                
                outType.IncludePath = PathExtension.GetShortestRelativePath(includeDirectories, filePath).Replace('\\', '/');
                return true;
            }

            return false;
        }

        void ExtractFunctionDefinitions(TSCursor cursor, IReadOnlyList<string> currentNamespace, out List<Function> outFunctions)
        {
            outFunctions = new List<Function>();

            foreach (var child in cursor.children())
            {
                string sym = child.current_symbol();
                switch (sym)
                {
                    case "function_definition":
                    case "declaration":
                    case "field_declaration":
                    {
                        bool isStatic = false;
                        foreach (var functionChild in cursor.children())
                        {
                            switch (functionChild.current_symbol())
                            {
                                case "storage_class_specifier":
                                {
                                    var nodeContent = GetNodeContent(functionChild.current_node());
                                    if (nodeContent is "static")
                                        isStatic = true;
                                    break;
                                }
                                case "function_declarator":
                                {
                                    string functionName;
                                    IReadOnlyList<FunctionParameter> functionArgs = ExtractFunctionParametersAndName(functionChild, out functionName);
                                    outFunctions.Add(new Function() { Name = functionName, Namespace = string.Join("::", currentNamespace), IsStatic = isStatic, Parameters = functionArgs });
                                    break;
                                }    
                            }
                        }

                        break;
                    }
                }
            }
        }

        IReadOnlyList<FunctionParameter> ExtractFunctionParametersAndName(TSCursor cursor, out string functionName)
        {
            functionName = "";
            IReadOnlyList<FunctionParameter> parameters = new List<FunctionParameter>();
            foreach (var functionDeclChild in cursor.children())
            {
                var functionChildContent = GetNodeContent(functionDeclChild.current_node());
                switch (functionDeclChild.current_symbol())
                {
                    case "field_identifier":
                    case "identifier":
                        functionName = functionChildContent.ToString();
                        break;
                    case "parameter_list":
                        parameters = GetFunctionParameters(functionDeclChild);
                        break;
                }
            }

            return parameters;
        }

        IReadOnlyList<FunctionParameter> GetFunctionParameters(TSCursor cursor)
        {
            List<FunctionParameter> parameters = new List<FunctionParameter>();
            // parse params
            foreach (var parameter in cursor.children())
            {
                if (parameter.current_symbol() == "parameter_declaration")
                {
                    ReadOnlySpan<char> parameterQualifier = ""; // const etc...
                    ReadOnlySpan<char> parameterType = "";
                    bool isReference = false;
                    bool isPointer = false;
                    foreach (var parameterDecl in cursor.children())
                    {
                        switch (parameterDecl.current_symbol())
                        {
                            case "type_qualifier":
                                parameterQualifier = GetNodeContent(parameterDecl.current_node());
                                break;
                            case "type_identifier":
                            case "qualified_identifier":
                            case "template_type":
                                parameterType = GetNodeContent(parameterDecl.current_node());
                                break;
                            case "reference_declarator":
                                isReference = true;
                                break;
                            case "pointer_declarator":
                                isPointer = true;
                                break;
                        }
                    }

                    if (parameterType.IsEmpty)
                        continue;

                    FunctionParameter functionParameter = new FunctionParameter();
                    functionParameter.TypeName = parameterType.ToString();
                    functionParameter.IsReference = isReference;
                    functionParameter.IsPointer = isPointer;
                    functionParameter.IsConst = parameterQualifier is "const";

                    int index = parameterType.LastIndexOf("::");
                    if (index == -1)
                        index = 0;
                    else
                        index += 2;

                    functionParameter.Name = char.ToLower(parameterType[index]) + parameterType[(index + 1)..].ToString();
                    parameters.Add(functionParameter);
                }
            }

            return parameters;
        }

        IEnumerable<string> GetTemplateParameters(TSCursor cursor)
        {
            var parameters = new List<string>();
            foreach (var child in cursor.children())
            {
                if ((cursor.current_symbol() == "template_parameter_list") ||
                    (cursor.current_symbol() == "template_argument_list"))
                {
                    foreach (var paramChild in child.children())
                    {
                        var templateParam = GetNodeContent(paramChild.current_node()).ToString();
                        switch (paramChild.current_symbol())
                        {
                            case "type_parameter_declaration":
                            case "parameter_declaration":
                                if (templateParam.StartsWith("typename"))
                                    templateParam = templateParam.Substring("typename".Count());
                                parameters.Add(templateParam.Trim());
                                break;
                            case "type_descriptor":
                                parameters.Add(templateParam.Trim());
                                break;
                            case "string_literal":
                            case "primitive_type":
                                parameters.Add(templateParam.Trim());
                                break;

                            default:
                                break;
                        }
                    }

                    break;
                }
            }

            return parameters;
        }
    }
}
