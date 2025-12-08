using onyx_codegen.common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace onyx_codegen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var mode = args[0]; // target name
            if (mode == "--project")
            {
                GenerateProjectInit(args);
            }
            else if ( mode == "--module")
            {
                GenerateModule( args );
            }
        }

        static void GenerateProjectInit(string[] args)
        {
            var outPath = args[1].Replace('\\', '/'); // base target output path (binary directory)
            var engineGeneratedCodePath = args[2].Replace('\\', '/'); // base path for generated engine source
            var projectGeneratedCodePath = args[3].Replace('\\', '/'); // base path for generated project source
            var generatedModuleHeadersFile = Path.Combine(projectGeneratedCodePath, "generatedmoduleheaders");
            var includesPath = Path.Combine(projectGeneratedCodePath, "includedirectories"); // include directories of target

            IReadOnlyList<string> includeDirectories = File.ReadAllLines(includesPath);
            IEnumerable<string> generatedModuleHeaderPaths = File.ReadAllLines(generatedModuleHeadersFile).Distinct();
            
            CodeGenerator codeGenerator = new CodeGenerator();

            List<common.Type> outTypes;
            List<common.Function> globalFunctions;
            List<string> includes = new List<string>();
            IEnumerable<common.Function> allGlobalFunctions = Enumerable.Empty<common.Function>();
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

            var sortedIncludes = includes.OrderBy(s => s.Count(c => c == '/'))  // sort by folder depth
              .ThenBy(s => s)                                                   // sort alphabetical
              .Select(s => $"#include <{s}>");

            codeGenerator.Append(sortedIncludes);
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

        static void GenerateModule(string[] args)
        {
            var targetName = args[1].Replace('\\', '/'); // target name
            var targetNamespace = args[2]; // target namespace 
            var basePath = args[3].Replace('\\', '/'); // base target path
            var binaryPath = args[4].Replace("\\", "/");
            var outPublicPath = args[5].Replace('\\', '/'); // base target output path (binary directory)
            var outPrivatePath = args[6].Replace('\\', '/'); // base target output path (binary directory)
            var sourcesPath = Path.Combine(binaryPath, "sourcefiles"); // public sources of target
            var includesPath = Path.Combine(binaryPath, "includedirectories"); // include directories of target

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

            foreach (var includeDirectory in includeDirectories.Distinct())
            {
                if (string.IsNullOrEmpty(includeDirectory))
                    continue;

                sources = sources.Union(Directory.EnumerateFiles(includeDirectory, "*.h", SearchOption.AllDirectories).Select(s => s.Replace('\\', '/')));
            }

            TypeDatabase typeDatabase = new TypeDatabase();
            typeDatabase.Init(sources, includeDirectories);
            
            string sourcesBasePath = PathExtension.GetShortestRelativePath(includeDirectories, sources.First());
            ModuleGenerator generator = new ModuleGenerator(targetName, basePath, targetNamespace.Split("::"), typeDatabase);
            generator.GenerateModule(outPublicPath, outPrivatePath);
        }
    }
}
