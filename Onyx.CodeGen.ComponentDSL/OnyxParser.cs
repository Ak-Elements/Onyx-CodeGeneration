using Onyx.CodeGen.Core;
using Onyx.CodeGen.TreeSitter;

namespace Onyx.CodeGen.ComponentDSL
{
    public class OnyxParser
    {
        private string source = "";
        private string filePath = "";
        private string generatedIncludePath = "";

        private TypeDatabase typeDatabase;

        private IEnumerable<string> includeDirectories = [];
        private IEnumerable<string> namespaceStack = [];

        internal OnyxParser(TypeDatabase typeDatabase, IEnumerable<string> includeDirectories, IEnumerable<string> namespaceStack)
        {
            this.typeDatabase = typeDatabase;
            this.includeDirectories = includeDirectories;
            this.namespaceStack = namespaceStack;
        }

        internal void Parse(string path, out List<Component> outComponents)
        {
            outComponents = new();
            filePath = path;

            source = File.ReadAllText(path);

            using var parser = new TSParser();
            var grammar = TreeSitterLanguageOnyx.tree_sitter_onyx();

            TSLanguage language = new TSLanguage(grammar);
            bool setLanguage = parser.set_language(language);

            using var tree = parser.parse_string(null, source);
            if (tree == null)
                return;

            var cursor = new TSCursor(tree.root_node(), language);
            Walk(cursor, ref outComponents, 0);
        }

        void Walk(TSCursor cursor, ref List<Component> outComponents, uint depth)
        {
            string currentSymbol = cursor.current_symbol();
            switch (currentSymbol)
            {
                case "component_declaration":
                    var component = ParseComponent(cursor);
                    outComponents.Add(component);
                    break;
            }

            // recurse into children for all other nodes
            foreach (var child in cursor.children())
            {
                Walk(cursor, ref outComponents, depth + 1);
            }
        }

        private List<Attribute> ParseAttributes(TSCursor cursor)
        {
            List<Attribute> attributes = [];
            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "attribute":
                        {
                            if (ParseAttribute(child) is Attribute attribute)
                            {
                                attributes.Add(attribute);
                            }
                            break;
                        }
                }
            }
            return attributes;
        }

        private Attribute? ParseAttribute(TSCursor cursor)
        {
            ReadOnlySpan<char> type = "";
            List<string> parameters = [];

            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "attribute_name":
                        type = child.GetContent(source);
                        break;
                    case "attribute_arguments":
                        foreach (var arg in child.children())
                        {
                            var symbol = arg.current_symbol();
                            if ((symbol == "(") || (symbol == ")"))
                                continue;

                            //TODO: Do we want to actually parse the designated intializer
                            parameters.Add(arg.GetContent(source).ToString());
                        }
                        break;
                }
            }

            if (Attributes.GetAttribute(type.Trim().ToString(), [.. parameters]) is Attribute newAttribute)
            {
                return newAttribute;
            }

            return null;
        }

        private Component ParseComponent(TSCursor cursor)
        {
            ReadOnlySpan<char> name = "";
            List<Field> fields = [];
            List<Attribute> attributes = [];

            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "identifier":
                        name = child.current_node().text(source);
                        break;
                    case "declaration_list":
                        fields = ParseFields(child);
                        break;
                    case "attribute_list":
                        attributes.AddRange(ParseAttributes(child));
                        break;
                }
            }

            string fullyQualifiedNamespace = string.Join("::", namespaceStack);

            Component newComponent = new Component();
            newComponent.Name = name.ToString();
            newComponent.FullyQualifiedName = $"{fullyQualifiedNamespace}::{newComponent.Name}";
            newComponent.Path = filePath;
            newComponent.Fields = fields;
            newComponent.Attributes = attributes;
            return newComponent;
        }

        private List<Field> ParseFields(TSCursor cursor)
        {
            List<Field> fields = [];
            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "field_declaration":
                        fields.Add(ParseField(child));
                        break;
                    case "enum_declaration":
                        break;
                }
            }
            return fields;
        }

        private Field ParseField(TSCursor cursor)
        {
            Core.Type? type = null;
            string formatLiteral = string.Empty;
            ReadOnlySpan<char> typeName = "";
            ReadOnlySpan<char> name = "";
            ReadOnlySpan<char> defaultValue = "";

            List<Attribute> attributes = [];

            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "type_identifier":
                        typeName = child.GetContent(source);
                        type = typeDatabase.ResolveTypeName(typeName.ToString(), namespaceStack);
                        formatLiteral = GetFormatLiteral(type);
                        break;
                    case "identifier":
                        name = child.GetContent(source);
                        break;
                    case "field_initializer":
                        defaultValue = ParseFieldInitializer(child, formatLiteral);
                        break;
                    case "attribute_list":
                        attributes = ParseAttributes(child);
                        break;
                }
            }

            List<Core.Type> specializedTemplateTypes = [];
            if (type is TemplateType)
            {
                specializedTemplateTypes = typeDatabase.ResolveSpecializedTemplateTypes(typeName.ToString(), namespaceStack);
            }

            Field field = new();
            field.Name = name.ToString();
            field.OcdTypeName = typeName.ToString();
            field.Type = type;
            field.SpecializedTemplateTypes = specializedTemplateTypes;
            field.DefaultValue = defaultValue.ToString();
            field.Attributes = attributes;
            return field;
        }

        private string ParseFieldInitializer(TSCursor cursor, string formatLiteral)
        {
            List<string> initializer = [];
            foreach (var child in cursor.children())
            {
                switch (child.current_symbol())
                {
                    case "assignment_expression":
                        initializer.Add(ParseFieldInitializer(child, formatLiteral));
                        break;
                    case "initializer_list":
                        initializer.Add($"{{ {string.Join(", ", ParseInitializerList(child, formatLiteral))} }}");
                        break;
                    case "bool_literal":
                    case "string_literal":
                        initializer.Add(child.GetContent(source).ToString());
                        break;
                    case "float_literal":
                    case "integer_literal":
                        string value = child.GetContent(source).ToString();

                        if (value.EndsWith(formatLiteral) == false)
                            value += formatLiteral;

                        initializer.Add(value);
                        break;
                }
            }
            return string.Join(',', initializer);
        }

        private List<string> ParseInitializerList(TSCursor cursor, string formatLiteral)
        {
            List<string> initializerList = [];
            foreach (var child in cursor.children())
            {
                if (child.current_node().is_named() == false)
                    continue;

                var argument = child.GetContent(source).ToString();
                if (argument.EndsWith(formatLiteral) == false)
                    argument += formatLiteral;

                initializerList.Add(argument);
            }
            return initializerList;
        }

        private string GetFormatLiteral(Core.Type? type)
        {
            if (type != null && DSLTypes.TYPE_TO_LITERAL_SUFFIX.TryGetValue(type.Name, out string? literalSuffix))
                return literalSuffix;

            return "";
        }
    }
}
