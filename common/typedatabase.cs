using System.Collections.Concurrent;

namespace onyx_codegen.common
{
    internal class TypeDatabase
    {
        private ConcurrentDictionary<string, Type> types = new ConcurrentDictionary<string, Type>();
        private ConcurrentBag<Function> globalFunctions = new ConcurrentBag<Function>();

        public IReadOnlyDictionary<string, Type> Types { get => types; }
         
        internal void Init(IEnumerable<string> sources, IEnumerable<string> includeDirectories)
        {
            Parallel.ForEach(sources, source =>
            {
                CppParser parser = new CppParser(includeDirectories);
                List<Type> parsedTypes;
                List<Function> parsedGlobalFunctions;
                parser.Parse(source, out parsedGlobalFunctions, out parsedTypes);

                foreach (var globalFunction in parsedGlobalFunctions)
                {
                    globalFunctions.Add(globalFunction);
                }

                foreach (var type in parsedTypes)
                {
                    if (types.ContainsKey(type.FullyQualifiedName))
                    {
                        continue;
                    }

                    types[type.FullyQualifiedName] = type;
                }
            });

            globalFunctions = new ConcurrentBag<Function>(globalFunctions.Distinct());

            // sanitize and cleanup inhertied classes
            Dictionary<string, List<string>> typeInheritanceChain = new Dictionary<string, List<string>>();
            foreach(var type in types.Values)
            {
                var inheritanceChain = ResolveFullInhertiance(type.FullyQualifiedName, typeInheritanceChain);
                type.Inherits = inheritanceChain.ToList();
            }
        }

        internal IEnumerable<Type> GetTypes()
        {
            return types.Values;
        }

        internal IEnumerable<Type> GetDerivedTypes(string baseClass)
        {
            return types.Values.Where(t => t.Inherits.Contains(baseClass));
        }

        internal IEnumerable<Type> GetTypesDerivedFromTemplate(string templateBaseClass)
        {
            return types.Values.Where(t => t.Inherits.Any(baseClass => baseClass.StartsWith($"{templateBaseClass}<")));
        }

        private IReadOnlyList<string> ResolveFullInhertiance(string className, Dictionary<string, List<string>> inheritanceCache)
        {
            List<string>? inheritanceChain;
            if (inheritanceCache.TryGetValue(className, out inheritanceChain))
            {
                return inheritanceChain;
            }

            Type? type;
            inheritanceChain = new List<string> { };
            if (types.TryGetValue(className, out type) == false)
            {
                return inheritanceChain;
            }

            int index = type.FullyQualifiedName.LastIndexOf("::");
            string namespaceContext = type.FullyQualifiedName;
            if (index != -1)
            {
                namespaceContext = namespaceContext.Substring(0, index);
            }

            Type ? baseType;
            foreach (var baseClass in type.Inherits)
            {
                baseType = ResolveTypeName(baseClass, namespaceContext);
                if (baseType == null)
                {
                    inheritanceChain.Add(baseClass);
                    continue;
                }

                inheritanceChain.Add(baseType.FullyQualifiedName);
                inheritanceChain.AddRange(ResolveFullInhertiance(baseType.FullyQualifiedName, inheritanceCache));
            }

            inheritanceCache[className] = inheritanceChain.ToList();
            return inheritanceChain;
        }

        private Type? ResolveTypeName(string typeName, string namespaceContext)
        {
            Type? type;
            if (types.TryGetValue(typeName, out type))
                return type;

            string currentNamespace = namespaceContext;
            string fullyQualifiedTypeName = currentNamespace + "::" + typeName;

            if (types.TryGetValue(fullyQualifiedTypeName, out type))
                return type;

            while (currentNamespace.LastIndexOf("::") is int i && i != -1)
            {
                fullyQualifiedTypeName = currentNamespace[0..(i + 2)] + typeName;
                if (types.TryGetValue(fullyQualifiedTypeName, out type))
                    return type;

                currentNamespace = currentNamespace[0..i];
            }

            return null;
        }
    }
}
