using onyx_codegen.common;

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
            void GenerateRegisterCalls(IEnumerable<Function> functions, CodeGenerator codeGenerator, string scopeComment)
            {
                if (functions.Any())
                {
                    using (codeGenerator.EnterScope(scopeComment))
                    {
                        foreach (var function in functions)
                        {
                            string fullyQualifiedName = function.Namespace + "::" + function.Name;
                            if (fullyQualifiedName.StartsWith("Onyx::"))
                            {
                                fullyQualifiedName = fullyQualifiedName["Onyx::".Length..];
                            }

                            codeGenerator.Append($"{fullyQualifiedName}();");
                        }
                    }
                }
            }
            var outPath = args[1].Replace('\\', '/'); // base target output path (binary directory)
            var engineGeneratedCodePath = args[2].Replace('\\', '/'); // base path for generated engine source
            var projectGeneratedCodePath = args[3].Replace('\\', '/'); // base path for generated project source
            var binaryPath = args[4].Replace('\\', '/'); // file containing paths to all generated modules
            var generatedModuleHeadersFile = Path.Combine(binaryPath, "generatedmoduleheaders");
            var includesPath = Path.Combine(binaryPath, "includedirectories"); // include directories of target

            IReadOnlyList<string> includeDirectories = File.ReadAllLines(includesPath);
            IEnumerable<string> generatedModuleHeaderPaths = File.ReadAllLines(generatedModuleHeadersFile).Distinct();
            
            CodeGenerator codeGenerator = new CodeGenerator();

            List<common.Type> outTypes;
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

            var sortedIncludes = includes.OrderBy(s => s.Count(c => c == '/'))  // sort by folder depth
              .ThenBy(s => s)                                                   // sort alphabetical
              .Select(s => $"#include <{s}>");

            codeGenerator.Append(sortedIncludes);
            codeGenerator.AppendLine();
            
            using (codeGenerator.EnterScope("namespace Onyx"))
            using (codeGenerator.EnterFunction("void Init()"))
            {
                var registerEngineSystemFunctions = allGlobalFunctions.Where(function => function.Name == "RegisterEngineSystems");
                var registerAssetsFunctions = allGlobalFunctions.Where(function => function.Name == "RegisterAssets");
                var registerSerializersFunctions = allGlobalFunctions.Where(function => function.Name == "RegisterSerializers");

                GenerateRegisterCalls(registerEngineSystemFunctions, codeGenerator, "// Register Systems");
                if (registerEngineSystemFunctions.Any())
                {
                    codeGenerator.AppendLine();
                }

                GenerateRegisterCalls(registerAssetsFunctions, codeGenerator, "// Register Assets");
                if (registerAssetsFunctions.Any())
                {
                    codeGenerator.AppendLine();
                }

                GenerateRegisterCalls(registerSerializersFunctions, codeGenerator, "// Register Serializers");
            }

            File.WriteAllText(outPath, codeGenerator.GetCode());
        }

        static void GenerateModule(string[] args)
        {
            var targetName = args[1].Replace('\\', '/'); // target name
            var targetNamespace = args[2]; // target namespace 
            var basePath = args[3].Replace('\\', '/'); // base target path
            var binaryPath = args[4].Replace("\\", "/");
            var outPublicPath = args[4].Replace('\\', '/'); // base target output path (binary directory)
            var outPrivatePath = args[5].Replace('\\', '/'); // base target output path (binary directory)
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
            IEnumerable<string> includeDirectories = File.ReadAllLines(includesPath).Where(path => path.StartsWith(@"D:/private/Irrlicht/code"));

            foreach (var includeDirectory in includeDirectories.Distinct())
            {
                if (string.IsNullOrEmpty(includeDirectory))
                    continue;

                sources = sources.Union(Directory.EnumerateFiles(includeDirectory, "*.h", SearchOption.AllDirectories).Select(s => s.Replace('\\', '/')));
            }

            TypeDatabase typeDatabase = new TypeDatabase();
            typeDatabase.Init(sources, includeDirectories);

            var shaderGraphNodes = typeDatabase.GetDerivedTypes("Onyx::Graphics::ShaderGraphNode");
            var renderGraphNodes = typeDatabase.GetDerivedTypes("Onyx::Graphics::IRenderGraphNode");

            string sourcesBasePath = PathExtension.GetShortestRelativePath(includeDirectories, sources.First());
            ModuleGenerator generator = new ModuleGenerator(targetName, basePath, targetNamespace.Split("::"), typeDatabase);
            generator.GenerateModule(outPublicPath, outPrivatePath);
        }
    }
}
