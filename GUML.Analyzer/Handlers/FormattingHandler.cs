using GUML.Analyzer.Utils;
using GUML.Analyzer.Workspace;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Analyzer.Handlers;

/// <summary>
/// Provides document formatting by walking the CST and producing text edits.
/// </summary>
public static class FormattingHandler
{
    /// <summary>
    /// Formats a GUML document (or a range within it) and returns a list of text edits.
    /// </summary>
    public static List<TextEdit> Format(GumlDocument document, FormattingOptions options, LspRange? range = null)
    {
        string text = document.Text;
        string indent = options.InsertSpaces ? new string(' ', options.TabSize) : "\t";

        var formatted = FormatNode(document.Root, 0, indent, options.TabSize);
        string formattedText = formatted.ToString();

        if (formattedText == text)
            return new List<TextEdit>();

        if (range == null)
        {
            // Full document format: single edit replacing all text
            var mapper = new PositionMapper(text);
            var docEnd = mapper.GetPosition(text.Length);
            return [new() { Range = new LspRange(new LspPosition(0, 0), docEnd), NewText = formattedText }];
        }

        // Range format: compute per-line diff edits within the requested range
        return ComputeRangeEdits(text, formattedText, range.Value);
    }

    /// <summary>
    /// Computes text edits for a range formatting request by comparing original and
    /// formatted text line-by-line within the requested range.
    /// </summary>
    private static List<TextEdit> ComputeRangeEdits(string original, string formatted, LspRange range)
    {
        string[] origLines = SplitKeepEndings(original);
        string[] fmtLines = SplitKeepEndings(formatted);
        var edits = new List<TextEdit>();

        int startLine = range.Start.Line;
        int endLine = range.End.Line;
        // If cursor is at the start of the end line, don't include that line
        if (range.End.Character == 0 && endLine > startLine)
            endLine--;

        if (origLines.Length != fmtLines.Length)
        {
            // Line count changed — replace the entire requested range
            int endOrig = Math.Min(endLine, origLines.Length - 1);
            int endFmt = Math.Min(endLine, fmtLines.Length - 1);

            string origRegion = string.Join("", origLines, startLine, endOrig - startLine + 1);
            string fmtRegion = string.Join("", fmtLines, startLine,
                Math.Min(endFmt - startLine + 1, fmtLines.Length - startLine));

            if (origRegion != fmtRegion)
            {
                int lastOrigLineLen = origLines[endOrig].TrimEnd('\n', '\r').Length;
                edits.Add(new TextEdit
                {
                    Range = new LspRange(
                        new LspPosition(startLine, 0),
                        new LspPosition(endOrig, lastOrigLineLen)),
                    NewText = fmtRegion
                });
            }

            return edits;
        }

        // Line counts match: emit per-line edits for changed lines in range
        int maxLine = Math.Min(endLine, origLines.Length - 1);
        for (int line = startLine; line <= maxLine; line++)
        {
            if (origLines[line] != fmtLines[line])
            {
                int origLineLen = origLines[line].TrimEnd('\n', '\r').Length;
                edits.Add(new TextEdit
                {
                    Range = new LspRange(
                        new LspPosition(line, 0),
                        new LspPosition(line, origLineLen)),
                    NewText = fmtLines[line].TrimEnd('\n', '\r')
                });
            }
        }

        return edits;
    }

    /// <summary>
    /// Splits text into lines preserving line endings on each line.
    /// </summary>
    private static string[] SplitKeepEndings(string text)
    {
        var lines = new List<string>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(text[start..(i + 1)]);
                start = i + 1;
            }
            else if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    lines.Add(text[start..(i + 2)]);
                    start = i + 2;
                    i++;
                }
                else
                {
                    lines.Add(text[start..(i + 1)]);
                    start = i + 1;
                }
            }
        }

        if (start < text.Length)
            lines.Add(text[start..]);

        return lines.ToArray();
    }

    // ── Formatting engine ──

    private static FormattedBuilder FormatNode(SyntaxNode node, int depth, string indent, int tabSize)
    {
        var builder = new FormattedBuilder();

        switch (node)
        {
            case GumlDocumentSyntax doc:
                FormatDocument(builder, doc, depth, indent, tabSize);
                break;
            case ImportDirectiveSyntax import:
                FormatImport(builder, import, depth, indent);
                break;
            case ComponentDeclarationSyntax comp:
                FormatComponent(builder, comp, depth, indent, tabSize);
                break;
            case PropertyAssignmentSyntax prop:
                FormatPropertyAssignment(builder, prop, depth, indent, tabSize);
                break;
            case MappingAssignmentSyntax mapping:
                FormatMappingAssignment(builder, mapping, depth, indent, tabSize);
                break;
            case EventSubscriptionSyntax evt:
                FormatEventSubscription(builder, evt, depth, indent, tabSize);
                break;
            case ParameterDeclarationSyntax param:
                FormatParameterDeclaration(builder, param, depth, indent, tabSize);
                break;
            case EventDeclarationSyntax eventDecl:
                FormatEventDeclaration(builder, eventDecl, depth, indent);
                break;
            case EachBlockSyntax each:
                FormatEachBlock(builder, each, depth, indent, tabSize);
                break;
            case TemplateParamAssignmentSyntax templateParam:
                FormatTemplateParam(builder, templateParam, depth, indent, tabSize);
                break;
            default:
                // Fallback: emit original text
                FormatFallback(builder, node);
                break;
        }

        return builder;
    }

    private static void FormatDocument(FormattedBuilder builder, GumlDocumentSyntax doc, int depth, string indent,
        int tabSize)
    {
        foreach (var import in doc.Imports)
        {
            EmitLeadingComments(builder, import, depth, indent);
            FormatImport(builder, import, depth, indent);
            EmitTrailingComment(builder, import);
            builder.AppendLine();
        }

        if (doc.Imports.Count > 0)
            builder.AppendLine();

        EmitLeadingComments(builder, doc.RootComponent, depth, indent, skipDocComments: true);
        FormatComponent(builder, doc.RootComponent, depth, indent, tabSize);
        builder.AppendLine();
    }

    private static void FormatImport(FormattedBuilder builder, ImportDirectiveSyntax import, int depth, string indent)
    {
        builder.AppendIndent(depth, indent);
        builder.Append("import ");
        builder.Append(import.Path.Text);
        if (import.Alias != null)
        {
            builder.Append(" as ");
            builder.Append(import.Alias.Name.Text);
        }
    }

    private static void FormatComponent(FormattedBuilder builder, ComponentDeclarationSyntax comp, int depth,
        string indent, int tabSize)
    {
        // Documentation comment
        if (comp.DocumentationComment != null)
        {
            foreach (var trivia in comp.DocumentationComment.DescendantTokens())
            {
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
            }
        }

        builder.AppendIndent(depth, indent);
        if (comp.AliasPrefix != null)
        {
            builder.Append(comp.AliasPrefix.AliasRef.Text);
            builder.Append(":");
        }

        builder.Append(comp.TypeName.Text);
        builder.Append(" {");
        EmitTrailingComment(builder, comp.OpenBrace);
        builder.AppendLine();

        for (int mi = 0; mi < comp.Members.Count; mi++)
        {
            var member = comp.Members[mi];

            // Determine if there should be a blank line separator before this member.
            // Preserve a single blank line if the original source had one (collapse multiple to one).
            bool wantBlankLine = mi > 0 && HasBlankLineBefore(member);

            // Emit standalone comment lines before this member
            bool isChildComp = member is ComponentDeclarationSyntax;
            bool emittedComments = EmitLeadingComments(builder, member, depth + 1, indent,
                skipDocComments: isChildComp, blankLineBefore: wantBlankLine);

            // If no comments were emitted but we want a blank line, emit it now
            if (wantBlankLine && !emittedComments)
                builder.AppendLine();

            switch (member)
            {
                case PropertyAssignmentSyntax prop:
                    FormatPropertyAssignment(builder, prop, depth + 1, indent, tabSize);
                    break;
                case MappingAssignmentSyntax mapping:
                    FormatMappingAssignment(builder, mapping, depth + 1, indent, tabSize);
                    break;
                case EventSubscriptionSyntax evt:
                    FormatEventSubscription(builder, evt, depth + 1, indent, tabSize);
                    break;
                case ComponentDeclarationSyntax child:
                    FormatComponent(builder, child, depth + 1, indent, tabSize);
                    break;
                case ParameterDeclarationSyntax param:
                    FormatParameterDeclaration(builder, param, depth + 1, indent, tabSize);
                    break;
                case EventDeclarationSyntax eventDecl:
                    FormatEventDeclaration(builder, eventDecl, depth + 1, indent);
                    break;
                case EachBlockSyntax each:
                    FormatEachBlock(builder, each, depth + 1, indent, tabSize);
                    break;
                case TemplateParamAssignmentSyntax templateParam:
                    FormatTemplateParam(builder, templateParam, depth + 1, indent, tabSize);
                    break;
            }

            // Comma handling per §8.1.3:
            // - Required between adjacent value-assignment members
            // - Optional (preserve original) as trailing comma
            // - Not required before/after structural elements (components, each blocks)
            if (IsValueAssignment(member))
            {
                bool nextIsValueAssignment = mi + 1 < comp.Members.Count
                    && IsValueAssignment(comp.Members[mi + 1]);
                if (nextIsValueAssignment)
                {
                    builder.Append(",");
                }
                else
                {
                    // Trailing comma: preserve if original had one
                    if (GetCommaToken(member) != null)
                        builder.Append(",");
                }

                EmitTrailingComment(builder, member);
                builder.AppendLine();
            }
        }

        // Emit comments before closing brace
        EmitTokenLeadingComments(builder, comp.CloseBrace, depth + 1, indent);
        builder.AppendIndent(depth, indent);
        builder.Append("}");
        builder.AppendLine();
    }

    private static void FormatPropertyAssignment(FormattedBuilder builder, PropertyAssignmentSyntax prop, int depth,
        string indent, int _)
    {
        builder.AppendIndent(depth, indent);
        builder.Append(prop.Name.Text);
        builder.Append(": ");
        FormatExpression(builder, prop.Value);
    }

    private static void FormatMappingAssignment(FormattedBuilder builder, MappingAssignmentSyntax mapping, int depth,
        string indent, int _)
    {
        builder.AppendIndent(depth, indent);
        builder.Append(mapping.Name.Text);
        builder.Append(" ");
        builder.Append(mapping.Operator.Text);
        builder.Append(" ");
        FormatExpression(builder, mapping.Value);
    }

    private static void FormatEventSubscription(FormattedBuilder builder, EventSubscriptionSyntax evt, int depth,
        string indent, int _)
    {
        builder.AppendIndent(depth, indent);
        builder.Append(evt.EventRef.Text);
        builder.Append(": ");
        FormatExpression(builder, evt.Handler);
    }

    private static void FormatParameterDeclaration(FormattedBuilder builder, ParameterDeclarationSyntax param,
        int depth, string indent, int _)
    {
        // Documentation comment
        if (param.DocumentationComment != null)
        {
            foreach (var trivia in param.DocumentationComment.DescendantTokens())
            {
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
            }
        }

        builder.AppendIndent(depth, indent);
        builder.Append("param ");
        builder.Append(param.TypeName.Text);
        builder.Append(" ");
        builder.Append(param.Name.Text);
        if (param.DefaultValue != null)
        {
            string op = param.DefaultOperator?.Text.Trim() ?? ":";
            builder.Append($" {op} ");
            FormatExpression(builder, param.DefaultValue);
        }
    }

    private static void FormatEventDeclaration(FormattedBuilder builder, EventDeclarationSyntax eventDecl, int depth,
        string indent)
    {
        // Documentation comment
        if (eventDecl.DocumentationComment != null)
        {
            foreach (var trivia in eventDecl.DocumentationComment.DescendantTokens())
            {
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
            }
        }

        builder.AppendIndent(depth, indent);
        builder.Append("event ");
        builder.Append(eventDecl.Name.Text);
        if (eventDecl.Arguments != null)
        {
            builder.Append("(");
            bool first = true;
            foreach (var arg in eventDecl.Arguments)
            {
                if (!first) builder.Append(", ");
                builder.Append(arg.TypeName.Text);
                if (arg.Name != null)
                {
                    builder.Append(" ");
                    builder.Append(arg.Name.Text);
                }

                first = false;
            }

            builder.Append(")");
        }
    }

    private static void FormatEachBlock(FormattedBuilder builder, EachBlockSyntax each, int depth, string indent,
        int tabSize)
    {
        builder.AppendIndent(depth, indent);
        builder.Append("each ");

        // Optional params: each ({ options }) source
        if (each.Params != null)
        {
            builder.Append("({ ");
            bool first = true;
            foreach (var prop in each.Params.Properties)
            {
                if (!first) builder.Append(", ");
                builder.Append(prop.Name.Text);
                builder.Append(": ");
                FormatExpression(builder, prop.Value);
                first = false;
            }

            builder.Append(" }) ");
        }

        FormatExpression(builder, each.DataSource);

        if (each is { FatArrow: not null, ProjectionName: not null })
        {
            // Projection form: each source => paramName
            builder.Append(" => ");
            builder.Append(each.ProjectionName.Text);
            EmitTrailingComment(builder, each);
            builder.AppendLine();
        }
        else if (each.Body != null)
        {
            // Block form: each source { |idx, val| body }
            builder.Append(" {");

            if (each.IndexName != null || each.ValueName != null)
            {
                builder.Append(" |");
                if (each.IndexName != null)
                {
                    builder.Append(each.IndexName.Text);
                    if (each.ValueName != null)
                    {
                        builder.Append(", ");
                        builder.Append(each.ValueName.Text);
                    }
                }

                builder.Append("|");
            }

            if (each.OpenBrace != null)
                EmitTrailingComment(builder, each.OpenBrace);
            builder.AppendLine();

            for (int bi = 0; bi < each.Body.Count; bi++)
            {
                var member = each.Body[bi];
                if (member is ComponentDeclarationSyntax child)
                {
                    bool wantBlank = bi > 0 && HasBlankLineBefore(child);
                    bool emitted = EmitLeadingComments(builder, child, depth + 1, indent,
                        skipDocComments: true, blankLineBefore: wantBlank);
                    if (wantBlank && !emitted)
                        builder.AppendLine();
                    FormatComponent(builder, child, depth + 1, indent, tabSize);
                }
            }

            if (each.CloseBrace != null)
                EmitTokenLeadingComments(builder, each.CloseBrace, depth + 1, indent);
            builder.AppendIndent(depth, indent);
            builder.Append("}");
            builder.AppendLine();
        }
        else
        {
            EmitTrailingComment(builder, each);
            builder.AppendLine();
        }
    }

    private static void FormatTemplateParam(FormattedBuilder builder, TemplateParamAssignmentSyntax templateParam,
        int depth, string indent, int tabSize)
    {
        builder.AppendIndent(depth, indent);
        builder.Append(templateParam.Name.Text);
        builder.Append(" => ");
        FormatComponent(builder, templateParam.Component, depth, indent, tabSize);
    }

    private static void FormatExpression(FormattedBuilder builder, ExpressionSyntax expr)
    {
        switch (expr)
        {
            case LiteralExpressionSyntax lit:
                builder.Append(lit.Token.Text);
                break;
            case ReferenceExpressionSyntax refExpr:
                builder.Append(refExpr.Identifier.Text);
                break;
            case MemberAccessExpressionSyntax memberAccess:
                FormatExpression(builder, memberAccess.Expression);
                builder.Append(".");
                builder.Append(memberAccess.Name.Text);
                break;
            case EnumValueExpressionSyntax enumVal:
                builder.Append(enumVal.Token.Text);
                break;
            case BinaryExpressionSyntax binary:
                FormatExpression(builder, binary.Left);
                builder.Append(" ");
                builder.Append(binary.OperatorToken.Text);
                builder.Append(" ");
                FormatExpression(builder, binary.Right);
                break;
            case PrefixUnaryExpressionSyntax unary:
                builder.Append(unary.OperatorToken.Text);
                FormatExpression(builder, unary.Operand);
                break;
            case ConditionalExpressionSyntax cond:
                FormatExpression(builder, cond.Condition);
                builder.Append(" ? ");
                FormatExpression(builder, cond.WhenTrue);
                builder.Append(" : ");
                FormatExpression(builder, cond.WhenFalse);
                break;
            case CallExpressionSyntax call:
                FormatExpression(builder, call.Expression);
                builder.Append("(");
            {
                bool first = true;
                foreach (var arg in call.Arguments)
                {
                    if (!first) builder.Append(", ");
                    FormatExpression(builder, arg);
                    first = false;
                }
            }

                builder.Append(")");
                break;
            case ResourceExpressionSyntax resource:
                builder.Append(resource.Keyword.Text);
                builder.Append("(");
                FormatExpression(builder, resource.Path);
                builder.Append(")");
                break;
            case ParenthesizedExpressionSyntax parens:
                builder.Append("(");
                FormatExpression(builder, parens.Expression);
                builder.Append(")");
                break;
            case StructExpressionSyntax structExpr:
                builder.Append(structExpr.TypeName.Text);
                builder.Append("(");
                if (structExpr.PositionalArgs != null)
                {
                    bool first = true;
                    foreach (var arg in structExpr.PositionalArgs)
                    {
                        if (!first) builder.Append(", ");
                        FormatExpression(builder, arg);
                        first = false;
                    }
                }
                else if (structExpr.NamedArgs != null)
                {
                    FormatExpression(builder, structExpr.NamedArgs);
                }

                builder.Append(")");
                break;
            case ObjectCreationExpressionSyntax objCreate:
                builder.Append("new ");
                builder.Append(objCreate.TypeName.Text);
                builder.Append(" { ");
            {
                bool first = true;
                foreach (var prop in objCreate.Properties)
                {
                    if (!first) builder.Append(", ");
                    builder.Append(prop.Name.Text);
                    builder.Append(": ");
                    FormatExpression(builder, prop.Value);
                    first = false;
                }
            }
                builder.Append(" }");
                break;
            case ObjectLiteralExpressionSyntax objLit:
                builder.Append("{ ");
            {
                bool first = true;
                foreach (var prop in objLit.Properties)
                {
                    if (!first) builder.Append(", ");
                    builder.Append(prop.Name.Text);
                    builder.Append(": ");
                    FormatExpression(builder, prop.Value);
                    first = false;
                }
            }
                builder.Append(" }");
                break;
            case ArrayLiteralExpressionSyntax arrLit:
                builder.Append(arrLit.TypeName.Text);
                builder.Append("[");
            {
                bool first = true;
                foreach (var elem in arrLit.Elements)
                {
                    if (!first) builder.Append(", ");
                    FormatExpression(builder, elem);
                    first = false;
                }
            }
                builder.Append("]");
                break;
            case DictionaryLiteralExpressionSyntax dictLit:
                builder.Append(dictLit.TypeName.Text);
                builder.Append("[");
                builder.Append(dictLit.KeyType.Text);
                builder.Append(", ");
                builder.Append(dictLit.ValueType.Text);
                builder.Append("]{ ");
            {
                bool first = true;
                foreach (var entry in dictLit.Entries)
                {
                    if (!first) builder.Append(", ");
                    foreach (var token in entry.DescendantTokens())
                        builder.Append(token.Text);
                    first = false;
                }
            }
                builder.Append(" }");
                break;
            case TemplateStringExpressionSyntax templateStr:
                // Reconstruct template string from parts
                builder.Append(templateStr.OpenToken.Text);
                foreach (var part in templateStr.Parts)
                {
                    foreach (var token in part.DescendantTokens())
                        builder.Append(token.Text);
                }

                builder.Append(templateStr.CloseQuoteToken.Text);
                break;
            default:
                // Fallback: emit original tokens
                foreach (var token in expr.DescendantTokens())
                    builder.Append(token.Text);
                break;
        }
    }

    private static void FormatFallback(FormattedBuilder builder, SyntaxNode node)
    {
        foreach (var token in node.DescendantTokens())
            builder.Append(token.Text);
    }

    // ── Comment preservation helpers ──

    /// <summary>
    /// Emits standalone comment lines from a node's first token leading trivia.
    /// Returns true if any comment was emitted.
    /// </summary>
    private static bool EmitLeadingComments(
        FormattedBuilder builder, SyntaxNode node, int depth, string indent,
        bool skipDocComments = false, bool blankLineBefore = false)
    {
        var firstToken = node.FirstToken();
        if (firstToken == null) return false;

        bool emitted = false;
        foreach (var trivia in firstToken.LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.SingleLineCommentTrivia)
            {
                if (!emitted && blankLineBefore)
                    builder.AppendLine();
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
                emitted = true;
            }
            else if (trivia.Kind == SyntaxKind.DocumentationCommentTrivia && !skipDocComments)
            {
                if (!emitted && blankLineBefore)
                    builder.AppendLine();
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
                emitted = true;
            }
        }

        return emitted;
    }

    /// <summary>
    /// Emits standalone comment lines from a token's leading trivia.
    /// </summary>
    private static void EmitTokenLeadingComments(
        FormattedBuilder builder, SyntaxToken token, int depth, string indent)
    {
        foreach (var trivia in token.LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.SingleLineCommentTrivia ||
                trivia.Kind == SyntaxKind.DocumentationCommentTrivia)
            {
                builder.AppendIndent(depth, indent);
                builder.Append(trivia.Text);
                builder.AppendLine();
            }
        }
    }

    /// <summary>
    /// Emits trailing inline comment from a node's last token trailing trivia.
    /// </summary>
    private static void EmitTrailingComment(FormattedBuilder builder, SyntaxNode node)
    {
        var lastToken = node.LastToken();
        if (lastToken == null) return;
        EmitTrailingComment(builder, lastToken);
    }

    /// <summary>
    /// Emits trailing inline comment from a token's trailing trivia.
    /// </summary>
    private static void EmitTrailingComment(FormattedBuilder builder, SyntaxToken token)
    {
        foreach (var trivia in token.TrailingTrivia)
        {
            if (trivia.Kind == SyntaxKind.SingleLineCommentTrivia ||
                trivia.Kind == SyntaxKind.DocumentationCommentTrivia)
            {
                builder.Append(" ");
                builder.Append(trivia.Text);
            }
        }
    }

    /// <summary>
    /// Checks whether the original source has at least one blank line before a node.
    /// In the Lexer's trivia model, the previous token's trailing trivia already includes
    /// the line-ending newline, so a visual blank line appears as just ONE EndOfLineTrivia
    /// in the current token's leading trivia.
    /// </summary>
    private static bool HasBlankLineBefore(SyntaxNode node)
    {
        var firstToken = node.FirstToken();
        if (firstToken == null) return false;

        foreach (var trivia in firstToken.LeadingTrivia)
        {
            if (trivia.Kind == SyntaxKind.EndOfLineTrivia)
                return true;
        }

        return false;
    }

    // ── Helper class ──

    /// <summary>
    /// Returns true if the node is a value-assignment member per §8.1.3.
    /// </summary>
    private static bool IsValueAssignment(SyntaxNode node) => node is
        PropertyAssignmentSyntax or MappingAssignmentSyntax or
        EventSubscriptionSyntax or ParameterDeclarationSyntax or
        EventDeclarationSyntax or TemplateParamAssignmentSyntax;

    /// <summary>
    /// Returns the Comma token from a value-assignment node, or null if none.
    /// </summary>
    private static SyntaxToken? GetCommaToken(SyntaxNode node) => node switch
    {
        PropertyAssignmentSyntax p => p.Comma,
        MappingAssignmentSyntax m => m.Comma,
        EventSubscriptionSyntax e => e.Comma,
        TemplateParamAssignmentSyntax t => t.Comma,
        _ => null
    };

    /// <summary>
    /// Simple string builder wrapper with indentation support.
    /// </summary>
    private sealed class FormattedBuilder
    {
        private readonly System.Text.StringBuilder _sb = new();

        public void Append(string text) => _sb.Append(text);
        public void AppendLine() => _sb.Append('\n');

        public void AppendIndent(int depth, string indent)
        {
            for (int i = 0; i < depth; i++)
                _sb.Append(indent);
        }

        public override string ToString() => _sb.ToString();
    }
}
