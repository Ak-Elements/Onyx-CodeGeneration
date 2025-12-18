using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Type = Onyx.CodeGen.Core.Type;

namespace Onyx.CodeGen.ComponentDSL
{
    internal static class Editors
    {
        private static readonly IEnumerable<System.Type> FIELD_EDITORS = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => !p.IsGenericType)
            .Where(p => typeof(IFieldEditor).IsAssignableFrom(p));

        internal static IFieldEditor? GetEditor(string editorName)
        {
            var editorType = FIELD_EDITORS.Where(editorType =>
            {
                var editorAttribute = editorType.GetCustomAttribute<Editor>(inherit: true);
                string editorName = editorAttribute?.Value ?? editorType.Name;
                return editorName.Equals(editorName, StringComparison.OrdinalIgnoreCase);

            }).FirstOrDefault();

            if (editorType == null)
            {
                return null;
            }

            return Activator.CreateInstance(editorType) as IFieldEditor ?? null; ;
        }

        internal static IFieldEditor? GetEditor(Field field)
        {
            var editorType = FIELD_EDITORS.Where(editorType =>
            {
                if (editorType.GetCustomAttribute<AllowedTypes>(inherit: true) is AllowedTypes allowedTypes)
                {
                    return allowedTypes.Types.Any(type => 
                    {
                        if (field.Type == null)
                        {
                            return type == field.TypeName;
                        }
                        else
                        {
                            Type cppType = field.Type;
                            return (type == cppType.Name) || (type == cppType.FullyQualifiedName) || (cppType.IsAliased && cppType.AliasedType == type);
                        }
                        
                    });
                }

                return false;

            }).FirstOrDefault();

            if (editorType == null)
            {
                return null;
            }

            return Activator.CreateInstance(editorType) as IFieldEditor ?? null;
        }
    }
}
