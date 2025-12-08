using onyx_codegen.common;

namespace onyx_codegen
{
    internal class ModuleGenerator
    {
        private string moduleName;
        private string moduleSourcePath;
        private IEnumerable<string> moduleNamespaceStack;
        private TypeDatabase typeDatabase;

        public ModuleGenerator(string moduleName, string moduleSourcePath, IEnumerable<string> moduleNamespaceStack, TypeDatabase typeDatabase)
        {
            this.moduleName = moduleName;
            this.moduleSourcePath = moduleSourcePath;
            this.moduleNamespaceStack = moduleNamespaceStack;
            this.typeDatabase = typeDatabase;
        }

        public void GenerateModule(string outputPublicPath, string outPrivatePath)
        {
            var engineSystems = typeDatabase.GetDerivedTypes("Onyx::IEngineSystem").Where(type => type.AbsolutePath.StartsWith(moduleSourcePath));

            var assets = typeDatabase.GetTypesDerivedFromTemplate("Onyx::Assets::Asset");
            assets = assets.Where(type => type.HasTypeId && type.AbsolutePath.StartsWith(moduleSourcePath));

            var serializers = typeDatabase.GetTypesDerivedFromTemplate("Onyx::Assets::AssetSerializer").Where(type => type.AbsolutePath.StartsWith(moduleSourcePath));

            // TODO: Get general nodegraph nodes

            // get shader graph nodes
            var shaderGraphNodes = typeDatabase
                .GetDerivedTypes("Onyx::Graphics::ShaderGraphNode")
                .Where(type => type.HasTypeId && (type is not TemplateType) && type.AbsolutePath.StartsWith(moduleSourcePath));

            // get rendergraph nodes
            var renderGraphNodes = typeDatabase
                .GetDerivedTypes("Onyx::Graphics::IRenderGraphNode")
                .Where(type => type.HasTypeId && (type is not TemplateType) && type.AbsolutePath.StartsWith(moduleSourcePath));

            var assetArgs = assets
                .SelectMany(type => type.GetConstructorsOrStaticCreate() ?? Enumerable.Empty<Function>())
                .SelectMany(f => f.Parameters ?? Enumerable.Empty<FunctionParameter>())
                .Distinct();

            var serializerArgs = serializers
                .SelectMany(type => type.GetConstructorsOrStaticCreate() ?? Enumerable.Empty<Function>())
                .SelectMany(f => f.Parameters ?? Enumerable.Empty<FunctionParameter>())
                .Distinct();

            var combinedArgs = assetArgs.Union(serializerArgs)
                .ReduceToConvertible();

            var allArgumentTypes = combinedArgs
                .SelectMany(argument => typeDatabase.GetTypes().Where(s => s.FullyQualifiedName.EndsWith(argument.TypeName)))
                .Distinct();

            GenerateModuleHeader(outputPublicPath);
            GenerateModuleCpp(outPrivatePath, allArgumentTypes,
                engineSystems,
                assets,
                serializers,
                shaderGraphNodes,
                renderGraphNodes);
        }

        private void GenerateModuleHeader(string outputPath)
        {
            CodeGenerator generator = new CodeGenerator(CodeGenerator.AUTO_GENERATED_FILE_HEADER);
            generator.Append("#pragma once");
    
            using (generator.EnterScope($"namespace {string.Join("::", moduleNamespaceStack)}"))
            {
                generator.Append("void Init();");
            }
        
            File.WriteAllText(Path.Join(outputPath, $"{moduleName}.gen.h"), generator.GetCode());
        }

        private void GenerateModuleCpp(string outputPath, IEnumerable<common.Type> allArguments,
            IEnumerable<common.Type> engineSystems,
            IEnumerable<common.Type> assets,
            IEnumerable<common.Type> serializers,
            IEnumerable<common.Type> shaderGraphNodes,
            IEnumerable<common.Type> renderGraphNodes)
        {
            IReadOnlyList<common.Type> systemIncludes;
            //IReadOnlyList<common.Type> assetCreationIncludes;
            CodeGenerator onyxNamespaceCodeGen = new CodeGenerator("");
            using (onyxNamespaceCodeGen.EnterScope("namespace Onyx"))
            {
                GenerateSystemsCode(onyxNamespaceCodeGen, engineSystems, out systemIncludes);
            }

            CodeGenerator generator = new CodeGenerator(CodeGenerator.AUTO_GENERATED_FILE_HEADER);

            var sortedIncludes = new[] { systemIncludes, assets, serializers, shaderGraphNodes, renderGraphNodes, allArguments }
                .SelectMany(list => list.Select(item => item.IncludePath));

            bool hasSystems = engineSystems.Any();
            bool hasAssets = assets.Any();
            bool hasSerializers = serializers.Any();
            bool hasShaderGraphNodes = shaderGraphNodes.Any();
            bool hasRenderGraphNodes = renderGraphNodes.Any();
            if (hasSystems)
                sortedIncludes = sortedIncludes.Append("onyx/engine/enginesystemfactory.h");

            if (hasAssets || hasSerializers)
                sortedIncludes = sortedIncludes.Append("onyx/assets/assetsystem.h");

            if (hasShaderGraphNodes)
                sortedIncludes = sortedIncludes.Append("onyx/graphics/shadergraph/shadergraphnodefactory.h");

            if (hasRenderGraphNodes)
                sortedIncludes = sortedIncludes.Append("onyx/graphics/rendergraph/rendergraphnodefactory.h");

            sortedIncludes = sortedIncludes.Distinct()                   // deduplicate
              .OrderBy(s => s.Count(c => c == '/' || c == '\\'))         // sort by folder depth
              .ThenBy(s => s)                                            // sort alphabetical
              .Select(s => $"#include <{s}>");

            generator.Append(sortedIncludes);
            generator.AppendLine();

            generator.Append(onyxNamespaceCodeGen.GetCode());
            using (generator.EnterScope($"namespace {string.Join("::", moduleNamespaceStack)}"))
            {
                List<string> generatedFunctions = GenerateRegisterFunctions(generator, 
                    engineSystems,
                    assets,
                    serializers,
                    shaderGraphNodes,
                    renderGraphNodes
                );

                using (generator.EnterFunction("void Init()"))
                {
                    generator.Append(generatedFunctions.Select(functionCall => $"{functionCall}();"));
                }
            }

            File.WriteAllText(Path.Join(outputPath, $"{moduleName}.gen.cpp"), generator.GetCode());
        }

        private List<string> GenerateRegisterFunctions(CodeGenerator generator, IEnumerable<common.Type> engineSystems, IEnumerable<common.Type> assets, IEnumerable<common.Type> serializers, IEnumerable<common.Type> shaderGraphNodes, IEnumerable<common.Type> renderGraphNodes)
        {
            List<string> generatedFunctionCalls = new List<string>();
            using (generator.EnterScope("namespace"))
            {
                if (engineSystems.Any())
                {
                    generatedFunctionCalls.Add("RegisterEngineSystems");

                    var trimmedFunctionCall = "Onyx::EngineSystemFactory::Register".TrimFullyQualifiedName(moduleNamespaceStack);
                    using (generator.EnterScope("void RegisterEngineSystems()"))
                    {
                        foreach (var system in engineSystems)
                        {
                            generator.Append($"{trimmedFunctionCall}<{system.FullyQualifiedName.TrimFullyQualifiedName(moduleNamespaceStack)}>();");
                        }
                    }

                }

                var trimmedAssetRegisterFunctionCall = "Onyx::Assets::AssetSystem::Register".TrimFullyQualifiedName(moduleNamespaceStack);
                if (assets.Any())
                {
                    generatedFunctionCalls.Add("RegisterAssets");
                    using (generator.EnterScope($"void RegisterAssets()"))
                    {
                        
                        foreach (var asset in assets)
                        {
                            generator.Append($"{trimmedAssetRegisterFunctionCall}<{asset.FullyQualifiedName.TrimFullyQualifiedName(moduleNamespaceStack)}>();");
                        }
                    }
                }

                if (serializers.Any())
                {
                    generatedFunctionCalls.Add("RegisterSerializers");
                    using (generator.EnterScope($"void RegisterSerializers()"))
                    {
                        foreach (var serializer in serializers)
                        {
                            generator.Append($"{trimmedAssetRegisterFunctionCall}<{serializer.FullyQualifiedName.TrimFullyQualifiedName(moduleNamespaceStack)}>();");
                        }
                    }
                }

                if (shaderGraphNodes.Any())
                {
                    generatedFunctionCalls.Add("RegisterShaderGraphNodes");
                    using (generator.EnterScope($"void RegisterShaderGraphNodes()"))
                    {
                        var trimmedFunctionCall = "Onyx::Graphics::ShaderGraphNodeFactory::Register".TrimFullyQualifiedName(moduleNamespaceStack);

                        foreach (var shaderGraphNode in shaderGraphNodes)
                        {
                            generator.Append($"{trimmedFunctionCall}<{shaderGraphNode.FullyQualifiedName.TrimFullyQualifiedName(moduleNamespaceStack)}>();");
                        }
                    }
                }

                if (renderGraphNodes.Any())
                {
                    generatedFunctionCalls.Add("RegisterRenderGraphNodes");
                    using (generator.EnterScope($"void RegisterRenderGraphNodes()"))
                    {
                        string trimmedFunctionCall = "Onyx::Graphics::RenderGraphNodeFactory::Register".TrimFullyQualifiedName(moduleNamespaceStack);
                        foreach (var renderGraphNode in renderGraphNodes)
                        {
                            generator.Append($"{trimmedFunctionCall}<{renderGraphNode.FullyQualifiedName.TrimFullyQualifiedName(moduleNamespaceStack)}>();");
                        }
                    }
                }
            }

            return generatedFunctionCalls;
        }

        private void GenerateSystemsCode(CodeGenerator codeGenerator, IEnumerable<common.Type> engineSystems, out IReadOnlyList<common.Type> outSystemIncludes)
        {
            List<string> namespaceStack = new List<string>() { "Onyx" };
            List<common.Type> includes = new List<common.Type>();

            foreach (var engineSystem in engineSystems)
            {
                var engineTypeName = engineSystem.FullyQualifiedName.TrimFullyQualifiedName(namespaceStack);

                codeGenerator.Append("template <>");
                using (codeGenerator.EnterClass($"struct EngineSystemMeta<{engineTypeName}>"))
                {
                    GenerateSystemCreate(codeGenerator, engineSystem, engineTypeName, includes);
                    GenerateSystemUpdate(codeGenerator, engineSystem, engineTypeName, includes);
                }
            }

            outSystemIncludes = includes.Union(engineSystems).Distinct().ToList();
        }

        private void GenerateSystemCreate(CodeGenerator generator, common.Type engineSystem, string engineTypeName, List<common.Type> outIncludes)
        {
            List<string> namespaceStack = new List<string>() { "Onyx" };
            var constructor = engineSystem.GetConstructorsOrStaticCreate().FirstOrDefault();

            IEnumerable<common.Type> constructorParameters = Enumerable.Empty<common.Type>();
           if (constructor.Parameters.IsNullOrEmpty() == false)
            {
                constructorParameters = constructor.Parameters
                .SelectMany(argument => typeDatabase.GetTypes().Where(s => s.FullyQualifiedName.EndsWith(argument.TypeName)))
                .Distinct();
            }

            if (constructorParameters.IsNullOrEmpty())
            {
                using (generator.EnterScope("static UniquePtr<IEngineSystem> Create(const EngineSystemCreateContext&)"))
                {
                    generator.Append($"return MakeUnique<{engineTypeName}>();");
                }
            }
            else
            {
                outIncludes.AddRange(constructorParameters);

                using (generator.EnterScope("static UniquePtr<IEngineSystem> Create(const EngineSystemCreateContext& context)"))
                using (generator.EnterMultilineFunctionCall($"return MakeUnique<{engineTypeName}>"))
                {
                    var lastParameter = constructorParameters.Last();
                    foreach (var parameter in constructorParameters)
                    {
                        bool isLastParameter = lastParameter == parameter;
                        generator.Append($"context.Get<{parameter.FullyQualifiedName.TrimFullyQualifiedName(namespaceStack)}>()" + (isLastParameter ? "" : ", "));
                    }
                }
            }
        }

        private void GenerateSystemUpdate(CodeGenerator generator, common.Type engineSystem, string engineTypeName, List<common.Type> outIncludes)
        {
            List<string> namespaceStack = new List<string>() { "Onyx" };
            var updateFunctions = engineSystem.GetFunctions("Update");
            if (updateFunctions.Any() == false)
                return;

            var updateParameters = updateFunctions.First().Parameters
                .SelectMany(argument => typeDatabase.GetTypes().Where(s => s.FullyQualifiedName.EndsWith(argument.TypeName)))
                .Distinct();

            if (updateParameters.Any())
            {
                outIncludes.AddRange(updateParameters);
                using (generator.EnterScope($"static void Update(IEngineSystem& systemInstance, const EngineSystemUpdateContext& context)"))
                {
                    generator.Append($"{engineTypeName}& typedSystemInstance = static_cast<{engineTypeName}&>(systemInstance);");

                    if (updateParameters.Any())
                    {
                        using (generator.EnterMultilineFunctionCall($"typedSystemInstance.Update"))
                        {
                            var lastParameter = updateParameters.Last();
                            foreach (var parameter in updateParameters)
                            {
                                bool isLastParameter = lastParameter == parameter;
                                generator.Append($"context.Get<{parameter.FullyQualifiedName.TrimFullyQualifiedName(namespaceStack)}>()" + (isLastParameter ? "" : ", "));
                            }
                        }
                    }
                }
            }
            else
            {
                using (generator.EnterScope($"static void Update(IEngineSystem& systemInstance, const EngineSystemUpdateContext&)"))
                {
                    generator.Append($"{engineTypeName}& typedSystemInstance = static_cast<{engineTypeName}&>(systemInstance);");
                    generator.Append("typedSystemInstance.Update();");
                }
            }
        }
    }
}
