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

            foreach (var type in types.Values)
            {
                if (type.IsAliased == false)
                    continue;

                var aliasedTypeName = type.AliasedType;
                var templateIndex = aliasedTypeName.IndexOf('<');
                if (templateIndex != -1)
                    aliasedTypeName = aliasedTypeName[0..templateIndex];

                if (string.IsNullOrEmpty(aliasedTypeName))
                    continue;
                    
                Type? aliasedType = ResolveTypeName(aliasedTypeName, type.Namespace);
                if (aliasedType == null)
                    continue;

                if (type.Name.Contains("ShaderAddF32"))
                {
                    Console.WriteLine();
                }

                if (aliasedType is TemplateType aliasedTemplateType)
                {
                    type.AliasedType = aliasedType.FullyQualifiedName + type.AliasedType[type.AliasedType.IndexOf('<')..];
                    List<string> inherits = aliasedType.Inherits.ToList();

                    for (int i = 0; i < aliasedType.Inherits.Count; ++i)
                    {
                        var baseClass = aliasedType.Inherits[i];
                        for (int j = 0; j < aliasedTemplateType.TemplateParameters.Count; ++j)
                        {
                            if (baseClass.Contains(aliasedTemplateType.TemplateParameters[j]))
                            {
                                baseClass = baseClass.Replace(aliasedTemplateType.TemplateParameters[j], type.SpecializedTemplateParameters[j]);
                            }
                        }

                        inherits[i] = baseClass;
                    }

                    type.Inherits = inherits;
                } 
                else
                {
                    type.AliasedType = aliasedType.FullyQualifiedName;
                    type.Inherits = aliasedType.Inherits;
                }

                type.IsAbstract = aliasedType.IsAbstract;
                type.HasTypeId = aliasedType.HasTypeId;
               

               
            }

            // sanitize and cleanup inhertied classes
            Dictionary<string, List<string>> typeInheritanceChain = new Dictionary<string, List<string>>();
            foreach(var type in types.Values)
            {
                var inheritanceChain = ResolveFullInhertiance(type, typeInheritanceChain);
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

        private IReadOnlyList<string> ResolveFullInhertiance(Type type, Dictionary<string, List<string>> inheritanceCache)
        {
            if (type.Name.Contains("ShaderAddF32"))
            {
                Console.WriteLine();
            }


            List<string> baseClasses = type.Inherits.ToList();
           //if (type.IsAliased)
           //{
           //    var aliasedTypeName = type.AliasedType;
           //    var templateIndex = aliasedTypeName.IndexOf('<');
           //    if (templateIndex != -1)
           //        aliasedTypeName = aliasedTypeName[0..templateIndex];
           //
           //    Type? aliasedType = ResolveTypeName(aliasedTypeName, type.Namespace);
           //    if (aliasedType == null)
           //    {
           //        return type.Inherits.ToList();
           //    }
           //
           //    IReadOnlyList<string>? specializedTemplateArguments = null;
           //    if (type is TemplateType templatedType)
           //    {
           //        specializedTemplateArguments = templatedType.SpecializedTemplateParameters;
           //    }
           //
           //    if (aliasedType is TemplateType aliasedTemplateType)
           //    {
           //        if (specializedTemplateArguments == null)
           //        {
           //            specializedTemplateArguments = aliasedTemplateType.SpecializedTemplateParameters;
           //        }
           //
           //        for (int i = 0; i < aliasedType.Inherits.Count; ++i)
           //        {
           //            if (aliasedTemplateType.TemplateParameters.Contains(aliasedType.Inherits[i]))
           //            {
           //                baseClasses.Add(specializedTemplateArguments[i]);
           //            }
           //        }
           //    }
           //    //return ResolveFullInhertiance(aliasedType, inheritanceCache);
           //}

            List<string>? inheritanceChain;
            var fullyQualifiedName = type.FullyQualifiedName;

            if (inheritanceCache.TryGetValue(fullyQualifiedName, out inheritanceChain))
            {
                return inheritanceChain;
            }

            inheritanceChain = new List<string> { };

            Type? baseType;
            foreach (var baseClass in baseClasses)
            {
                var templateIndex = baseClass.IndexOf('<');
                var strippedTemplate = baseClass;
                var templateParameters = "";
                if (templateIndex != -1)
                {
                    strippedTemplate = baseClass[0..templateIndex];
                    templateParameters = baseClass[(templateIndex + 1)..^1];
                }

                baseType = ResolveTypeName(strippedTemplate, type.Namespace);
                if (baseType == null)
                {
                    inheritanceChain.Add(baseClass);
                    continue;
                }

                var baseFullyQualifiedName = baseType.FullyQualifiedName;
                if (templateIndex != -1)
                {
                    baseFullyQualifiedName += baseClass[templateIndex..];
                }
                inheritanceChain.Add(baseFullyQualifiedName);

                if (baseType is TemplateType templateBaseType)
                {
                    // check for if a template argument is used as inhertiance, if it is add that to the chain instead of the template name
                    bool isDerivedFromTemplateArg = templateBaseType.TemplateParameters.Any(templateBaseType.IsDerivedFrom);
                    if (isDerivedFromTemplateArg)
                    {
                        var templateArguments = templateParameters.Split(',');
                        for (int i = 0; i < templateBaseType.Inherits.Count; i++)
                        {
                            if (templateBaseType.TemplateParameters.Contains(templateBaseType.Inherits[i]))
                            {
                                Type? templateBase = ResolveTypeName(templateArguments[i], type.Namespace);
                                if (templateBase == null)
                                {
                                    inheritanceChain.Add(templateArguments[i]);
                                    continue;
                                }

                                inheritanceChain.Add(templateBase.FullyQualifiedName);
                                inheritanceChain.AddRange(ResolveFullInhertiance(templateBase, inheritanceCache));
                            }
                            else
                            {
                                Type? nonTemplateBase = ResolveTypeName(templateBaseType.Inherits[i], type.Namespace);
                                if (nonTemplateBase == null)
                                {
                                    inheritanceChain.Add(templateBaseType.Inherits[i]);
                                    continue;
                                }

                                inheritanceChain.Add(nonTemplateBase.FullyQualifiedName);
                                inheritanceChain.AddRange(ResolveFullInhertiance(nonTemplateBase, inheritanceCache));
                            }
                        }

                        continue;
                    }
                }

                inheritanceChain.AddRange(ResolveFullInhertiance(baseType, inheritanceCache));
            }

            inheritanceCache[type.FullyQualifiedName] = inheritanceChain.ToList();
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

            // try to get closest match to typename even if namespace did not match
            return types.SingleOrDefault(type => type.Value.Name.Equals(typeName)).Value;
        }
    }
}
