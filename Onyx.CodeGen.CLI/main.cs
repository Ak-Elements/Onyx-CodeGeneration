using Onyx.CodeGen.ComponentDSL;
using Onyx.CodeGen.Core;
using Onyx.CodeGen.Module;
using System.Runtime.Serialization;
using Type = Onyx.CodeGen.Core.Type;

namespace Onyx.CodeGen.CLI
{
    class TargetConfig
    {
        [DataMember(Name = "name")]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "namespace")]
        public string Namespace { get; set; } = string.Empty;

        [DataMember(Name = "is_executable")]
        public bool IsExecutable { get; set; }

        [DataMember(Name = "source_files")]
        public List<string> Sources { get; set; } = new List<string>();

        [DataMember(Name = "include_directories")]
        public List<string> IncludeDirectories { get; set; } = new List<string>();

        [DataMember(Name = "generated_module_headers")]
        public List<string> GeneratedModuleHeaders { get; set; } = new List<string>();
    }

    class Paths
    {
        [DataMember(Name = "project_dir")]
        public string ProjectDirectory { get; set; } = string.Empty;

        [DataMember(Name = "source_dir")]
        public string SourceDirectory { get; set; } = string.Empty;

        [DataMember(Name = "binary_dir")]
        public string BinaryDirectory { get; set; } = string.Empty;

        [DataMember(Name = "dependencies_dir")]
        public string DependenciesDirectory { get; set; } = string.Empty;

        [DataMember(Name = "public_dir_suffix")]
        public string PublicDirectorySuffix { get; set; } = string.Empty;

        [DataMember(Name = "private_dir_suffix")]
        public string PrivateDirectorySuffix { get; set; } = string.Empty;

        [DataMember(Name = "generated_dir_suffix")]
        public string GeneratedDirectorySuffix { get; set; } = "generated";

        [DataMember(Name = "editor_binary_dir")]
        public string PrivateEditorBinaryDirectory { get; set; } = string.Empty;
    }


    class Config
    {
        [DataMember(Name = "target")]
        public TargetConfig TargetConfig { get; set; } = new TargetConfig();
        public Paths Paths { get; set; } = new Paths();


    }

    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Missing command. Expected module or project");
                Console.Error.WriteLine("   onyx-codegen module [path to config]");
                Console.Error.WriteLine("   onyx-codegen project [path to config]");
            }

            string command = args[0];
            string configPath = args[1];
            var alltext = File.ReadAllText(configPath);
            var config = Tomlyn.Toml.ToModel<Config>(alltext, configPath);

            RunModuleCodeGeneration(config);
            if( config.TargetConfig.IsExecutable )
            {
                RunProjectBootstrapGeneration(config);
            };
        }

        static void RunProjectBootstrapGeneration(Config config)
        {
            var outPath = Path.Combine(config.Paths.BinaryDirectory, config.Paths.PrivateDirectorySuffix, "init.gen.cpp");
            var projectGeneratedCodePath = config.Paths.BinaryDirectory;

            IReadOnlyList<string> includeDirectories = config.TargetConfig.IncludeDirectories;
            IEnumerable<string> generatedModuleHeaderPaths = config.TargetConfig.GeneratedModuleHeaders.Distinct();
            
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

        static void RunModuleCodeGeneration(Config config)
        {
            string targetName = config.TargetConfig.Name;
            string targetNamespace = config.TargetConfig.Namespace;
            string sourceDir = config.Paths.SourceDirectory;
            string binaryDir = config.Paths.BinaryDirectory;
            string generatedPathSuffix = config.Paths.GeneratedDirectorySuffix;
            string publicPathSuffix = config.Paths.PublicDirectorySuffix;
            string privatePathSuffix = config.Paths.PrivateDirectorySuffix;
            string editorDir = config.Paths.PrivateEditorBinaryDirectory ?? "";

            var outPublicPath = binaryDir + "/" + generatedPathSuffix + "/" + publicPathSuffix;
            var outPrivatePath = binaryDir + "/" + generatedPathSuffix + "/" + privatePathSuffix;

            // only used for engine modules
            var outEditorPrivatePath = editorDir.Replace('\\', '/'); // base target output path for editor files (binary directory)

        
            // output containing all files generated so consecutive runs can delete files that are no longer valid
            var generatedFilesPath = Path.Combine(binaryDir, "generatedfiles");


            IEnumerable<string> sources = config.TargetConfig.Sources;
            IEnumerable<string> includeDirectories = config.TargetConfig.IncludeDirectories.Where(includeDirectory => includeDirectory.StartsWith(config.Paths.ProjectDirectory) && includeDirectory.StartsWith(config.Paths.DependenciesDirectory) == false);
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
