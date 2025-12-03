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
                        codeGenerator.Append(functions.Select(function => function.Namespace + "::" + function.Name + "();"));
                    }
                }
            }
            var outPath = args[1].Replace('\\', '/'); // base target output path (binary directory)
            var engineGeneratedCodePath = args[2].Replace('\\', '/'); // base path for generated engine source
            var projectGeneratedCodePath = args[3].Replace('\\', '/'); // base path for generated project source
            var generatedModuleHeadersFile = args[4].Replace('\\', '/'); // file containing paths to all generated modules
            var includesPath = args[5].Replace('\\', '/'); // include directories of target

            IReadOnlyList<string> includeDirectories = File.ReadAllLines(includesPath);
            IEnumerable<string> generatedModuleHeaderPaths = File.ReadAllLines(generatedModuleHeadersFile).Distinct();
            
            CodeGenerator codeGenerator = new CodeGenerator();

            var includes = generatedModuleHeaderPaths.Select(includePath => $"#include <{PathExtension.GetShortestRelativePath(includeDirectories, includePath)}>");

            List<common.Type> outTypes;
            List<Function> globalFunctions;
            IEnumerable<Function> allGlobalFunctions = Enumerable.Empty<Function>();            
            foreach (var moduleHeaderPath in generatedModuleHeaderPaths)
            {
                CppParser parser = new CppParser(includeDirectories);        
                parser.Parse(moduleHeaderPath, out globalFunctions, out outTypes);
                allGlobalFunctions = allGlobalFunctions.Union(globalFunctions);
            }

            codeGenerator.Append(includes);
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
            var outPublicPath = args[4].Replace('\\', '/'); // base target output path (binary directory)
            var outPrivatePath = args[5].Replace('\\', '/'); // base target output path (binary directory)
            var sourcesPath = args[6].Replace('\\', '/'); // public sources of target
            var includesPath = args[7].Replace('\\', '/'); // include directories of target

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
            IReadOnlyList<string> includeDirectories = File.ReadAllLines(includesPath);

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
