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

        [DataMember(Name = "has_tools_target")]
        public bool HasToolsTarget { get; set; }

        [DataMember(Name = "is_tools_target")]
        public bool IsToolsTarget { get; set; } = false;

        [DataMember(Name = "source_files")]
        public List<string> Sources { get; set; } = new List<string>();

        [DataMember(Name = "include_directories")]
        public List<string> IncludeDirectories { get; set; } = new List<string>();
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

        [DataMember(Name = "namespace_dir_suffix")]
        public string NamespaceDirectorySuffix { get; set; } = string.Empty;

        [DataMember(Name = "generated_dir_suffix")]
        public string GeneratedDirectorySuffix { get; set; } = "generated";

        [DataMember(Name = "editor_target_binary_dir")]
        public string EditorTargetBinaryDir { get; set; } = string.Empty;
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
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Missing toml config");
                Console.Error.WriteLine("   onyx-codegen [path to config]");
                return;
            }

            string configPath = args[0];
            var alltext = File.ReadAllText(configPath);
            var config = Tomlyn.Toml.ToModel<Config>(alltext, configPath);

            RunModuleCodeGeneration(config);
            if (config.TargetConfig.IsExecutable)
            {
                RunProjectBootstrapGeneration(config);
            }
        }

        static void RunProjectBootstrapGeneration(Config config)
        {
            var outPath = Path.Combine(config.Paths.BinaryDirectory, config.Paths.GeneratedDirectorySuffix, "private", config.Paths.NamespaceDirectorySuffix, "init.gen.cpp");
            var projectGeneratedCodePath = config.Paths.BinaryDirectory;

            IReadOnlyList<string> includeDirectories = config.TargetConfig.IncludeDirectories;

            IEnumerable<string> generatedSourceFiles = [];
            foreach (var includeDirectory in includeDirectories.Distinct())
            {
                if (string.IsNullOrEmpty(includeDirectory))
                    continue;

                generatedSourceFiles = generatedSourceFiles.Union(Directory.EnumerateFiles(includeDirectory, "*.gen.h", SearchOption.AllDirectories).Select(s => s.Replace('\\', '/')));
            }

            CodeGenerator codeGenerator = new CodeGenerator();
            List<Type> outTypes;
            List<Function> globalFunctions;
            List<string> includes = new List<string>();
            IEnumerable<Function> allGlobalFunctions = Enumerable.Empty<Function>();
            foreach (var moduleHeaderPath in generatedSourceFiles)
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

            using (codeGenerator.EnterScope("namespace onyx"))
            using (codeGenerator.EnterScope("void init()"))
            {
                var registerEngineModuleFunctions = allGlobalFunctions.Where(function => function.Name == "init");
                foreach (var function in registerEngineModuleFunctions)
                {
                    string fullyQualifiedName = function.Namespace + "::" + function.Name;
                    if (fullyQualifiedName.StartsWith("onyx::"))
                    {
                        fullyQualifiedName = fullyQualifiedName["onyx::".Length..];
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
            string namespacePathSuffix = config.Paths.NamespaceDirectorySuffix;
            string editorBinaryDirPath = config.Paths.EditorTargetBinaryDir;

            var outPublicPath = Path.Join(binaryDir, generatedPathSuffix, "public", namespacePathSuffix).Replace('\\', '/');
            var outPrivatePath = Path.Join(binaryDir, generatedPathSuffix, "private", namespacePathSuffix).Replace('\\', '/');

            var editorBinaryPublicPath = string.IsNullOrWhiteSpace(editorBinaryDirPath) ? "" : Path.Join(editorBinaryDirPath, generatedPathSuffix, "public", namespacePathSuffix).Replace('\\', '/');
            var editorBinaryPrivatePath = string.IsNullOrWhiteSpace(editorBinaryDirPath) ? "" : Path.Join(editorBinaryDirPath, generatedPathSuffix, "private", namespacePathSuffix).Replace('\\', '/');

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

            TypeDatabase typeDatabase = new TypeDatabase([sourceDir, outPublicPath]);
            typeDatabase.Init(cppSources, includeDirectories);

            IEnumerable<string> moduleNamespaceStack = targetNamespace.Split("::");

            ComponentGenerator componentGenerator = new ComponentGenerator(typeDatabase,
                sourceDir,
                namespacePathSuffix,
                generatedPathSuffix,
                outPublicPath,
                outPrivatePath,
                editorBinaryPublicPath,
                editorBinaryPrivatePath,
                config.TargetConfig.HasToolsTarget,
                includeDirectories,
                moduleNamespaceStack);

            List<string> generatedComponentFiles = [];
            List<string> generatedComponentEditorFiles = [];

            foreach (var componentDefinition in componentDefinitions)
            {
                componentGenerator.Generate(componentDefinition, generatedComponentFiles, generatedComponentEditorFiles);

                typeDatabase.AddType(generatedComponentEditorFiles.Where(file => file.EndsWith(".h")), includeDirectories);
            }

            string sourcesBasePath = PathExtension.GetShortestRelativePath(includeDirectories, sources.First());
            ModuleGenerator generator = new ModuleGenerator(targetName, sourceDir, binaryDir, moduleNamespaceStack, typeDatabase);
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
