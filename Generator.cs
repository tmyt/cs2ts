using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace cs2ts
{
    public class Generator
    {
        public List<string> Warnings { get; set; } = new List<string>();

        private readonly List<string> _imports = new List<string>();
        private readonly List<string> _exports = new List<string>();

        private readonly List<string> _context = new List<string>();

        public string Generate(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();
            var gen = Parse(root.Members);
            var sortedImports = _imports
                .Where(s => !_exports.Contains(s))
                .OrderBy(s => s)
                .Distinct()
                .Select(type => $"import {{ {type} }} from './{type}';")
                .ToArray();
            var imports = sortedImports.JoinToString("\n");
            return sortedImports.Length == 0 ? gen : $"{imports}\n\n{gen}";
        }

        private string Parse(SyntaxList<MemberDeclarationSyntax> members)
        {
            return members.Select(ParseOne)
                .Where(s => !string.IsNullOrEmpty(s))
                .JoinToString("\n");
        }

        private string ParseOne(MemberDeclarationSyntax syntax)
        {
            switch (syntax)
            {
                case NamespaceDeclarationSyntax ns:
                    return Parse(ns.Members);
                case EnumDeclarationSyntax eds:
                    return ParseEnum(eds);
                case TypeDeclarationSyntax typeDeclaration:
                    return ParseTypeDeclaration(typeDeclaration);
            }
            return "";
        }

        private string ParseTypeDeclaration(TypeDeclarationSyntax syntax)
        {
            // suppress code generation for attribute class
            if (syntax is ClassDeclarationSyntax cls && IsAttributeClassDeclaration(cls))
            {
                return "";
            }
            var segment = $"export type {syntax.Identifier.Text}{ParseTypeParameters(syntax.TypeParameterList)} = {{\n";
            foreach (var member in syntax.Members)
            {
                if (!(member is PropertyDeclarationSyntax prop)) continue;
                if (prop.Modifiers.Any(mod => mod.Kind() == SyntaxKind.OverrideKeyword)) continue;
                if (prop.Modifiers.All(mod => mod.Kind() != SyntaxKind.PublicKeyword)) continue;
                segment += $"  {CamelCase(prop.Identifier.Text)}{ParseNullable(prop.Type)}: {ParseType(prop.Type)};\n";
            }
            segment += $"}}{ParseExtendsSyntax(syntax.BaseList)};\n";
            _exports.Add(syntax.Identifier.Text);
            _context.Clear();
            return segment;
        }

        private string ParseNullable(TypeSyntax syntax)
        {
            return syntax is NullableTypeSyntax ? "?" : "";
        }

        private string ParseTypeParameters(TypeParameterListSyntax parameters)
        {
            if (parameters == null) return "";
            var types = parameters.Parameters.Select(p => p.Identifier.Text).ToArray();
            var segment = "<";
            segment += types.JoinToString(", ");
            segment += ">";
            _context.AddRange(types);
            return segment;
        }

        private string ParseExtendsSyntax(BaseListSyntax syntax)
        {
            if (syntax == null) return "";
            return " & " + syntax.Types.Select(t => ParseType(t.Type)).JoinToString(" & ");
        }

        private string ParseEnum(EnumDeclarationSyntax syntax)
        {
            var segment = $"export enum {syntax.Identifier.Text} {{\n";
            foreach (var member in syntax.Members)
            {
                segment += $"  {member.Identifier.Text}{ParseEnumValue(member.EqualsValue)},\n";
            }
            segment += "}\n";
            _exports.Add(syntax.Identifier.Text);
            return segment;
        }

        private string ParseEnumValue(EqualsValueClauseSyntax syntax)
        {
            if (syntax == null) return "";
            return $" = {ParseExpression(syntax.Value)}";
        }

        private string ParseExpression(ExpressionSyntax syntax)
        {
            switch (syntax)
            {
                case LiteralExpressionSyntax literal:
                    return ParseLiteral(literal);
                case BinaryExpressionSyntax binary:
                    return ParseBinary(binary);
                case ParenthesizedExpressionSyntax parenthesized:
                    return $"({ParseExpression(parenthesized.Expression)})";
            }
            return ParseUnknown(syntax);
        }

        private string ParseLiteral(LiteralExpressionSyntax syntax)
        {
            switch (syntax.Kind())
            {
                case SyntaxKind.DefaultLiteralExpression:
                    return "undefined";
                case SyntaxKind.NullLiteralExpression:
                    return "null";
                case SyntaxKind.CharacterLiteralExpression:
                    return $"{(int)syntax.Token.ValueText[0]} /* {syntax.Token.ValueText} */";
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                    return syntax.Token.Text;
                case SyntaxKind.FalseLiteralExpression:
                    return "false";
                case SyntaxKind.TrueLiteralExpression:
                    return "true";
            }
            return ParseUnknown(syntax);
        }

        private string ParseBinary(BinaryExpressionSyntax syntax)
        {
            switch (syntax.Kind())
            {
                case SyntaxKind.AddExpression:
                    return $"{ParseExpression(syntax.Left)} + {ParseExpression(syntax.Right)}";
                case SyntaxKind.SubtractExpression:
                    return $"{ParseExpression(syntax.Left)} - {ParseExpression(syntax.Right)}";
                case SyntaxKind.MultiplyExpression:
                    return $"{ParseExpression(syntax.Left)} * {ParseExpression(syntax.Right)}";
                case SyntaxKind.DivideExpression:
                    return $"{ParseExpression(syntax.Left)} / {ParseExpression(syntax.Right)}";
                case SyntaxKind.ModuloExpression:
                    return $"{ParseExpression(syntax.Left)} % {ParseExpression(syntax.Right)}";
                case SyntaxKind.LeftShiftExpression:
                    return $"{ParseExpression(syntax.Left)} << {ParseExpression(syntax.Right)}";
                case SyntaxKind.RightShiftExpression:
                    return $"{ParseExpression(syntax.Left)} >> {ParseExpression(syntax.Right)}";
            }
            return ParseUnknown(syntax);
        }

        private string ParseType(TypeSyntax syntax)
        {
            if (syntax is IdentifierNameSyntax identifier)
            {
                if (_context.Contains(identifier.Identifier.Text))
                {
                    return identifier.Identifier.Text;
                }
                switch (identifier.Identifier.Text)
                {
                    case "String":
                    case "DateTime":
                    case "Guid":
                        return "string";
                    case "dynamic":
                        return "any";
                }
                _imports.Add(identifier.Identifier.Text);
                return identifier.Identifier.Text;
            }
            if (syntax is GenericNameSyntax generic)
            {
                var args = generic.TypeArgumentList.Arguments;
                switch (generic.Identifier.Text)
                {
                    case "List":
                    case "IList":
                    case "ICollection":
                    case "IEnumerable":
                        return $"{ParseType(args[0])}[]";
                    case "Dictionary":
                        return $"{{ [ key: {ParseType(args[0])} ]: {ParseType(args[1])} }}";
                }
                _imports.Add(generic.Identifier.Text);
                return $"{generic.Identifier.Text}<{generic.TypeArgumentList.Arguments.Select(ParseType).JoinToString(", ")}>";
            }
            if (syntax is QualifiedNameSyntax qualifier)
            {
                return ParseType(qualifier.Right);
            }
            if (syntax is PredefinedTypeSyntax predefined)
            {
                switch (predefined.Keyword.Kind())
                {
                    case SyntaxKind.BoolKeyword:
                        return "boolean";
                    case SyntaxKind.ByteKeyword:
                    case SyntaxKind.SByteKeyword:
                    case SyntaxKind.CharKeyword:
                    case SyntaxKind.DecimalKeyword:
                    case SyntaxKind.DoubleKeyword:
                    case SyntaxKind.FloatKeyword:
                    case SyntaxKind.IntKeyword:
                    case SyntaxKind.UIntKeyword:
                    case SyntaxKind.LongKeyword:
                    case SyntaxKind.ULongKeyword:
                    case SyntaxKind.ShortKeyword:
                    case SyntaxKind.UShortKeyword:
                        return "number";
                    case SyntaxKind.ObjectKeyword:
                        return "any";
                    case SyntaxKind.StringKeyword:
                        return "string";
                }
            }
            if (syntax is NullableTypeSyntax nullable)
            {
                return ParseType(nullable.ElementType);
            }
            if (syntax is ArrayTypeSyntax array)
            {
                return $"{ParseType(array.ElementType)}[]";
            }
            return ParseUnknown(syntax);
        }

        private string CamelCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return $"{s[0].ToString().ToLowerInvariant()}{s.Substring(1)}";
        }

        private string ParseUnknown(SyntaxNode node)
        {
            var location = node.GetLocation();
            var lines = location.GetLineSpan();
            var pos = lines.StartLinePosition;
            var content = node.GetText().ToString().Trim();
            Warnings.Add($"(Line: {pos.Line}:{pos.Character}): Could not recognize {content}");
            return $"<???> /* @{pos.Line}:{pos.Character} {content} */";
        }

        private bool IsAttributeClassDeclaration(ClassDeclarationSyntax syntax)
        {
            if (syntax.BaseList == null) return false;
            var hasAttribute = syntax.BaseList.Types
                .Select(type => type.Type)
                .OfType<IdentifierNameSyntax>()
                .Any(type => type.Identifier.Text == "Attribute");
            return syntax.Identifier.Text.EndsWith("Attribute") && hasAttribute;
        }
    }

    internal static class StringEnumerable
    {
        public static string JoinToString(this IEnumerable<string> source, string separator)
        {
            return string.Join(separator, source);
        }
    }
}
