using Onyx.CodeGen.Core;

namespace Onyx.CodeGen.ComponentDSL
{
    public class ComponentGenerator
    {
        private string moduleSourcePath;
        private string namespacePathSuffix;
        private string generatedPathSuffix;
        private string outPublicPath;
        private string outPrivatePath;
        private string outEditorPublicPath;
        private string outEditorPrivatePath;
        private IEnumerable<string> includeDirectories;
        private IEnumerable<string> moduleNamespaceStack;
        private TypeDatabase typeDatabase;

        bool hasToolsTarget;

        public ComponentGenerator(TypeDatabase typeDatabase,
            string moduleSourcePath,
            string namespacePathSuffix,
            string generatedPathSuffix,
            string outPublicPath,
            string outPrivatePath,
            string outEditorPublicPath,
            string outEditorPrivatePath,
            bool hasToolsTarget,
            IEnumerable<string> includeDirectories,
            IEnumerable<string> moduleNamespaceStack
        )
        {
            this.typeDatabase = typeDatabase;
            this.moduleSourcePath = moduleSourcePath;
            this.namespacePathSuffix = namespacePathSuffix;
            this.generatedPathSuffix = generatedPathSuffix;

            this.outPublicPath = outPublicPath;
            this.outPrivatePath = outPrivatePath;
            this.outEditorPublicPath = outEditorPublicPath;
            this.outEditorPrivatePath = outEditorPrivatePath;

            this.includeDirectories = includeDirectories;
            this.moduleNamespaceStack = moduleNamespaceStack;

            this.hasToolsTarget = hasToolsTarget;
        }

        public void Generate(string componentDefinitionPath, List<string> outGeneratedFiles, List<string> outGeneratedEditorFiles)
        {
            List<Component> components;
            OnyxParser parser = new OnyxParser(typeDatabase, includeDirectories, moduleNamespaceStack);
            parser.Parse(componentDefinitionPath, out components);
            //return;
            //components = Parse(componentDefinitionPath);

            // Generate
            var outFileName = Path.GetFileNameWithoutExtension(componentDefinitionPath);
            string relativePath = Path.GetDirectoryName(PathExtension.GetShortestRelativePath(includeDirectories, componentDefinitionPath)) ?? "";
            relativePath = relativePath.Replace('\\', '/');
            if (relativePath.StartsWith(namespacePathSuffix))
            {
                relativePath = relativePath.Substring(namespacePathSuffix.Length);
            }

            var headerPath = Path.Join(outPublicPath, relativePath, $"{outFileName}.gen.h").Replace('\\', '/');
            var cppPath = Path.Join(outPrivatePath, relativePath, $"{outFileName}.gen.cpp").Replace('\\', '/');

            string componentHeaderIncludePath = PathExtension.GetShortestRelativePath(includeDirectories, headerPath);

            List<string> componentHeaderIncludes = [];
            List<string> componentCppIncludes = [];

            List<string> editorHeaderIncludes = [];
            List<string> editorCppIncludes = [];

            IEnumerable<string> headerCodeLines = GenerateComponentHeader(components, componentHeaderIncludes);
            IEnumerable<string> componentCppCodeLines = GenerateComponentCpp(components, componentHeaderIncludePath, out componentCppIncludes);

            bool hasEditorComponents = components.Any(component => component.IsHidden == false && component.IsCodeOnly == false);
            IEnumerable<string> editorHeaderCodeLines = hasEditorComponents ? GenerateComponentInspectorHeader(components, editorHeaderIncludes) : Enumerable.Empty<string>();
            IEnumerable<string> editorCppCodeLines = hasEditorComponents ? GenerateComponentInspectorCpp(components, editorCppIncludes) : Enumerable.Empty<string>();

            CodeGenerator headerGenerator = new CodeGenerator(CodeGenerator.AUTO_GENERATED_FILE_H_HEADER);
            CodeGenerator cppGenerator = new CodeGenerator();

            CodeGenerator editorHeaderGenerator = hasToolsTarget ? new CodeGenerator(CodeGenerator.AUTO_GENERATED_FILE_H_HEADER) : headerGenerator;
            CodeGenerator editorCppGenerator = hasToolsTarget ? new CodeGenerator() : cppGenerator;

            headerGenerator.AddIncludes(componentHeaderIncludes);
            headerGenerator.Append(headerCodeLines);

            cppGenerator.AddIncludes(componentCppIncludes);
            cppGenerator.Append(componentCppCodeLines);

            editorHeaderGenerator.AddIncludes(editorHeaderIncludes);
            editorHeaderGenerator.Append(editorHeaderCodeLines);

            editorCppGenerator.AddIncludes(editorCppIncludes);
            editorCppGenerator.Append(editorCppCodeLines);

            outGeneratedFiles.Add(headerPath);
            outGeneratedFiles.Add(cppPath);
            File.WriteAllText(headerPath, headerGenerator.GetCode());
            File.WriteAllText(cppPath, cppGenerator.GetCode());

            if (hasToolsTarget)
            {
                var editorHeaderFileName = $"{outFileName}inspector.gen.h";
                var editorCppPath = Path.Join(outEditorPrivatePath, relativePath, $"{outFileName}inspector.gen.cpp");
                var editorHeaderPath = Path.Join(outEditorPublicPath, relativePath, editorHeaderFileName);

                var editorHeaderIncludePath = Path.Join(namespacePathSuffix, relativePath, editorHeaderFileName).Replace('\\', '/');

                editorHeaderGenerator.AddInclude(componentHeaderIncludePath);
                editorCppGenerator.AddInclude(editorHeaderIncludePath);

                outGeneratedEditorFiles.Add(editorHeaderPath);
                outGeneratedEditorFiles.Add(editorCppPath);
                File.WriteAllText(editorHeaderPath, editorHeaderGenerator.GetCode());
                File.WriteAllText(editorCppPath, editorCppGenerator.GetCode());
            }
        }

        private IEnumerable<string> GenerateComponentHeader(IReadOnlyList<Component> components, List<string> outIncludes)
        {
            CodeGenerator codeGenerator = new CodeGenerator(string.Empty);

            var currentNamespace = string.Join("::", moduleNamespaceStack);
            foreach (var component in components)
            {
                GenerateComponentDeclaration(codeGenerator, currentNamespace, component, outIncludes);
            }

            var nonTransientComponents = components.Where(component => (component.IsTransient == false) && component.Fields.Any(f => f.IsTransient == false));
            if (nonTransientComponents.Any())
            {
                codeGenerator.AppendLine();
                using (codeGenerator.EnterScope($"namespace onyx"))
                {
                    foreach (var component in nonTransientComponents)
                    {
                        GenerateComponentSerializerDeclaration(codeGenerator, component);
                    }
                }
            }

            return codeGenerator.GetCodeLines();
        }

        private void GenerateComponentDeclaration(CodeGenerator codeGenerator, string currentNamespace, Component component, List<string> outIncludes)
        {
            var includes = component.Fields.SelectMany(field => field.GetIncludePaths());

            outIncludes.AddRange(includes);

            if (includes.Any())
            {
                codeGenerator.AppendLine();
            }

            using (codeGenerator.EnterScope($"namespace {currentNamespace}"))
            using (codeGenerator.EnterClass($"struct {component.Name}"))
            {
                bool isCodeOnly = component.IsCodeOnly;
                bool isTransient = component.IsTransient;

                if (isTransient)
                {
                    codeGenerator.Append("static constexpr bool IsTransient = true;");
                }

                if (isCodeOnly)
                {
                    codeGenerator.Append("static constexpr bool IsCodeOnly = true;");
                }

                if (isCodeOnly || isTransient)
                {
                    codeGenerator.AppendLine();
                }

                codeGenerator.Append($"static constexpr StringId32 TypeId {{ \"{component.FullyQualifiedName}\" }};");
                codeGenerator.Append("StringId32 GetTypeId() const { return TypeId; }");
                codeGenerator.AppendLine();

                // TODO: Group fields based on their build type / editor only
                //var debugBuildOnlyFields = component.Fields.Where(field => field.BuildType == Build.Debug);
                //var releaseBuildOnlyFields = component.Fields.Where(field => field.BuildType == Build.Release);
                //var retailBuildOnlyFields = component.Fields.Where(field => field.BuildType == Build.Retail);
                //
                //var editorOnlyField = component.Fields.Where(field => field.HasAttribute<EditorOnlyAttribute>());
                //
                //var allBuildTypeFields = component.Fields
                //    .Except(debugBuildOnlyFields)
                //    .Except(releaseBuildOnlyFields)
                //    .Except(retailBuildOnlyFields)
                //    .Except(editorOnlyField);

                foreach (Field field in component.Fields)
                {
                    var editorPreprocessor = field.IsEditorOnly ? "ONYX_IS_EDITOR" : string.Empty;

                    using (codeGenerator.EnterPreprocessorScope(editorPreprocessor))
                    {
                        if (field.Attributes.Any())
                        {
                            codeGenerator.Append($"//[{string.Join(", ", field.Attributes)}]");
                        }

                        var fieldTypeName = field.GetTrimmedTypeName(typeDatabase, moduleNamespaceStack);
                        if (string.IsNullOrEmpty(field.DefaultValue))
                        {
                            codeGenerator.Append($"{fieldTypeName} {field.Name};");
                        }
                        else
                        {
                            if (fieldTypeName.Equals("string", StringComparison.OrdinalIgnoreCase))
                            {
                                codeGenerator.Append($"{fieldTypeName} {field.Name} {{ \"{field.DefaultValue}\" }};");
                            }
                            else
                            {
                                // TODO: Default value could support initalizer list {}, assignemt =
                                if (field.DefaultValue[0] == '{')
                                    codeGenerator.Append($"{fieldTypeName} {field.Name} {field.DefaultValue};");
                                else
                                    codeGenerator.Append($"{fieldTypeName} {field.Name} {{ {field.DefaultValue} }};");
                            }
                        }
                    }
                }
            }
        }

        private void GenerateComponentSerializerDeclaration(CodeGenerator generator, Component component)
        {
            var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName("onyx");
            generator.Append("template <>");
            using (generator.EnterClass($"struct Serialization<{componentTypeName}>"))
            {
                generator.Append($"static bool serialize(Serializer& serializer, const {componentTypeName}& {char.ToLower(component.Name[0]) + component.Name[1..]});");
                generator.Append($"static bool deserialize(const Deserializer& deserializer, {componentTypeName}& out{component.Name});");
            }
        }

        private IEnumerable<string> GenerateComponentCpp(IReadOnlyList<Component> components, string headerIncludePath, out List<string> includePaths)
        {
            includePaths = new List<string>();

            CodeGenerator codeGenerator = new CodeGenerator(string.Empty);
            includePaths.Add(headerIncludePath);

            var serializableComponents = components.Where(component => (component.IsTransient == false) && component.Fields.Any(f => f.IsTransient == false));
            if (serializableComponents.Any())
            {
                includePaths.Add("onyx/serialize/serializer.h");
                includePaths.Add("onyx/serialize/deserializer.h");

                using (codeGenerator.EnterScope($"namespace onyx"))
                {
                    foreach (var component in serializableComponents)
                    {
                        var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName("onyx");
                        var serializerComponentParameterName = char.ToLower(component.Name[0]) + component.Name[1..];
                        using (codeGenerator.EnterScope($"bool Serialization<{componentTypeName}>::serialize(Serializer& serializer, const {componentTypeName}& {serializerComponentParameterName})"))
                        {
                            var serializerCalls = component.Fields
                                .Where(field => field.IsTransient == false)
                                .Select(field => $"serializer.write<\"{field.Name}\">({serializerComponentParameterName}.{field.Name})");

                            var serializerWritesCount = serializerCalls.Count();
                            if (serializerWritesCount == 0)
                            {
                                codeGenerator.Append("return true;");
                            }
                            else if (serializerWritesCount == 1)
                            {
                                codeGenerator.Append($"return {serializerCalls.First()};");
                            }
                            else
                            {
                                codeGenerator.Append($"return {serializerCalls.First()} ||");

                                using (codeGenerator.Indent())
                                {
                                    codeGenerator.Append(serializerCalls.Skip(1).SkipLast(1).Select(serializerCall => $"{serializerCall} ||"));
                                    codeGenerator.Append($"{serializerCalls.Last()};");
                                }

                            }
                        }

                        codeGenerator.AppendLine();

                        using (codeGenerator.EnterScope($"bool Serialization<{componentTypeName}>::deserialize(const Deserializer& deserializer, {componentTypeName}& out{component.Name})"))
                        {
                            var deserializerCalls = component.Fields
                                .Where(field => field.IsTransient == false)
                                .Select(field => $"deserializer.read<\"{field.Name}\">(out{component.Name}.{field.Name})");

                            var serializerReadsCount = deserializerCalls.Count();
                            if (serializerReadsCount == 0)
                            {
                                codeGenerator.Append("return true;");
                            }
                            else if (serializerReadsCount == 1)
                            {
                                codeGenerator.Append($"return {deserializerCalls.First()};");
                            }
                            else
                            {
                                codeGenerator.Append($"return {deserializerCalls.First()} ||");

                                using (codeGenerator.Indent())
                                {
                                    codeGenerator.Append(deserializerCalls.Skip(1).SkipLast(1).Select(deserializerCall => $"{deserializerCall} ||"));
                                    codeGenerator.Append($"{deserializerCalls.Last()};");
                                }
                            }
                        }
                    }
                }
            }

            return codeGenerator.GetCodeLines();
        }

        private IEnumerable<string> GenerateComponentInspectorHeader(IReadOnlyList<Component> components, List<string> outIncludes)
        {
            CodeGenerator codeGenerator = new CodeGenerator(string.Empty);

            outIncludes.Add("onyx/ui/propertyinspector.h");

            IEnumerable<string> currentNamespace = ["onyx", "ui"];
            using (codeGenerator.EnterScope("namespace onyx::ui"))
            {
                bool appendNewLine = false;
                foreach (var component in components)
                {
                    bool hasEditorFields = component.Fields.Any(field => field.IsHidden == false);
                    if (hasEditorFields == false)
                    {
                        continue;
                    }

                    if (appendNewLine)
                        codeGenerator.AppendLine();

                    var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName(currentNamespace);
                    codeGenerator.Append("template <>");
                    using (codeGenerator.EnterClass($"struct PropertyInspector<{componentTypeName}>"))
                    {
                        //TODO: Add Visibility flag for attributes / components
                        var drawSignature = $"static bool draw({componentTypeName}& component, bool /*forceShow*/);";

                        codeGenerator.Append(drawSignature);
                    }

                    appendNewLine = true;

                }
            }

            return codeGenerator.GetCodeLines();
        }

        private IEnumerable<string> GenerateComponentInspectorCpp(IReadOnlyList<Component> components, List<string> outEditorIncludes)
        {
            outEditorIncludes.Add("onyx/ui/propertygrid.h");

            CodeGenerator codeGenerator = new CodeGenerator(string.Empty);
            IEnumerable<string> currentNamespace = ["onyx", "ui"];
            using (codeGenerator.EnterScope($"namespace onyx::ui"))
            {
                bool appendNewLine = false;
                foreach (var component in components)
                {
                    bool hasEditorFields = component.Fields.Any(field => field.IsHidden == false);
                    if (hasEditorFields == false)
                    {
                        continue;
                    }

                    if (appendNewLine)
                        codeGenerator.AppendLine();


                    bool hasReadOnlyFields = component.Fields.Any(field => field.IsReadOnly);
                    if (hasReadOnlyFields)
                    {
                        outEditorIncludes.Add("onyx/ui/scopeddisable.h");
                    }

                    var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName(currentNamespace);
                    var componentInspectorSignature = $"/*static*/ bool PropertyInspector<{componentTypeName}>::draw({componentTypeName}& component, bool /*forceShow*/)";
                    using (codeGenerator.EnterScope(componentInspectorSignature))
                    {
                        codeGenerator.Append("bool isModified = false;");
                        foreach (var field in component.Fields)
                        {
                            if (field.IsHidden)
                                continue;

                            if (field.GetAttribute<Tooltip>() is Tooltip tooltipAttribute)
                            {
                                codeGenerator.Append($"property_grid::setNextPropertyTooltip(\"{tooltipAttribute.Value}\");");
                            }

                            // TODO: Add visibility check here
                            var scopeName = "";

                            IFieldEditor? editor = null;
                            if (field.GetAttribute<EditorAttribute>() is EditorAttribute customEditorAttribute)
                            {
                                editor = Editors.GetEditor(customEditorAttribute.Value);
                            }
                            else
                            {
                                editor = Editors.GetEditor(field);
                            }

                            var fieldEditor = editor ?? new DefaultEditor();
                            var fieldName = $"component.{field.Name}";
                            if (scopeName.IsNullOrEmpty() && (field.IsReadOnly == false))
                            {
                                fieldEditor.Generate(codeGenerator, fieldName, field);
                            }
                            else
                            {
                                using (codeGenerator.EnterScope(scopeName))
                                {
                                    if (field.IsReadOnly)
                                    {
                                        codeGenerator.Append("ScopedImGuiDisabled _;");
                                    }

                                    fieldEditor.Generate(codeGenerator, fieldName, field);
                                }
                            }
                        }

                        codeGenerator.Append("return isModified;");
                    }

                    appendNewLine = true;
                }
            }

            return codeGenerator.GetCodeLines();
        }

        private List<Component> Parse(string componentDefinitionPath)
        {
            var componentDefinitionLines = File.ReadAllLines(componentDefinitionPath);

            bool isComponentBlock = false;
            List<Attribute> attributes = [];
            Component currentComponent = new Component();
            List<Component> components = new List<Component>();

            var currentNamespace = string.Join("::", moduleNamespaceStack);
            for (int i = 0; i < componentDefinitionLines.Length; ++i)
            {
                var trimmed = componentDefinitionLines[i].Trim();
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    var attributeStrings = trimmed[1..];

                    string attributeParameterString = "";
                    string attributeTypeString = "";
                    List<string> attributeParameters = [];
                    bool isInAttributeParameters = false;
                    foreach (char c in attributeStrings)
                    {
                        if (c == '(')
                        {
                            isInAttributeParameters = true;
                            continue;
                        }

                        if ((c == ')') || (c == ']'))
                        {
                            isInAttributeParameters = false;

                            if (string.IsNullOrEmpty(attributeParameterString) == false)
                            {
                                attributeParameters.Add(attributeParameterString);
                                attributeParameterString = "";
                            }

                            if (Attributes.GetAttribute(attributeTypeString.Trim(), attributeParameters.ToArray()) is Attribute newAttribute)
                            {
                                attributeTypeString = string.Empty;
                                attributeParameters.Clear();
                                attributes.Add(newAttribute);
                            }

                            continue;
                        }

                        if (c == ',')
                        {
                            if (isInAttributeParameters)
                            {
                                attributeParameters.Add(attributeParameterString);
                                attributeParameterString = "";
                            }
                            else if (string.IsNullOrEmpty(attributeTypeString) == false)
                            {
                                if (string.IsNullOrEmpty(attributeParameterString) == false)
                                {
                                    attributeParameters.Add(attributeParameterString);
                                    attributeParameterString = "";
                                }

                                if (Attributes.GetAttribute(attributeTypeString.Trim(), attributeParameters.ToArray()) is Attribute newAttribute)
                                {
                                    attributeTypeString = string.Empty;
                                    attributeParameters.Clear();
                                    attributes.Add(newAttribute);
                                }
                            }
                            continue;
                        }

                        if (isInAttributeParameters)
                        {
                            attributeParameterString += c;
                        }
                        else
                        {
                            attributeTypeString += c;
                        }
                    }

                    continue;
                }

                //TODO: cleanup invalid attributes

                if (trimmed.StartsWith('{') && (isComponentBlock == false))
                {
                    isComponentBlock = true;
                    var name = componentDefinitionLines[i - 1].Trim();
                    currentComponent = new Component
                    {
                        Name = name,
                        FullyQualifiedName = currentNamespace + "::" + name,
                        Attributes = new List<Attribute>(attributes),
                        Fields = []
                    };

                    attributes.Clear();
                    components.Add(currentComponent);
                    continue;
                }

                if (trimmed.StartsWith('}') && isComponentBlock)
                {
                    isComponentBlock = false;
                    continue;
                }

                if (isComponentBlock == false)
                {
                    continue;
                }

                var splitCharacters = new char[] { ' ', ';', '=', '{', '}', ',' };
                var parts = trimmed.Split(splitCharacters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.IsNullOrEmpty())
                {
                    continue;
                }

                var typeName = parts[0];
                Core.Type? type = typeDatabase.ResolveTypeName(typeName, moduleNamespaceStack);
                List<Core.Type> specializedTemplateTypes = [];
                if (type is TemplateType)
                {
                    specializedTemplateTypes = typeDatabase.ResolveSpecializedTemplateTypes(typeName, moduleNamespaceStack);
                }

                string defaultValue = string.Empty;
                bool hasDefaultValue = trimmed.Any(c => c == '=' || c == '{');
                if (hasDefaultValue)
                {
                    defaultValue = string.Join(", ", parts[2..].Select(value =>
                    {
                        var literal = GetFormatLiteral(type);

                        if (value.EndsWith(literal))
                            return value;

                        return value + literal;
                    }));
                }

                Field field = new Field
                {
                    OcdTypeName = parts[0],
                    Type = type,
                    SpecializedTemplateTypes = specializedTemplateTypes,
                    Attributes = new List<Attribute>(attributes),
                    Name = parts[1],
                    DefaultValue = defaultValue
                };

                attributes.Clear();
                currentComponent.Fields.Add(field);
            }

            return components;
        }

        private string GetFormatLiteral(Core.Type? type)
        {
            if (type != null && DSLTypes.TYPE_TO_LITERAL_SUFFIX.TryGetValue(type.Name, out string? literalSuffix))
                return literalSuffix;

            return "";
        }
    }
}
