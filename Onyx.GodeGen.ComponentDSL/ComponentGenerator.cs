using Onyx.CodeGen.Core;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using static Onyx.CodeGen.Core.CodeGenerator;

namespace Onyx.CodeGen.ComponentDSL
{
    public class ComponentGenerator
    {
        private string moduleSourcePath;
        private string publicPathSuffix;
        private string privatePathSuffix;
        private string generatedPathSuffix;
        private string outPublicPath;
        private string outPrivatePath;
        private string outEditorPath;
        private IEnumerable<string> includeDirectories;
        private IEnumerable<string> moduleNamespaceStack;
        private TypeDatabase typeDatabase;

        private static readonly IEnumerable<System.Type> ANNOTATION_ATTRIBUTES = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => !p.IsGenericType)
            .Where(p => typeof(Attribute).IsAssignableFrom(p));



        public ComponentGenerator(TypeDatabase typeDatabase,
            string moduleSourcePath,
            string publicPathSuffix,
            string privatePathSuffix,
            string generatedPathSuffix,
            string outPublicPath,
            string outPrivatePath,
            string outEditorPath,
            IEnumerable<string> includeDirectories,
            IEnumerable<string> moduleNamespaceStack)
        {
            this.moduleSourcePath = moduleSourcePath;
            this.publicPathSuffix = publicPathSuffix;
            this.privatePathSuffix = privatePathSuffix;
            this.generatedPathSuffix = generatedPathSuffix;
            this.outPublicPath = outPublicPath;
            this.outPrivatePath = outPrivatePath;
            this.outEditorPath = outEditorPath;
            this.includeDirectories = includeDirectories;
            this.moduleNamespaceStack = moduleNamespaceStack;
            this.typeDatabase = typeDatabase;
        }

        public void Generate(string componentDefinitionPath)
        {
            List<Component> components = Parse(componentDefinitionPath);

            // Generate
            var publicSourcesPath = Path.Join(moduleSourcePath, publicPathSuffix);
            var outFileName = Path.GetFileNameWithoutExtension(componentDefinitionPath);
            var relativePath = Path.GetDirectoryName(Path.GetRelativePath(publicSourcesPath, componentDefinitionPath));
           
            var headerPath = Path.Join(outPublicPath, relativePath, $"{outFileName}.gen.h").Replace('\\', '/');
            GenerateComponentHeader(components, headerPath);

            string headerIncludePath = PathExtension.GetShortestRelativePath(includeDirectories, headerPath);
            var cppPath = Path.Join(outPrivatePath, relativePath, $"{outFileName}.gen.cpp").Replace('\\', '/');

            List<string> componentIncludes;
            List<string> editorIncludes;
            IEnumerable<string> editorCppCodeLines = GenerateComponentEditorCpp(components, headerIncludePath, out editorIncludes);
            IEnumerable<string> componentCppCodeLines = GenerateComponentCpp(components, headerIncludePath, out componentIncludes);
            if (string.IsNullOrWhiteSpace(outEditorPath))
            {
                CodeGenerator codeGenerator = new CodeGenerator();

                var sortedIncludes = componentIncludes.Union(editorIncludes)
                    .Distinct()                                                 // deduplicate
                    .OrderBy(s => s.Count(c => c == '/' || c == '\\'))          // sort by folder depth
                    .ThenBy(s => s)                                             // sort alphabetical
                    .Select(s => $"#include <{s}>");

                codeGenerator.Append(sortedIncludes);
                codeGenerator.AppendLine();

                codeGenerator.Append(editorCppCodeLines);
                codeGenerator.AppendLine();

                codeGenerator.Append(componentCppCodeLines);
                codeGenerator.AppendLine();

                File.WriteAllText(cppPath, codeGenerator.GetCode());
            }
            else
            {
                {
                    CodeGenerator codeGenerator = new CodeGenerator();
                    codeGenerator.Append($"#include <{headerIncludePath}>");
                    codeGenerator.Append(componentIncludes.Select(include => $"#include <{include}>"));
                    codeGenerator.AppendLine();
                    codeGenerator.Append(componentCppCodeLines);
                    File.WriteAllText(cppPath, codeGenerator.GetCode());
                }

                {
                    var editorCppPath = Path.Join(outEditorPath, relativePath, $"{outFileName}_editor.gen.cpp");
                    CodeGenerator codeGenerator = new CodeGenerator();
                    codeGenerator.Append(editorIncludes.Select(include => $"#include <{include}>"));
                    codeGenerator.AppendLine();
                    codeGenerator.Append(editorCppCodeLines);
                    File.WriteAllText(editorCppPath, codeGenerator.GetCode());
                }  
            }
        }

        private void GenerateComponentHeader(IReadOnlyList<Component> components, string outPath)
        {
            CodeGenerator codeGenerator = new CodeGenerator();
            codeGenerator.Append("#pragma once");
            
            var currentNamespace = string.Join("::", moduleNamespaceStack);
            foreach (var component in components)
            {
                GenerateComponentDeclaration(codeGenerator, currentNamespace, component);
            }

            var nonTransientComponents = components.Where(component => component.IsTransient == false);
            if (nonTransientComponents.Any())
            {
                codeGenerator.AppendLine();
                using (codeGenerator.EnterScope($"namespace Onyx"))
                {
                    foreach (var component in nonTransientComponents)
                    {
                        GenerateComponentSerializerDeclaration(codeGenerator, component);
                    }
                }
            }

            File.WriteAllText(outPath, codeGenerator.GetCode());
        }

        private static void GenerateComponentDeclaration(CodeGenerator codeGenerator, string currentNamespace, Component component)
        {
            var includes = component.Fields
                .Where(field => field.Type?.AbsolutePath.Contains("onyx/modules/core") == false)
                .Select(field => field.Type?.IncludePath)
                .Distinct()                                                 // deduplicate
                .OrderBy(s => s.Count(c => c == '/' || c == '\\'))          // sort by folder depth
                .ThenBy(s => s)                                             // sort alphabetical
                .Select(s => $"#include <{s}>"); ;

            codeGenerator.Append(includes);
            if (includes.Any())
            {
                codeGenerator.AppendLine();
            }

            using (codeGenerator.EnterScope($"namespace {currentNamespace}"))
            using (codeGenerator.EnterClass($"struct {component.Name}"))
            {
                bool isTransient = component.IsTransient;
                bool isHidden = component.IsHidden;

                if (isHidden)
                {
                    codeGenerator.Append("static constexpr bool HideInEditor = true;");
                }

                if (isTransient)
                {
                    codeGenerator.Append("static constexpr bool IsTransient = true;");
                }

                if (isTransient || isHidden)
                {
                    codeGenerator.AppendLine();
                }

                codeGenerator.Append($"static constexpr StringId32 TypeId = \"{component.FullyQualifiedName}\";");
                codeGenerator.Append("StringId32 GetTypeId() const { return TypeId; }");
                codeGenerator.AppendLine();

                foreach (Field field in component.Fields)
                {
                    if (field.Attributes.Any())
                    {
                        codeGenerator.Append($"//[{string.Join(", ", field.Attributes)}]");
                    }

                    if (string.IsNullOrEmpty(field.DefaultValue))
                    {
                        codeGenerator.Append($"{field.TypeName} {field.Name};");
                    }
                    else
                    {
                        if (field.TypeName.Equals("string", StringComparison.OrdinalIgnoreCase))
                        {
                            codeGenerator.Append($"{field.TypeName} {field.Name} {{\"{field.DefaultValue}\"}};");
                        }
                        else
                        {
                            codeGenerator.Append($"{field.TypeName} {field.Name} {{{field.DefaultValue}}};");
                        }
                    }
                }

                codeGenerator.AppendLine();
                codeGenerator.Append("#if ONYX_IS_DEBUG || ONYX_IS_EDITOR", true);
                codeGenerator.Append("bool DrawProperties(bool showHidden);");
                codeGenerator.Append("#endif", true);
            }
        }

        private void GenerateComponentSerializerDeclaration(CodeGenerator generator, Component component)
        {
            var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName("Onyx");
            generator.Append("template <>");
            using (generator.EnterClass($"struct Serialization<{componentTypeName}>"))
            {
                generator.Append($"static bool Serialize(Serializer& serializer, const {componentTypeName}& {char.ToLower(component.Name[0]) + component.Name[1..]});");
                generator.Append($"static bool Deserialize(const Deserializer& deserializer, {componentTypeName}& out{component.Name});");
            }
        }

        private IEnumerable<string> GenerateComponentCpp(IReadOnlyList<Component> components, string headerIncludePath, out List<string> includePaths)
        {
            includePaths = new List<string>();

            CodeGenerator codeGenerator = new CodeGenerator("");

            var nonTransientComponents = components.Where(component => component.IsTransient == false);
            if (nonTransientComponents.Any())
            {
                includePaths.Add("onyx/serialize/serializer.h");
                includePaths.Add("onyx/serialize/deserializer.h");

                using (codeGenerator.EnterScope($"namespace Onyx"))
                {
                    foreach (var component in nonTransientComponents)
                    {
                        var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName("Onyx");
                        var serializerComponentParameterName = char.ToLower(component.Name[0]) + component.Name[1..];
                        using (codeGenerator.EnterFunction($"bool Serialization<{componentTypeName}>::Serialize(Serializer& serializer, const {componentTypeName}& {serializerComponentParameterName})"))
                        {
                            var serializerCalls = component.Fields
                                .Where(field => field.IsTransient == false)
                                .Select(field => $"serializer.Write<\"{field.Name}\">({serializerComponentParameterName}.{field.Name})");

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

                                using (codeGenerator.EnterScopeNoBraces())
                                {
                                    codeGenerator.Append(serializerCalls.Skip(1).SkipLast(1).Select(serializerCall => $"{serializerCall} ||"));
                                    codeGenerator.Append($"{serializerCalls.Last()};");
                                }

                            }
                        }
                        
                        codeGenerator.AppendLine();

                        using (codeGenerator.EnterFunction($"bool Serialization<{componentTypeName}>::Deserialize(const Deserializer& deserializer, {componentTypeName}& out{component.Name})"))
                        {
                            var deserializerCalls = component.Fields
                                .Where(field => field.IsTransient == false)
                                .Select(field => $"deserializer.Read<\"{field.Name}\">(out{component.Name}.{field.Name})");

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

                                using (codeGenerator.EnterScopeNoBraces())
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

        private IEnumerable<string> GenerateComponentEditorCpp(IReadOnlyList<Component> components, string headerIncludePath, out List<string> outEditorIncludes)
        {
            outEditorIncludes =
            [
                headerIncludePath,
                "onyx/ui/propertygrid.h",
                "onyx/ui/scopeddisable.h", // Only if readonly
            ];

            CodeGenerator codeGenerator = new CodeGenerator("");
            var currentNamespace = string.Join("::", moduleNamespaceStack);
            using (codeGenerator.EnterScope($"namespace {currentNamespace}"))
            {
                foreach (var component in components)
                {
                    bool hasFields = component.Fields.Any();
                    bool hasHiddenFields = component.Fields.Any(component => component.IsHidden);

                    var componentTypeName = component.FullyQualifiedName.TrimFullyQualifiedName(currentNamespace);
                    var drawPropertiesSignature = $"bool {componentTypeName}::DrawProperties(bool{(hasHiddenFields ? " showHidden" : "/*showHidden*/")})";
                    using (codeGenerator.EnterFunction(drawPropertiesSignature))
                    {
                        if (hasFields)
                        {
                            codeGenerator.Append("using namespace Ui;");
                            codeGenerator.Append("bool isModified = false;");
                            foreach (var field in component.Fields)
                            {
                                if (field.GetAttribute<Tooltip>() is Tooltip tooltipAttribute)
                                {
                                    codeGenerator.Append($"PropertyGrid::SetNextPropertyTooltip(\"{tooltipAttribute.Value}\");");
                                }

                                // Hidden should be overridable by the editor
                                var scopeName = field.IsHidden ? "if (showHidden)" : "";

                                IFieldEditor? editor = null;
                                if (field.GetAttribute<Editor>() is Editor customEditorAttribute)
                                {
                                    editor = Editors.GetEditor(customEditorAttribute.Value);
                                }
                                else
                                {
                                    editor = Editors.GetEditor(field);
                                }
                                
                                var fieldEditor = editor ?? new DefaultEditor();
                                if (scopeName.IsNullOrEmpty() && (field.IsReadOnly == false))
                                {
                                    fieldEditor.Generate(codeGenerator, field);
                                }
                                else
                                {
                                    using (codeGenerator.EnterScope(scopeName))
                                    {
                                        if (field.IsReadOnly)
                                        {
                                            codeGenerator.Append("ScopedImGuiDisabled _;");
                                        }

                                        fieldEditor.Generate(codeGenerator, field);
                                    }
                                }
                            }

                            codeGenerator.Append("return isModified;");
                        }
                    }
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
                    var attributeStrings = trimmed[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var attributeString in attributeStrings)
                    {
                        var index = attributeString.IndexOf('(');
                        var endIndex = attributeString.IndexOf(')');
                        var attributeTypeString = index == -1 ? attributeString : attributeString[0..index];
                        var attributeParam = index == -1 ? "" : attributeString[(index+1)..endIndex];

                        if (attributeParam.StartsWith('"')  && attributeParam.EndsWith('"'))
                        {
                            attributeParam = attributeParam[1..^1];
                        }

                        var attribute = ANNOTATION_ATTRIBUTES.Where(attributeType =>
                        {
                            var nameAttribute = attributeType.GetCustomAttribute<Name>(inherit: true);
                            string attributeName = nameAttribute?.Value ?? attributeType.Name;
                            return attributeName.Equals(attributeTypeString, StringComparison.OrdinalIgnoreCase);
       
                        }).FirstOrDefault();

                        if (attribute != null)
                        {
                            object? newAttribute = null;
                            if (string.IsNullOrEmpty(attributeParam))
                            {
                                newAttribute = Activator.CreateInstance(attribute);
                            }
                            else
                            {
                                var ctorArguments = attribute.GetConstructors().First().GetParameters();
                                object? t = Convert.ChangeType(attributeParam, ctorArguments[0].ParameterType);
                                newAttribute = Activator.CreateInstance(attribute, t);
                            }
                            
                            if (newAttribute != null)
                            {
                                attributes.Add((Attribute)newAttribute);
                            }
                        }
                    }
                    
                    continue;
                }

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

                var parts = trimmed.Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.IsNullOrEmpty())
                {
                    continue;
                }

                var typeName = parts[0];
                Core.Type? type = typeDatabase.ResolveTypeName(typeName, currentNamespace);

                Field field = new Field
                { 
                    FallbackTypeName = parts[0],
                    Type = type,
                    Attributes = new List<Attribute>(attributes),
                    Name = parts[1]
                };

                attributes.Clear();
                currentComponent.Fields.Add(field);
            }

            return components;
        }
    }
}
