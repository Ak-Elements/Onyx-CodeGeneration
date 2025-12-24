using Onyx.Codegen.Core;
using Onyx.CodeGen.Core;
using System.Reflection;

namespace Onyx.CodeGen.ComponentDSL
{
    public static class Attributes
    {
        private static readonly IEnumerable<System.Type> ANNOTATION_ATTRIBUTES = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => !p.IsGenericType)
            .Where(p => typeof(Attribute).IsAssignableFrom(p));

        internal static Attribute? GetAttribute(string attributeTypeName, params string[] parameters)
        {
            var attributeType = ANNOTATION_ATTRIBUTES.Where(attributeType =>
            {
                var typeName = attributeType.Name.EndsWith("Attribute") ? attributeType.Name.Replace("Attribute", "") : attributeType.Name;
                
                var nameAttribute = attributeType.GetCustomAttribute<NameAttribute>(inherit: true);
                string attributeName = nameAttribute?.Value ?? typeName;
                return attributeName.Equals(attributeTypeName, StringComparison.OrdinalIgnoreCase);

            }).FirstOrDefault();

            if (attributeType != null)
            {
                if ( parameters.IsNullOrEmpty() )
                {
                    return Activator.CreateInstance(attributeType) as Attribute;
                }
                else
                {
                    var constructors = attributeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance );
                    bool hasMatchingConstructor = constructors.Any( constructor => constructor.GetParameters().Length == parameters.Length );

                    if (hasMatchingConstructor)
                    {
                        var ctorArguments = constructors.First().GetParameters();

                        List<object> arguments = [];
                        for( int i = 0; i < ctorArguments.Length; ++i )
                        {
                            arguments.Add(Convert.ChangeType(parameters[i], ctorArguments[i].ParameterType));
                        }

                        return Activator.CreateInstance(attributeType, arguments.ToArray()) as Attribute;
                    }
                    else
                    {
                        // TODO: NAMED CONSTRUCT
                        Attribute newAttribute = Activator.CreateInstance(attributeType) as Attribute;
                        foreach (var param in parameters)
                        {
                            // is named parameter
                            if (param.Contains('='))
                            {
                                string[] paramParts = param.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                var property = attributeType.GetProperty(paramParts[0], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (property.PropertyType.IsEnum)
                                {
                                    property.SetValue(newAttribute, Enum.Parse(property.PropertyType, paramParts[1]));
                                }
                                else
                                {
                                    property.SetValue(newAttribute, Convert.ChangeType(paramParts[1], property.PropertyType));
                                }                                
                            }
                        }

                        return newAttribute;
                    }
                    
                }
            }

            return null;
        }
    }

   
}
