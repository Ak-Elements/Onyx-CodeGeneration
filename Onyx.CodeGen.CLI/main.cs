using Onyx.CodeGen.ComponentDSL;
using Onyx.CodeGen.Core;
using Onyx.CodeGen.Module;
using System.CommandLine;
using Type = Onyx.CodeGen.Core.Type;

namespace Onyx.CodeGen.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            RootCommand rootCommand = [CreateProjectCommand(), CreateModuleCommand()];
            rootCommand.Parse(args).Invoke();
        }

        static Command CreateProjectCommand()
        {
            var cmd = new Command("project", "Generate init code for a project.");

            var binaryDirOpt = new Option<string>("--binary-dir")
            {
                Description = "Module binary directory",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
                Required = true
            };

            var outDirOpt = new Option<string>("--out-dir")
            {
                Description = "Output directory",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
                Required = true
            };
           
            cmd.Add(binaryDirOpt);
            cmd.Add(outDirOpt);
            cmd.SetAction(RunProjectBootstrapGeneration);

            return cmd;
        }


        static Command CreateModuleCommand()
        {
            var cmd = new Command("module", "Generate code for an engine/project module.");

            var targetOpt = new Option<string>("--target")
            {
                Description = "CMake target name",
                Required = true
            };

            var namespaceOpt = new Option<string>("--namespace")
            {
                Description = "C++ namespace",
                Required = true
            };

            var sourceDirOpt = new Option<string>("--source-dir")
            {
                Description = "Module source directory",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
                Required = true
            };

            var binaryDirOpt = new Option<string>("--binary-dir")
            {
                Description = "Module binary directory",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
                Required = true
            };

            var generatedDirOpt = new Option<string>(
                "--generated-dir")
            {
                Description = "Generated output directory",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
                DefaultValueFactory = (_) => "generated",
                
            };
            
            var publicDirOpt = new Option<string>("--public-dir")
            {
                Description = "Public include directory (relative)",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
            };

            var privateDirOpt = new Option<string>("--private-dir")
            {
                Description = "Private include directory (relative)",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
            };

            var privateEditorBinaryDirOpt = new Option<string>("--editor-dir")
            {
                Description = "Private binary directory for the editor module",
                DefaultValueFactory = (_) => "",
                CustomParser = result => result.Tokens.First().Value.Replace('\\', '/'),
            };

            cmd.Add(targetOpt);
            cmd.Add(namespaceOpt);
            cmd.Add(sourceDirOpt);
            cmd.Add(binaryDirOpt);
            cmd.Add(generatedDirOpt);
            cmd.Add(publicDirOpt);
            cmd.Add(privateDirOpt);
            cmd.Add(privateEditorBinaryDirOpt);

            cmd.SetAction(RunModuleCodeGeneration);

            return cmd;
        }

        static void RunProjectBootstrapGeneration(ParseResult result)
        {
            var outPath = result.GetRequiredValue<string>("--out-dir");
            var projectGeneratedCodePath = result.GetRequiredValue<string>("--binary-dir"); 
            var generatedModuleHeadersFile = Path.Combine(projectGeneratedCodePath, "generatedmoduleheaders");
            var includesPath = Path.Combine(projectGeneratedCodePath, "includedirectories");

            IReadOnlyList<string> includeDirectories = File.ReadAllLines(includesPath);
            IEnumerable<string> generatedModuleHeaderPaths = File.ReadAllLines(generatedModuleHeadersFile).Distinct();
            
            CodeGenerator codeGenerator = new CodeGenerator();
            List<Type> outTypes;
            List<Function> globalFunctions;
            List<string> includes = new List<string>();
            IEnumerable<Function> allGlobalFunctions = Enumerable.Empty<Function>();
            foreach (var moduleHeaderPath in generatedModuleHeaderPaths)
            {
                CppParser parser = new CppParser(includeDirectories);
                parser.Parse(moduleHeaderPath, out globalFunctions, out outTypes);
                allGlobalFunctions = allGlobalFunctions.Union(globalFunctions);

                if (globalFunctions.Any())
                {
                    string moduleIncludePath = PathExtension.GetShortestRelativePath(includeDirectories, moduleHeaderPath).Replace('\\', '/'); ;
                    includes.Add(moduleIncludePath);
                }
            }

            codeGenerator.AddIncludes(includes);
            codeGenerator.AppendLine();
            
            using (codeGenerator.EnterScope("namespace Onyx"))
            using (codeGenerator.EnterFunction("void Init()"))
            {
                var registerEngineModuleFunctions = allGlobalFunctions.Where(function => function.Name == "Init");
                foreach (var function in registerEngineModuleFunctions)
                {
                    string fullyQualifiedName = function.Namespace + "::" + function.Name;
                    if (fullyQualifiedName.StartsWith("Onyx::"))
                    {
                        fullyQualifiedName = fullyQualifiedName["Onyx::".Length..];
                    }

                    codeGenerator.Append($"{fullyQualifiedName}();");
                }
            }

            File.WriteAllText(outPath, codeGenerator.GetCode());
        }

        static void RunModuleCodeGeneration(ParseResult result)
        {
            string targetName = result.GetRequiredValue<string>("--target")!;
            string targetNamespace = result.GetRequiredValue<string>("--namespace")!;
            string sourceDir = result.GetRequiredValue<string>("--source-dir")!;
            string binaryDir = result.GetRequiredValue<string>("--binary-dir")!;
            string generatedPathSuffix = result.GetValue<string>("--generated-dir") ?? "generated";
            string publicPathSuffix = result.GetRequiredValue<string>("--public-dir");
            string privatePathSuffix = result.GetRequiredValue<string>("--private-dir");
            string editorDir = result.GetValue<string>("--editor-dir") ?? "";

            var outPublicPath = binaryDir + "/" + generatedPathSuffix + "/" + publicPathSuffix;
            var outPrivatePath = binaryDir + "/" + generatedPathSuffix + "/" + privatePathSuffix;

            // only used for engine modules
            var outEditorPrivatePath = editorDir.Replace('\\', '/'); // base target output path for editor files (binary directory)

            var sourcesPath = Path.Combine(binaryDir, "sourcefiles"); // public sources of target
            var includesPath = Path.Combine(binaryDir, "includedirectories"); // include directories of target

            // output containing all files generated so consecutive runs can delete files that are no longer valid
            var generatedFilesPath = Path.Combine(binaryDir, "generatedfiles");

            if (File.Exists(sourcesPath) == false)
            {
                Console.Error.WriteLine($"Sources file(path:{sourcesPath}) does not exist");
                return;
            }

            if (File.Exists(includesPath) == false)
            {
                Console.Error.WriteLine($"Include directories file(path:{includesPath}) does not exist");
                return;
            }

            IEnumerable<string> sources = File.ReadAllLines(sourcesPath);
            IEnumerable<string> includeDirectories = File.ReadAllLines(includesPath);
            IEnumerable<string> oldGeneratedFiles = File.Exists(generatedFilesPath) ? File.ReadAllLines(generatedFilesPath) : Enumerable.Empty<string>();

            foreach (var includeDirectory in includeDirectories.Distinct())
            {
                if (string.IsNullOrEmpty(includeDirectory))
                    continue;

                sources = sources.Union(Directory.EnumerateFiles(includeDirectory, "*.h", SearchOption.AllDirectories).Select(s => s.Replace('\\', '/')));
            }

            IEnumerable<string> componentDefinitions = sources.Where(sourcePath => sourcePath.EndsWith(".ocd"));
            IEnumerable<string> cppSources = sources.Except(componentDefinitions);

            TypeDatabase typeDatabase = new TypeDatabase();
            typeDatabase.Init(cppSources, includeDirectories);

            IEnumerable<string> moduleNamespaceStack = targetNamespace.Split("::");
            
            ComponentGenerator componentGenerator = new ComponentGenerator(typeDatabase,
                sourceDir,
                publicPathSuffix,
                privatePathSuffix,
                generatedPathSuffix,
                outPublicPath,
                outPrivatePath,
                outEditorPrivatePath,
                includeDirectories,
                moduleNamespaceStack);
            
            foreach (var componentDefinition in componentDefinitions)
            {
                componentGenerator.Generate(componentDefinition);
            }
            
            string sourcesBasePath = PathExtension.GetShortestRelativePath(includeDirectories, sources.First());
            ModuleGenerator generator = new ModuleGenerator(targetName, sourceDir, moduleNamespaceStack, typeDatabase);
            IEnumerable<string> generatedFiles = generator.GenerateModule(outPublicPath, outPrivatePath);
            
            IEnumerable<string> filesToDelete = oldGeneratedFiles.Except(generatedFiles);
            foreach (var file in filesToDelete)
            {
                File.Delete(file);
            }
            
            File.WriteAllLines(generatedFilesPath, generatedFiles);
        }
    }
}
