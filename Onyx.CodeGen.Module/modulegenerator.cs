using Onyx.CodeGen.Core;
using System.Reflection.Emit;
using Type = Onyx.CodeGen.Core.Type;

namespace Onyx.CodeGen.Module
{
    public class ModuleGenerator
    {
        struct RegisterCreateData
        {
            public string FunctionName;
            public string RegisterFunction;
            public string AdditionalInclude;
            public IEnumerable<Type> Types;

            public Func<Type, string> OverrideTypeName;
        }

        private string moduleName;
        private string moduleSourcePath;
        private string moduleBinaryPath;
        private IEnumerable<string> moduleNamespaceStack;
        private TypeDatabase typeDatabase;


        public ModuleGenerator(string moduleName, string moduleSourcePath, string moduleBinaryPath, IEnumerable<string> moduleNamespaceStack, TypeDatabase typeDatabase)
        {
            this.moduleName = moduleName;
            this.moduleSourcePath = moduleSourcePath;
            this.moduleBinaryPath = moduleBinaryPath;
            this.moduleNamespaceStack = moduleNamespaceStack;
            this.typeDatabase = typeDatabase;
        }

        public IEnumerable<string> GenerateModule(string outputPublicPath, string outPrivatePath)
        {
            List<string> generatedFiles = new List<string>();

            var engineSystems = typeDatabase.GetDerivedTypes("Onyx::IEngineSystem").Where(type => type.AbsolutePath.StartsWith(moduleSourcePath));

            var assets = typeDatabase.GetCurrentModuleTypesDerivedFromTemplate("Onyx::Assets::Asset");
            assets = assets.Where(type => type.HasTypeId);

            var serializers = typeDatabase.GetCurrentModuleTypesDerivedFromTemplate("Onyx::Assets::AssetSerializer");

            // get shader graph nodes
            var shaderGraphNodes = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::Graphics::ShaderGraphNode")
                .Where(type => type.HasTypeId && (type is not TemplateType));

            // get rendergraph nodes
            var renderGraphNodes = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::Graphics::IRenderGraphNode")
                .Where(type => type.HasTypeId && (type is not TemplateType));

            // get normal graph nodes
            // filter out shader / rendergraph nodes
            var nodeGraphNodes = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::NodeGraph::Node")
                .Except(shaderGraphNodes)
                .Except(renderGraphNodes)
                .Where(type => type.HasTypeId && (type is not TemplateType));


            var inputBindings = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::InputActions::InputBinding")
                .Where(type => type.HasTypeId);

            var inputTriggers = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::InputActions::InputTrigger")
                .Where(type => type.HasTypeId);

            var inputModifiers = typeDatabase
                .GetCurrentModuleDerivedTypes("Onyx::InputActions::InputModifier")
                .Where(type => type.HasTypeId);

            // component inspectors
            var componentInspectors = typeDatabase
                .GetCurrentModuleTypes()
                .Where(type => type.FullyQualifiedName.StartsWith("Onyx::Ui::PropertyInspector<") &&
                    (type is not TemplateType) &&
                    (type.AbsolutePath.StartsWith(moduleSourcePath) || type.AbsolutePath.StartsWith(moduleBinaryPath)));

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

            var headerFile = GenerateModuleHeader(outputPublicPath);

            List<RegisterCreateData> register = new List<RegisterCreateData>()
            {
                new ()
                {
                    FunctionName = "RegisterEngineSystems",
                    RegisterFunction = "Onyx::EngineSystemFactory::Register",
                    Types = engineSystems,
                    AdditionalInclude = "onyx/engine/enginesystemfactory.h"
                },
                new ()
                {
                    FunctionName = "RegisterAssets",
                    RegisterFunction = "Onyx::Assets::AssetSystem::Register",
                    Types = assets,
                    AdditionalInclude = "onyx/assets/assetsystem.h"
                },

                new ()
                {
                    FunctionName = "RegisterSerializers", 
                    RegisterFunction = "Onyx::Assets::AssetSystem::Register", 
                    Types = serializers, 
                    AdditionalInclude = "onyx/assets/assetsystem.h" },

                new ()
                {
                    FunctionName = "RegisterGraphNodes", 
                    RegisterFunction = "Onyx::NodeGraph::NodeGraphFactory::Register", 
                    Types = nodeGraphNodes, 
                    AdditionalInclude = "onyx/nodegraph/nodegraphfactory.h" },

                new ()
                {
                    FunctionName = "RegisterShaderGraphNodes", 
                    RegisterFunction = "Onyx::Graphics::ShaderGraphNodeFactory::Register", 
                    Types = shaderGraphNodes, 
                    AdditionalInclude = "onyx/graphics/shadergraph/shadergraphnodefactory.h" 
                },
                new ()
                {
                    FunctionName = "RegisterRenderGraphNodes", 
                    RegisterFunction = "Onyx::Graphics::RenderGraphNodeFactory::Register", 
                    Types = renderGraphNodes, 
                    AdditionalInclude = "onyx/graphics/rendergraph/rendergraphnodefactory.h" 
                },
                new ()
                {
                    FunctionName = "RegisterInputBindings", 
                    RegisterFunction = "Onyx::InputActions::InputBindingsFactory::Register", 
                    Types = inputBindings, 
                    AdditionalInclude = "onyx/inputactions/bindings/inputbindingsfactory.h"
                },
                new ()
                {
                    FunctionName = "RegisterInputTriggers", 
                    RegisterFunction = "Onyx::InputActions::InputTriggersFactory::Register", 
                    Types = inputTriggers, 
                    AdditionalInclude = "onyx/inputactions/triggers/inputtriggersfactory.h" 
                },
                new ()
                {
                    FunctionName = "RegisterInputModifiers", 
                    RegisterFunction = "Onyx::InputActions::InputModifiersFactory::Register", 
                    Types = inputModifiers, 
                    AdditionalInclude = "onyx/inputactions/modifiers/inputmodifiersfactory.h" 
                },
                new ()
                {
                    FunctionName = "RegisterPropertyInspectors", 
                    RegisterFunction = "Onyx::Ui::PropertyInspectors::Register", 
                    Types = componentInspectors, 
                    AdditionalInclude = "onyx/ui/propertyinspector.h",
                    OverrideTypeName = (Type type) => 
                        {
                            if ( typeDatabase.ResolveTypeName(type.SpecializedTemplateParameters[0], moduleNamespaceStack) is Type componentType )
                                   return componentType.FullyQualifiedName;
                            return type.SpecializedTemplateParameters[0]; 
                        } 
                }
            };
            
            var cppFile = GenerateModuleCpp(outPrivatePath, allArgumentTypes, engineSystems, register);

            generatedFiles.Add(headerFile);
            generatedFiles.Add(cppFile);
            return generatedFiles;
        }

        private string GenerateModuleHeader(string outputPath)
        {
            CodeGenerator generator = new CodeGenerator(CodeGenerator.AUTO_GENERATED_FILE_H_HEADER);
    
            using (generator.EnterScope($"namespace {string.Join("::", moduleNamespaceStack)}"))
            {
                generator.Append("void Init();");
            }

            var headerFile = Path.Join(outputPath, $"{moduleName}.gen.h");
            File.WriteAllText(headerFile, generator.GetCode());
            return headerFile;
        }

        private string GenerateModuleCpp(string outputPath, IEnumerable<Type> allArguments, IEnumerable<Type> engineSystems, IEnumerable<RegisterCreateData> registerCreateData)
        {
            CodeGenerator systemRegistrationCodeGen = new CodeGenerator("");

            IReadOnlyList<Type> systemIncludes;
            GenerateSystemsCode(systemRegistrationCodeGen, engineSystems, out systemIncludes);

            List<string> generatedFunctionCalls = new List<string>();
            List<string> generatedRegisterCodeBlocks = new List<string>();
            List<string> includes = new List<string>();
            foreach (var registerData  in registerCreateData)
            {
                if (GenerateRegisterFunction(registerData, moduleNamespaceStack, generatedRegisterCodeBlocks, includes))
                {
                    generatedFunctionCalls.Add(registerData.FunctionName);
                }
            }

            CodeGenerator generator = new CodeGenerator();
            generator.AddIncludes(includes);
            generator.AddIncludes( systemIncludes.Select(type => type.IncludePath) );
            generator.AddIncludes( allArguments.Select(type => type.IncludePath) );

            generator.AppendLine();
            
            var systemCreationCodeLines = systemRegistrationCodeGen.GetCodeLines();
            if (systemCreationCodeLines.Any())
            {
                using (generator.EnterScope($"namespace Onyx"))
                {
                    generator.Append(systemCreationCodeLines);
                }
            }
            
            using (generator.EnterScope($"namespace {string.Join("::", moduleNamespaceStack)}"))
            {
                bool appendLine = false;
                using (generator.EnterScope("namespace"))
                {
                    foreach (var codeBlock in generatedRegisterCodeBlocks)
                    {
                        if (appendLine)
                            generator.AppendLine();

                        generator.Append(codeBlock.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

                        appendLine = true;
                    }
                }

                if (appendLine)
                    generator.AppendLine();

                using (generator.EnterScope("void Init()"))
                {
                    generator.Append(generatedFunctionCalls.Select(functionCall => $"{functionCall}();"));
                }
            }

            var cppFile = Path.Join(outputPath, $"{moduleName}.gen.cpp");
            File.WriteAllText(cppFile, generator.GetCode());
            return cppFile;
        }

        private bool GenerateRegisterFunction(RegisterCreateData registerData, IEnumerable<string> moduleNamespaceStack, List<string> outGeneratedCode, List<string> outIncludes)
        {
            if (!registerData.Types.Any())
                return false;

            outIncludes.Add(registerData.AdditionalInclude);

            CodeGenerator codeGenerator = new CodeGenerator("");
            var trimmedRegisterFn = registerData.RegisterFunction.TrimFullyQualifiedName(moduleNamespaceStack);

            using (codeGenerator.EnterScope($"void {registerData.FunctionName}()"))
            {
                foreach (var type in registerData.Types.OrderBy(type => type.Name))
                {
                    outIncludes.Add(type.IncludePath);

                    var fullyQualifiedTypeName = registerData.OverrideTypeName != null ? registerData.OverrideTypeName(type) : type.FullyQualifiedName;
                    var trimmedTypeName = fullyQualifiedTypeName.TrimFullyQualifiedName(moduleNamespaceStack);

                    codeGenerator.Append($"{trimmedRegisterFn}<{trimmedTypeName}>();");
                }
            }

            outGeneratedCode.Add(codeGenerator.GetCode());
            return true;
        }

        private void GenerateSystemsCode(CodeGenerator codeGenerator, IEnumerable<Type> engineSystems, out IReadOnlyList<Type> outSystemIncludes)
        {
            List<string> namespaceStack = new List<string>() { "Onyx" };
            List<Type> includes = new List<Type>();

            bool appendLine = false;
            foreach (var engineSystem in engineSystems)
            {
                if (appendLine)
                {
                    codeGenerator.AppendLine();
                }

                var engineTypeName = engineSystem.FullyQualifiedName.TrimFullyQualifiedName(namespaceStack);

                codeGenerator.Append("template <>");
                using (codeGenerator.EnterClass($"struct EngineSystemMeta<{engineTypeName}>"))
                {
                    GenerateSystemCreate(codeGenerator, engineSystem, engineTypeName, includes);
                    GenerateSystemUpdate(codeGenerator, engineSystem, engineTypeName, includes);
                }

                appendLine = true;
            }

            outSystemIncludes = includes.Union(engineSystems).Distinct().ToList();
        }

        private void GenerateSystemCreate(CodeGenerator generator, Type engineSystem, string engineTypeName, List<Type> outIncludes)
        {
            List<string> namespaceStack = new List<string>() { "Onyx" };
            var constructor = engineSystem.GetConstructorsOrStaticCreate().FirstOrDefault();

            IEnumerable<Type> constructorParameters = Enumerable.Empty<Type>();
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

        private void GenerateSystemUpdate(CodeGenerator generator, Type engineSystem, string engineTypeName, List<Type> outIncludes)
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
