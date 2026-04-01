using GUML.Shared.Syntax.Nodes;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Internal;

/// <summary>
/// Error-tolerant recursive-descent parser for the GUML language.
/// Always produces a <see cref="GumlDocumentSyntax"/> tree, even from malformed input.
/// Uses MissingToken insertion and SkippedTokens recovery.
/// </summary>
internal sealed class Parser
{
    private readonly List<SyntaxToken> _tokens;
    private int _position;
    private int _currentOffset;
    private readonly List<Diagnostic> _diagnostics = new List<Diagnostic>();
    private int _recursionDepth;
    private const int MaxRecursionDepth = 256;

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public Parser(List<SyntaxToken> tokens)
    {
        _tokens = tokens;
    }

    public Parser(string text)
    {
        var lexer = new Lexer(text);
        _tokens = lexer.LexAll();
        _diagnostics.AddRange(lexer.Diagnostics);
    }

    // ------------------------------------------------------------------
    // Entry
    // ------------------------------------------------------------------

    /// <summary>
    /// Parse the full document, returning a <see cref="GumlDocumentSyntax"/>.
    /// </summary>
    public GumlDocumentSyntax ParseDocument()
    {
        var imports = ParseImportDirectives();
        var root = ParseComponentDeclaration(isRoot: true);
        var eof = ExpectToken(SyntaxKind.EndOfFileToken);
        return new GumlDocumentSyntax(imports, root, eof);
    }

    // ------------------------------------------------------------------
    // Import directives
    // ------------------------------------------------------------------

    private SyntaxList<ImportDirectiveSyntax> ParseImportDirectives()
    {
        var list = new List<ImportDirectiveSyntax>();
        while (CurrentKind == SyntaxKind.ImportKeyword)
        {
            list.Add(ParseImportDirective());
        }

        return list.Count == 0 ? SyntaxList<ImportDirectiveSyntax>.Empty : new SyntaxList<ImportDirectiveSyntax>(list);
    }

    private ImportDirectiveSyntax ParseImportDirective()
    {
        var keyword = ExpectToken(SyntaxKind.ImportKeyword);
        var path = ExpectToken(SyntaxKind.StringLiteralToken);
        ImportAliasSyntax? alias = null;
        if (CurrentKind == SyntaxKind.AsKeyword)
        {
            var asKw = AdvanceToken();
            var name = ExpectToken(SyntaxKind.ComponentNameToken, SyntaxKind.IdentifierToken);
            alias = new ImportAliasSyntax(asKw, name);
        }

        return new ImportDirectiveSyntax(keyword, path, alias);
    }

    // ------------------------------------------------------------------
    // Component declarations
    // ------------------------------------------------------------------

    internal ComponentDeclarationSyntax ParseComponentDeclaration(bool isRoot)
    {
        if (++_recursionDepth > MaxRecursionDepth)
        {
            ReportError("GUML1001", "Maximum nesting depth exceeded");
            --_recursionDepth;
            // Return a minimal component with missing tokens
            return new ComponentDeclarationSyntax(
                null, null,
                SyntaxToken.Missing(SyntaxKind.ComponentNameToken),
                SyntaxToken.Missing(SyntaxKind.OpenBraceToken),
                SyntaxList<SyntaxNode>.Empty,
                SyntaxToken.Missing(SyntaxKind.CloseBraceToken));
        }

        // Optional documentation comment (already in leading trivia of next token)
        DocumentationCommentSyntax? docComment = TryParseDocComment();

        // Optional alias prefix: @alias:
        AliasPrefixSyntax? aliasPrefix = null;
        if (CurrentKind == SyntaxKind.AliasRefToken && PeekKind(1) == SyntaxKind.ColonToken)
        {
            var aliasRef = AdvanceToken();
            CheckPrefixedIdentifierNaming(aliasRef, "Alias reference");
            var colon = AdvanceToken();
            aliasPrefix = new AliasPrefixSyntax(aliasRef, colon);
        }

        var typeName = ExpectToken(SyntaxKind.ComponentNameToken);
        var openBrace = ExpectToken(SyntaxKind.OpenBraceToken);
        var members = ParseComponentBody(isRoot);
        var closeBrace = ExpectToken(SyntaxKind.CloseBraceToken);

        --_recursionDepth;
        return new ComponentDeclarationSyntax(docComment, aliasPrefix, typeName, openBrace, members, closeBrace);
    }

    private SyntaxList<SyntaxNode> ParseComponentBody(bool isRoot)
    {
        var members = new List<SyntaxNode>();
        while (CurrentKind != SyntaxKind.CloseBraceToken && CurrentKind != SyntaxKind.EndOfFileToken)
        {
            int beforePos = _position;
            var member = ParseComponentBodyElement(isRoot);
            if (member != null)
            {
                members.Add(member);

                // Enforce comma between adjacent comma-supporting elements
                if (IsCommaSupportedElement(member)
                    && !HasTrailingComma(member)
                    && NextStartsCommaSupportedElement())
                {
                    // Report at end of previous member, not at the next token
                    int endPos = member.Span.End;
                    ReportError("GUML1016", "Expected ',' between items",
                        new TextSpan(endPos, 0));
                }
            }

            // Safety: if we didn't advance at all, skip one bad token to avoid infinite loop
            if (_position == beforePos)
            {
                SkipBadToken();
            }
        }

        return members.Count == 0 ? SyntaxList<SyntaxNode>.Empty : new SyntaxList<SyntaxNode>(members);
    }

    internal SyntaxNode? ParseComponentBodyElement(bool isRoot)
    {
        switch (CurrentKind)
        {
            case SyntaxKind.ParamKeyword when isRoot:
                return ParseParameterDeclaration();
            case SyntaxKind.EventKeyword when isRoot:
                return ParseEventDeclaration();
            case SyntaxKind.EachKeyword:
                return ParseEachBlock();
            case SyntaxKind.ComponentNameToken:
                // ComponentName { ... } is a nested component declaration
                if (PeekKind(1) == SyntaxKind.OpenBraceToken)
                    return ParseComponentDeclaration(isRoot: false);
                // Otherwise it's a property/mapping assignment (value may be array, struct, etc.)
                return ParsePropertyOrMappingAssignment();
            case SyntaxKind.AliasRefToken:
                // @alias: ComponentName { ... } is a prefixed component
                if (PeekKind(1) == SyntaxKind.ColonToken)
                    return ParseComponentDeclaration(isRoot: false);
                // @alias as expression in property assignment? Shouldn't start a member.
                return ParsePropertyOrMappingAssignment();
            case SyntaxKind.EventRefToken:
                return ParseEventSubscription();
            case SyntaxKind.IdentifierToken:
                // Could be: property assignment, mapping assignment, or template param assignment
                if (PeekKind(1) == SyntaxKind.FatArrowToken)
                    return ParseTemplateParamAssignment();
                return ParsePropertyOrMappingAssignment();
            default:
                // Not a recognized member start - do nothing, caller will skip
                return null;
        }
    }

    // ------------------------------------------------------------------
    // Member-level parsing
    // ------------------------------------------------------------------

    private SyntaxNode ParsePropertyOrMappingAssignment()
    {
        var name = AdvanceToken();
        if (name.Kind == SyntaxKind.IdentifierToken)
            CheckIdentifierNaming(name, "Property name");
        if (SyntaxFacts.IsMappingOperator(CurrentKind))
        {
            var op = AdvanceToken();
            var value = ParseExpression();
            var comma = TryMatchToken(SyntaxKind.CommaToken);
            return new MappingAssignmentSyntax(name, op, value, comma);
        }
        else
        {
            var colon = ExpectToken(SyntaxKind.ColonToken);
            var value = ParseExpression();
            var comma = TryMatchToken(SyntaxKind.CommaToken);
            return new PropertyAssignmentSyntax(name, colon, value, comma);
        }
    }

    private EventSubscriptionSyntax ParseEventSubscription()
    {
        var eventRef = ExpectToken(SyntaxKind.EventRefToken);
        CheckPrefixedIdentifierNaming(eventRef, "Event reference");
        var colon = ExpectToken(SyntaxKind.ColonToken);
        var handler = ParseExpression();
        var comma = TryMatchToken(SyntaxKind.CommaToken);
        return new EventSubscriptionSyntax(eventRef, colon, handler, comma);
    }

    private TemplateParamAssignmentSyntax ParseTemplateParamAssignment()
    {
        var name = ExpectToken(SyntaxKind.IdentifierToken);
        CheckIdentifierNaming(name, "Template parameter name");
        var fatArrow = ExpectToken(SyntaxKind.FatArrowToken);
        var component = ParseComponentDeclaration(isRoot: false);
        var comma = TryMatchToken(SyntaxKind.CommaToken);
        return new TemplateParamAssignmentSyntax(name, fatArrow, component, comma);
    }

    private ParameterDeclarationSyntax ParseParameterDeclaration()
    {
        var docComment = TryParseDocComment();
        var paramKw = ExpectToken(SyntaxKind.ParamKeyword);
        var typeName = ParseTypeName();
        var name = ExpectToken(SyntaxKind.IdentifierToken);
        CheckIdentifierNaming(name, "Parameter name");

        SyntaxToken? defaultOp = null;
        ExpressionSyntax? defaultValue = null;
        if (CurrentKind == SyntaxKind.ColonToken || CurrentKind == SyntaxKind.MapToPropertyToken)
        {
            defaultOp = AdvanceToken();
            defaultValue = ParseExpression();
        }

        return new ParameterDeclarationSyntax(docComment, paramKw, typeName, name, defaultOp, defaultValue);
    }

    private EventDeclarationSyntax ParseEventDeclaration()
    {
        var docComment = TryParseDocComment();
        var eventKw = ExpectToken(SyntaxKind.EventKeyword);
        var name = ExpectToken(SyntaxKind.IdentifierToken);
        CheckIdentifierNaming(name, "Event name");

        SyntaxToken? openParen = null;
        SyntaxList<EventArgumentSyntax>? args = null;
        SyntaxToken? closeParen = null;

        if (CurrentKind == SyntaxKind.OpenParenToken)
        {
            openParen = AdvanceToken();
            var argList = new List<EventArgumentSyntax>();
            while (CurrentKind != SyntaxKind.CloseParenToken && CurrentKind != SyntaxKind.EndOfFileToken)
            {
                var argTypeName = ParseTypeName();
                SyntaxToken? argName = null;
                if (CurrentKind == SyntaxKind.IdentifierToken)
                    argName = AdvanceToken();
                var comma = TryMatchToken(SyntaxKind.CommaToken);
                argList.Add(new EventArgumentSyntax(argTypeName, argName, comma));
                if (comma == null
                    && CurrentKind != SyntaxKind.CloseParenToken
                    && CurrentKind != SyntaxKind.EndOfFileToken)
                {
                    ReportError("GUML1016", "Expected ',' between event arguments");
                }
            }

            args = argList.Count > 0
                ? new SyntaxList<EventArgumentSyntax>(argList)
                : SyntaxList<EventArgumentSyntax>.Empty;
            closeParen = ExpectToken(SyntaxKind.CloseParenToken);
        }

        return new EventDeclarationSyntax(docComment, eventKw, name, openParen, args, closeParen);
    }

    /// <summary>
    /// Parses a type name that may include generic type arguments (e.g. <c>List&lt;int&gt;</c>).
    /// Returns a single <see cref="SyntaxToken"/> whose Text is the full type string.
    /// </summary>
    private SyntaxToken ParseTypeName()
    {
        var baseToken = ExpectToken(SyntaxKind.IdentifierToken, SyntaxKind.ComponentNameToken);
        if (CurrentKind != SyntaxKind.LessThanToken)
            return baseToken;

        // Consume generic arguments: < Type1, Type2, ... >
        // Track all consumed tokens to compute correct trailing trivia
        var sb = new StringBuilder(baseToken.Text);
        sb.Append(CurrentToken.LeadingTrivia);
        sb.Append('<');
        AdvanceToken(); // consume <

        int depth = 1;
        while (depth > 0 && CurrentKind != SyntaxKind.EndOfFileToken)
        {
            var tok = CurrentToken;
            sb.Append(tok.LeadingTrivia);
            if (CurrentKind == SyntaxKind.LessThanToken)
            {
                depth++;
                sb.Append('<');
            }
            else if (CurrentKind == SyntaxKind.GreaterThanToken)
            {
                depth--;
                sb.Append('>');
            }
            else
            {
                sb.Append(tok.Text);
            }
            if (depth > 0)
                sb.Append(tok.TrailingTrivia);
            AdvanceToken();
        }

        // Build a synthetic token preserving original leading trivia, and the last token's trailing trivia
        return new SyntaxToken(
            baseToken.Kind,
            sb.ToString(),
            baseToken.LeadingTrivia,
            // Trailing trivia comes from the last consumed token (the closing >)
            _position > 0 ? _tokens[_position - 1].TrailingTrivia : SyntaxTriviaList.Empty);
    }

    private EachBlockSyntax ParseEachBlock()
    {
        var eachKw = ExpectToken(SyntaxKind.EachKeyword);

        // Optional each params: (prop: val, ...)
        EachParamsSyntax? eachParams = null;
        if (CurrentKind == SyntaxKind.OpenParenToken)
        {
            eachParams = ParseEachParams();
        }

        // Data source expression
        var dataSource = ParseExpression();

        SyntaxToken? openBrace = null, openPipe = null, indexName = null, comma = null;
        SyntaxToken? valueName = null, closePipe = null, closeBrace = null;
        SyntaxList<SyntaxNode>? body = null;
        SyntaxToken? fatArrow = null, projectionName = null;

        if (CurrentKind == SyntaxKind.OpenBraceToken)
        {
            // Block form: { |idx, val| body }
            openBrace = AdvanceToken();
            openPipe = ExpectToken(SyntaxKind.PipeToken);
            indexName = ExpectToken(SyntaxKind.IdentifierToken);
            comma = ExpectToken(SyntaxKind.CommaToken);
            valueName = ExpectToken(SyntaxKind.IdentifierToken);
            closePipe = ExpectToken(SyntaxKind.PipeToken);
            body = ParseComponentBody(isRoot: false);
            closeBrace = ExpectToken(SyntaxKind.CloseBraceToken);
        }
        else if (CurrentKind == SyntaxKind.FatArrowToken)
        {
            // Projection form: => paramName
            fatArrow = AdvanceToken();
            projectionName = ExpectToken(SyntaxKind.IdentifierToken);
            CheckIdentifierNaming(projectionName, "Projection name");
        }
        else
        {
            ReportError("GUML1002", "Expected '{' or '=>' after each data source");
        }

        return new EachBlockSyntax(
            eachKw, eachParams, dataSource,
            openBrace, openPipe, indexName, comma, valueName, closePipe, body, closeBrace,
            fatArrow, projectionName);
    }

    private EachParamsSyntax ParseEachParams()
    {
        var open = ExpectToken(SyntaxKind.OpenParenToken);
        var objectLiteral = ParseObjectLiteralExpression();
        var close = ExpectToken(SyntaxKind.CloseParenToken);
        return new EachParamsSyntax(open, objectLiteral, close);
    }

    // ------------------------------------------------------------------
    // Expression parsing (precedence climbing)
    // ------------------------------------------------------------------

    internal ExpressionSyntax ParseExpression()
    {
        return ParseConditionalExpression();
    }

    private ExpressionSyntax ParseConditionalExpression()
    {
        var left = ParseBinaryExpression(0);
        if (CurrentKind == SyntaxKind.QuestionToken)
        {
            var question = AdvanceToken();
            var whenTrue = ParseExpression();
            var colon = ExpectToken(SyntaxKind.ColonToken);
            var whenFalse = ParseExpression();
            return new ConditionalExpressionSyntax(left, question, whenTrue, colon, whenFalse);
        }

        return left;
    }

    private ExpressionSyntax ParseBinaryExpression(int minPrecedence)
    {
        var left = ParsePrefixUnaryExpression();
        while (true)
        {
            int prec = SyntaxFacts.GetBinaryOperatorPrecedence(CurrentKind);
            if (prec <= minPrecedence)
                break;
            var op = AdvanceToken();
            var right = ParseBinaryExpression(prec);
            left = new BinaryExpressionSyntax(left, op, right);
        }

        return left;
    }

    private ExpressionSyntax ParsePrefixUnaryExpression()
    {
        if (SyntaxFacts.IsPrefixUnaryOperator(CurrentKind))
        {
            var op = AdvanceToken();
            var operand = ParsePrefixUnaryExpression();
            return new PrefixUnaryExpressionSyntax(op, operand);
        }

        return ParsePostfixExpression();
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
        var expr = ParsePrimaryExpression();
        // Postfix: member access (.name / .PascalCase) and call ((...))
        while (true)
        {
            if (CurrentKind == SyntaxKind.DotToken)
            {
                var dot = AdvanceToken();
                var name = ExpectToken(SyntaxKind.IdentifierToken);
                CheckMemberAccessNaming(name);
                expr = new MemberAccessExpressionSyntax(expr, dot, name);
            }
            else if (CurrentKind == SyntaxKind.EnumValueToken)
            {
                // The Lexer produces ".PascalCase" as a single EnumValueToken.
                // In postfix position (after an expression), this is actually
                // member access, not an enum value. Split it into dot + name.
                var enumToken = AdvanceToken();
                string text = enumToken.Text; // e.g. ".Name"
                var dotToken = new SyntaxToken(SyntaxKind.DotToken, ".",
                    enumToken.LeadingTrivia, SyntaxTriviaList.Empty);
                var nameToken = new SyntaxToken(SyntaxKind.IdentifierToken, text.Substring(1),
                    SyntaxTriviaList.Empty, enumToken.TrailingTrivia);

                // Sync the token list: replace the single EnumValueToken with DotToken + IdentifierToken
                // so that ParseResult.Tokens stays consistent with the syntax tree.
                _tokens[_position - 1] = dotToken;
                _tokens.Insert(_position, nameToken);
                _position++;

                CheckMemberAccessNaming(nameToken);
                expr = new MemberAccessExpressionSyntax(expr, dotToken, nameToken);
            }
            else if (CurrentKind == SyntaxKind.OpenParenToken)
            {
                var open = AdvanceToken();
                var args = ParseArgumentListInner();
                var close = ExpectToken(SyntaxKind.CloseParenToken);
                expr = new CallExpressionSyntax(expr, open, args, close);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        switch (CurrentKind)
        {
            // Literals
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.IntegerLiteralToken:
            case SyntaxKind.FloatLiteralToken:
            case SyntaxKind.TrueLiteralToken:
            case SyntaxKind.FalseLiteralToken:
            case SyntaxKind.NullLiteralToken:
                return new LiteralExpressionSyntax(AdvanceToken());

            // Template string
            case SyntaxKind.TemplateStringLiteralToken:
                return ParseTemplateStringExpression();

            // Enum value: .PascalCase
            case SyntaxKind.EnumValueToken:
                return new EnumValueExpressionSyntax(AdvanceToken());

            // Global ref: $name
            case SyntaxKind.GlobalRefToken:
                return new ReferenceExpressionSyntax(AdvanceToken());

            // Alias ref: @name
            case SyntaxKind.AliasRefToken:
                return new ReferenceExpressionSyntax(AdvanceToken());

            // Identifier
            case SyntaxKind.IdentifierToken:
                return ParseIdentifierExpression();

            // Component name (PascalCase): struct constructor, array literal, or dictionary literal
            case SyntaxKind.ComponentNameToken:
                return ParseComponentNameExpression();

            // Parenthesized expression
            case SyntaxKind.OpenParenToken:
                return ParseParenthesizedExpression();

            // new keyword
            case SyntaxKind.NewKeyword:
                return ParseNewExpression();

            // Resource keywords: image, font, audio, video
            case SyntaxKind.ImageKeyword:
            case SyntaxKind.FontKeyword:
            case SyntaxKind.AudioKeyword:
            case SyntaxKind.VideoKeyword:
                return ParseResourceExpression();

            // Object literal: { key: val, ... }
            case SyntaxKind.OpenBraceToken:
                return ParseObjectLiteralExpression();

            // Incomplete enum value: bare '.' without a following PascalCase identifier
            case SyntaxKind.DotToken:
            {
                AdvanceToken(); // consume the lone '.'
                ReportError("GUML1003", "Expected enum value after '.'");
                return new LiteralExpressionSyntax(SyntaxToken.Missing(SyntaxKind.EnumValueToken));
            }

            default:
                ReportError("GUML1003", $"Expected expression, got '{CurrentToken.Text}'");
                return new LiteralExpressionSyntax(SyntaxToken.Missing(SyntaxKind.StringLiteralToken));
        }
    }

    // Identifier could be: plain reference, or start of struct constructor Type(...)
    private ExpressionSyntax ParseIdentifierExpression()
    {
        // Plain identifier — just a reference
        return new ReferenceExpressionSyntax(AdvanceToken());
    }

    // ComponentName could be:
    //   - ComponentName ( ... )  => struct expression
    //   - ComponentName [ ... ]  => array or typed dictionary literal
    //   - ComponentName          => reference (uncommon but possible in expressions)
    private ExpressionSyntax ParseComponentNameExpression()
    {
        if (PeekKind(1) == SyntaxKind.OpenParenToken)
        {
            return ParseStructExpression();
        }

        if (PeekKind(1) == SyntaxKind.OpenBracketToken)
        {
            return ParseArrayOrDictionaryLiteral();
        }

        // Bare component name as reference
        return new ReferenceExpressionSyntax(AdvanceToken());
    }

    private StructExpressionSyntax ParseStructExpression()
    {
        var typeName = AdvanceToken(); // ComponentName or Identifier
        var open = ExpectToken(SyntaxKind.OpenParenToken);

        // Disambiguate: named initializer starts with { or positional args
        SeparatedSyntaxList<ExpressionSyntax>? positionalArgs = null;
        ObjectLiteralExpressionSyntax? namedArgs = null;

        if (CurrentKind == SyntaxKind.OpenBraceToken)
        {
            namedArgs = ParseObjectLiteralExpression();
        }
        else if (CurrentKind != SyntaxKind.CloseParenToken)
        {
            positionalArgs = ParseArgumentListInner();
        }
        else
        {
            positionalArgs = SeparatedSyntaxList<ExpressionSyntax>.Empty;
        }

        var close = ExpectToken(SyntaxKind.CloseParenToken);
        return new StructExpressionSyntax(typeName, open, positionalArgs, namedArgs, close);
    }

    private ExpressionSyntax ParseArrayOrDictionaryLiteral()
    {
        var typeName = AdvanceToken(); // ComponentName or Identifier
        var openBracket = ExpectToken(SyntaxKind.OpenBracketToken);

        // Peek ahead: if the pattern is Type[ type, type ]{ ... } it's a dictionary
        // Otherwise it's an array: Type[ expr, expr, ... ]
        // Detect dictionary: first token after [ is an identifier/component, followed by comma,
        // then another identifier/component, then ]
        if (IsDictionaryTypeArgs())
        {
            return ParseDictionaryLiteralRest(typeName, openBracket);
        }
        else
        {
            return ParseArrayLiteralRest(typeName, openBracket);
        }
    }

    private bool IsDictionaryTypeArgs()
    {
        // Lookahead heuristic: check if we see TypeName , TypeName ]
        bool result = false;
        if (CurrentKind == SyntaxKind.IdentifierToken || CurrentKind == SyntaxKind.ComponentNameToken)
        {
            int p2 = _position + 1;
            if (p2 < _tokens.Count && _tokens[p2].Kind == SyntaxKind.CommaToken)
            {
                int p3 = p2 + 1;
                if (p3 < _tokens.Count && (_tokens[p3].Kind == SyntaxKind.IdentifierToken ||
                                           _tokens[p3].Kind == SyntaxKind.ComponentNameToken))
                {
                    int p4 = p3 + 1;
                    if (p4 < _tokens.Count && _tokens[p4].Kind == SyntaxKind.CloseBracketToken)
                    {
                        result = true;
                    }
                }
            }
        }

        return result;
    }

    private DictionaryLiteralExpressionSyntax ParseDictionaryLiteralRest(SyntaxToken typeName, SyntaxToken openBracket)
    {
        var keyType = ExpectToken(SyntaxKind.IdentifierToken, SyntaxKind.ComponentNameToken);
        var typeComma = ExpectToken(SyntaxKind.CommaToken);
        var valueType = ExpectToken(SyntaxKind.IdentifierToken, SyntaxKind.ComponentNameToken);
        var closeBracket = ExpectToken(SyntaxKind.CloseBracketToken);
        var openBrace = ExpectToken(SyntaxKind.OpenBraceToken);
        var entries = ParseDictionaryEntries();
        var closeBrace = ExpectToken(SyntaxKind.CloseBraceToken);
        return new DictionaryLiteralExpressionSyntax(
            typeName, openBracket, keyType, typeComma, valueType, closeBracket,
            openBrace, entries, closeBrace);
    }

    private ArrayLiteralExpressionSyntax ParseArrayLiteralRest(SyntaxToken typeName, SyntaxToken openBracket)
    {
        var elements = SeparatedSyntaxList<ExpressionSyntax>.Empty;
        if (CurrentKind != SyntaxKind.CloseBracketToken)
        {
            elements = ParseArgumentListInner();
        }

        var closeBracket = ExpectToken(SyntaxKind.CloseBracketToken);
        return new ArrayLiteralExpressionSyntax(typeName, openBracket, elements, closeBracket);
    }

    private SyntaxList<DictionaryEntrySyntax> ParseDictionaryEntries()
    {
        var entries = new List<DictionaryEntrySyntax>();
        while (CurrentKind != SyntaxKind.CloseBraceToken && CurrentKind != SyntaxKind.EndOfFileToken)
        {
            int before = _position;
            var key = ParseExpression();
            var colon = ExpectToken(SyntaxKind.ColonToken);
            var value = ParseExpression();
            var comma = TryMatchToken(SyntaxKind.CommaToken);
            entries.Add(new DictionaryEntrySyntax(key, colon, value, comma));
            if (comma == null
                && CurrentKind != SyntaxKind.CloseBraceToken
                && CurrentKind != SyntaxKind.EndOfFileToken)
            {
                ReportError("GUML1016", "Expected ',' between items");
            }
            if (_position == before)
            {
                SkipBadToken();
            }
        }

        return entries.Count > 0
            ? new SyntaxList<DictionaryEntrySyntax>(entries)
            : SyntaxList<DictionaryEntrySyntax>.Empty;
    }

    private ParenthesizedExpressionSyntax ParseParenthesizedExpression()
    {
        var open = ExpectToken(SyntaxKind.OpenParenToken);
        var expr = ParseExpression();
        var close = ExpectToken(SyntaxKind.CloseParenToken);
        return new ParenthesizedExpressionSyntax(open, expr, close);
    }

    private ObjectCreationExpressionSyntax ParseNewExpression()
    {
        var newKw = ExpectToken(SyntaxKind.NewKeyword);
        var typeName = ExpectToken(SyntaxKind.ComponentNameToken);
        var openBrace = ExpectToken(SyntaxKind.OpenBraceToken);
        var props = ParsePropertyAssignmentList();
        var closeBrace = ExpectToken(SyntaxKind.CloseBraceToken);
        return new ObjectCreationExpressionSyntax(newKw, typeName, openBrace, props, closeBrace);
    }

    private ResourceExpressionSyntax ParseResourceExpression()
    {
        var keyword = AdvanceToken(); // image/font/audio/video
        var open = ExpectToken(SyntaxKind.OpenParenToken);
        var path = ParseExpression();
        var close = ExpectToken(SyntaxKind.CloseParenToken);
        return new ResourceExpressionSyntax(keyword, open, path, close);
    }

    private ObjectLiteralExpressionSyntax ParseObjectLiteralExpression()
    {
        var open = ExpectToken(SyntaxKind.OpenBraceToken);
        var props = ParsePropertyAssignmentList();
        var close = ExpectToken(SyntaxKind.CloseBraceToken);
        return new ObjectLiteralExpressionSyntax(open, props, close);
    }

    private TemplateStringExpressionSyntax ParseTemplateStringExpression()
    {
        var rawToken = AdvanceToken();
        string text = rawToken.Text; // e.g. $"Hello {$controller.name}"

        // Build the $" open token with the original token's leading trivia
        var openToken = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, "$\"",
            rawToken.LeadingTrivia, SyntaxTriviaList.Empty);

        // If the token is too short or malformed, return a minimal node
        if (text.Length < 3 || !text.StartsWith("$\""))
        {
            var closeEmpty = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, "",
                SyntaxTriviaList.Empty, rawToken.TrailingTrivia);
            return new TemplateStringExpressionSyntax(openToken, SyntaxList<SyntaxNode>.Empty, closeEmpty);
        }

        // Determine if the string is terminated with a quote
        bool terminated = text.EndsWith("\"") && text.Length > 2;
        int contentEnd = terminated ? text.Length - 1 : text.Length;

        var parts = new List<SyntaxNode>();
        int pos = 2; // skip $"

        while (pos < contentEnd)
        {
            char c = text[pos];
            if (c == '{' && pos + 1 < contentEnd && text[pos + 1] == '{')
            {
                // Escaped brace {{ — collect as literal text (including any following }} escapes)
                int textStart = pos;
                while (pos < contentEnd)
                {
                    if (text[pos] == '{' && pos + 1 < contentEnd && text[pos + 1] == '{')
                        pos += 2;
                    else if (text[pos] == '}' && pos + 1 < contentEnd && text[pos + 1] == '}')
                        pos += 2;
                    else if (text[pos] == '{')
                        break; // unescaped { starts real interpolation
                    else if (text[pos] == '\\' && pos + 1 < contentEnd)
                        pos += 2;
                    else
                        pos++;
                }

                string segment = text.Substring(textStart, pos - textStart);
                var txtToken = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, segment,
                    SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
                parts.Add(new TemplateStringTextSyntax(txtToken));
            }
            else if (c == '{')
            {
                // Find matching close brace (skip over nested string literals)
                int braceStart = pos;
                int depth = 1;
                int exprStart = pos + 1;
                pos++;
                while (pos < contentEnd && depth > 0)
                {
                    char ic = text[pos];
                    if (ic == '"')
                    {
                        // Skip over string literal inside interpolation
                        pos++;
                        while (pos < contentEnd && text[pos] != '"')
                        {
                            if (text[pos] == '\\' && pos + 1 < contentEnd)
                                pos += 2;
                            else
                                pos++;
                        }

                        if (pos < contentEnd) pos++; // skip closing "
                    }
                    else
                    {
                        if (ic == '{') depth++;
                        else if (ic == '}') depth--;
                        if (depth > 0) pos++;
                    }
                }

                if (depth == 0)
                {
                    // pos is at the matching '}'
                    string exprText = text.Substring(exprStart, pos - exprStart);
                    var openBrace = new SyntaxToken(SyntaxKind.OpenBraceToken, "{",
                        SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);

                    ExpressionSyntax expr;
                    if (string.IsNullOrWhiteSpace(exprText))
                    {
                        // Empty interpolation: produce a missing identifier
                        _diagnostics.Add(new Diagnostic("GUML1000",
                            "Empty interpolation expression",
                            DiagnosticSeverity.Error, new TextSpan(_currentOffset, 0)));
                        expr = new ReferenceExpressionSyntax(SyntaxToken.Missing(SyntaxKind.IdentifierToken));
                    }
                    else
                    {
                        // Sub-parse the expression
                        var subParser = new Parser(exprText);
                        expr = subParser.ParseExpression();

                        // Adjust sub-parser diagnostic positions to the main document
                        int exprDocOffset = rawToken.Span.Start + exprStart;
                        foreach (var diag in subParser.Diagnostics)
                        {
                            _diagnostics.Add(new Diagnostic(diag.Id, diag.Message, diag.Severity,
                                new TextSpan(diag.Span.Start + exprDocOffset, diag.Span.Length)));
                        }
                    }

                    var closeBrace = new SyntaxToken(SyntaxKind.CloseBraceToken, "}",
                        SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);

                    parts.Add(new TemplateStringInterpolationSyntax(openBrace, expr, closeBrace));
                    pos++; // skip past '}'
                }
                else
                {
                    // Unmatched brace — treat remaining text as literal
                    string remaining = text.Substring(braceStart, contentEnd - braceStart);
                    var txtToken = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, remaining,
                        SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
                    parts.Add(new TemplateStringTextSyntax(txtToken));
                    pos = contentEnd;
                }
            }
            else if (c == '\\' && pos + 1 < contentEnd)
            {
                // Escape sequence — collect as part of text
                int textStart = pos;
                pos += 2; // skip escape
                // Continue collecting text until next '{' or end
                while (pos < contentEnd && text[pos] != '{')
                {
                    if (text[pos] == '\\' && pos + 1 < contentEnd)
                        pos += 2;
                    else
                        pos++;
                }

                string segment = text.Substring(textStart, pos - textStart);
                var txtToken = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, segment,
                    SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
                parts.Add(new TemplateStringTextSyntax(txtToken));
            }
            else
            {
                // Plain text segment
                int textStart = pos;
                while (pos < contentEnd && text[pos] != '{')
                {
                    if (text[pos] == '\\' && pos + 1 < contentEnd)
                        pos += 2;
                    else
                        pos++;
                }

                string segment = text.Substring(textStart, pos - textStart);
                var txtToken = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, segment,
                    SyntaxTriviaList.Empty, SyntaxTriviaList.Empty);
                parts.Add(new TemplateStringTextSyntax(txtToken));
            }
        }

        // Build the closing " token with the original token's trailing trivia
        string closeText = terminated ? "\"" : "";
        var closeQuote = new SyntaxToken(SyntaxKind.TemplateStringLiteralToken, closeText,
            SyntaxTriviaList.Empty, rawToken.TrailingTrivia);

        return new TemplateStringExpressionSyntax(openToken,
            parts.Count > 0 ? new SyntaxList<SyntaxNode>(parts.ToArray()) : SyntaxList<SyntaxNode>.Empty,
            closeQuote);
    }

    // ------------------------------------------------------------------
    // Shared helpers for lists
    // ------------------------------------------------------------------

    private SeparatedSyntaxList<ExpressionSyntax> ParseArgumentListInner()
    {
        var args = new List<ExpressionSyntax>();
        var separators = new List<SyntaxToken>();
        while (CurrentKind != SyntaxKind.CloseParenToken
               && CurrentKind != SyntaxKind.CloseBracketToken
               && CurrentKind != SyntaxKind.EndOfFileToken)
        {
            int before = _position;
            args.Add(ParseExpression());
            // Consume optional comma separator
            if (CurrentKind == SyntaxKind.CommaToken)
                separators.Add(AdvanceToken());
            else if (CurrentKind != SyntaxKind.CloseParenToken
                     && CurrentKind != SyntaxKind.CloseBracketToken
                     && CurrentKind != SyntaxKind.EndOfFileToken)
            {
                ReportError("GUML1016", "Expected ',' between arguments");
            }
            if (_position == before)
            {
                SkipBadToken();
            }
        }

        return args.Count > 0
            ? new SeparatedSyntaxList<ExpressionSyntax>(args, separators)
            : SeparatedSyntaxList<ExpressionSyntax>.Empty;
    }

    private SyntaxList<PropertyAssignmentSyntax> ParsePropertyAssignmentList()
    {
        var props = new List<PropertyAssignmentSyntax>();
        while (CurrentKind != SyntaxKind.CloseBraceToken && CurrentKind != SyntaxKind.EndOfFileToken)
        {
            int before = _position;
            var name = ExpectToken(SyntaxKind.IdentifierToken);
            var colon = ExpectToken(SyntaxKind.ColonToken);
            var value = ParseExpression();
            var comma = TryMatchToken(SyntaxKind.CommaToken);
            props.Add(new PropertyAssignmentSyntax(name, colon, value, comma));
            if (comma == null
                && CurrentKind != SyntaxKind.CloseBraceToken
                && CurrentKind != SyntaxKind.EndOfFileToken)
            {
                ReportError("GUML1016", "Expected ',' between items");
            }
            if (_position == before)
            {
                SkipBadToken();
            }
        }

        return props.Count > 0
            ? new SyntaxList<PropertyAssignmentSyntax>(props)
            : SyntaxList<PropertyAssignmentSyntax>.Empty;
    }

    // ------------------------------------------------------------------
    // Doc comment helper
    // ------------------------------------------------------------------

    private DocumentationCommentSyntax? TryParseDocComment()
    {
        // Doc comments are attached as leading trivia on the next token.
        // We extract them into a DocumentationCommentSyntax node and strip them
        // from the token so they are not double-emitted during round-trip.
        var token = CurrentToken;
        var triviaList = token.LeadingTrivia;

        // Quick check: any doc comments at all?
        int lastDocIdx = -1;
        for (int i = 0; i < triviaList.Count; i++)
            if (triviaList[i].Kind == SyntaxKind.DocumentationCommentTrivia)
                lastDocIdx = i;
        if (lastDocIdx < 0) return null;

        // Boundary: include the trailing EOL after the last doc comment (if present).
        int boundary = lastDocIdx + 1;
        if (boundary < triviaList.Count && triviaList[boundary].Kind == SyntaxKind.EndOfLineTrivia)
            boundary++;

        // Build doc tokens from trivia [0..boundary), leave [boundary..] on the original token.
        var docTokens = new List<SyntaxToken>();
        var pendingLeading = new List<SyntaxTrivia>();

        for (int i = 0; i < boundary; i++)
        {
            var trivia = triviaList[i];
            if (trivia.Kind == SyntaxKind.DocumentationCommentTrivia)
            {
                // Collect trailing trivia until the next doc comment or boundary
                var trailing = new List<SyntaxTrivia>();
                int j = i + 1;
                while (j < boundary && triviaList[j].Kind != SyntaxKind.DocumentationCommentTrivia)
                {
                    trailing.Add(triviaList[j]);
                    j++;
                }

                docTokens.Add(new SyntaxToken(
                    SyntaxKind.DocumentationCommentTrivia, trivia.Text,
                    pendingLeading.Count > 0 ? new SyntaxTriviaList(pendingLeading.ToArray()) : SyntaxTriviaList.Empty,
                    trailing.Count > 0 ? new SyntaxTriviaList(trailing.ToArray()) : SyntaxTriviaList.Empty));
                pendingLeading.Clear();
                i = j - 1; // skip trailing trivia (loop will i++)
            }
            else
            {
                pendingLeading.Add(trivia);
            }
        }

        // Remaining trivia stays on the original token
        var remaining = new List<SyntaxTrivia>();
        // Any pending leading that wasn't consumed (shouldn't happen, but be safe)
        remaining.AddRange(pendingLeading);
        for (int i = boundary; i < triviaList.Count; i++)
            remaining.Add(triviaList[i]);

        var newLeading = remaining.Count > 0
            ? new SyntaxTriviaList(remaining.ToArray())
            : SyntaxTriviaList.Empty;
        _tokens[_position] = new SyntaxToken(token.Kind, token.Text, newLeading, token.TrailingTrivia, token.IsMissing);

        return new DocumentationCommentSyntax(docTokens.ToArray());
    }

    // ------------------------------------------------------------------
    // Token consumption helpers
    // ------------------------------------------------------------------

    private SyntaxToken CurrentToken => _position < _tokens.Count ? _tokens[_position] : _tokens[_tokens.Count - 1];
    private SyntaxKind CurrentKind => CurrentToken.Kind;

    private SyntaxKind PeekKind(int offset)
    {
        int idx = _position + offset;
        return idx < _tokens.Count ? _tokens[idx].Kind : SyntaxKind.EndOfFileToken;
    }

    private SyntaxToken AdvanceToken()
    {
        var token = CurrentToken;
        if (_position < _tokens.Count)
        {
            _currentOffset += token.FullWidth;
            _position++;
        }

        return token;
    }

    /// <summary>
    /// Expect one of the given kinds. If the current token does not match, insert a missing token and report a diagnostic.
    /// </summary>
    private SyntaxToken ExpectToken(SyntaxKind expected)
    {
        if (CurrentKind == expected)
            return AdvanceToken();
        ReportError("GUML1000",
            $"Expected '{SyntaxFacts.GetText(expected) ?? expected.ToString()}', got '{CurrentToken.Text}'");
        return SyntaxToken.Missing(expected);
    }

    /// <summary>
    /// Expect either of two kinds. Returns the current token if it matches, else inserts missing for the first kind.
    /// </summary>
    private SyntaxToken ExpectToken(SyntaxKind expected1, SyntaxKind expected2)
    {
        if (CurrentKind == expected1 || CurrentKind == expected2)
            return AdvanceToken();
        ReportError("GUML1000",
            $"Expected '{SyntaxFacts.GetText(expected1) ?? expected1.ToString()}' or '{SyntaxFacts.GetText(expected2) ?? expected2.ToString()}', got '{CurrentToken.Text}'");
        return SyntaxToken.Missing(expected1);
    }

    /// <summary>
    /// If the current token matches, consume and return it. Otherwise return null.
    /// </summary>
    private SyntaxToken? TryMatchToken(SyntaxKind kind)
    {
        if (CurrentKind == kind)
            return AdvanceToken();
        return null;
    }

    // ------------------------------------------------------------------
    // Comma enforcement helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns true if the given node is a comma-supporting element
    /// (property/mapping assignment, event subscription, template param assignment).
    /// </summary>
    private static bool IsCommaSupportedElement(SyntaxNode node)
        => node is PropertyAssignmentSyntax
            or MappingAssignmentSyntax
            or EventSubscriptionSyntax
            or TemplateParamAssignmentSyntax;

    /// <summary>
    /// Returns true if the given comma-supporting element has a trailing comma token.
    /// </summary>
    private static bool HasTrailingComma(SyntaxNode node) => node switch
    {
        PropertyAssignmentSyntax p => p.Comma != null,
        MappingAssignmentSyntax m => m.Comma != null,
        EventSubscriptionSyntax e => e.Comma != null,
        TemplateParamAssignmentSyntax t => t.Comma != null,
        _ => true
    };

    /// <summary>
    /// Returns true if the current token could start another comma-separated element
    /// in a component body. Structural elements (child components, param/event/each) are excluded.
    /// </summary>
    private bool NextStartsCommaSupportedElement()
    {
        switch (CurrentKind)
        {
            case SyntaxKind.IdentifierToken:
            case SyntaxKind.EventRefToken:
                return true;
            case SyntaxKind.ComponentNameToken:
                return PeekKind(1) != SyntaxKind.OpenBraceToken;
            case SyntaxKind.AliasRefToken:
                return PeekKind(1) != SyntaxKind.ColonToken;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------
    // Error recovery
    // ------------------------------------------------------------------

    private void SkipBadToken()
    {
        var token = CurrentToken;
        ReportError("GUML1004", $"Unexpected token '{token.Text}'");
        if (_position < _tokens.Count)
        {
            _currentOffset += token.FullWidth;
            _position++;
        }
    }

    private void ReportError(string id, string message)
    {
        var token = CurrentToken;
        var span = new TextSpan(_currentOffset + token.LeadingTrivia.FullWidth, token.Width);
        _diagnostics.Add(new Diagnostic(id, message, DiagnosticSeverity.Error, span));
    }

    private void ReportError(string id, string message, TextSpan span)
    {
        _diagnostics.Add(new Diagnostic(id, message, DiagnosticSeverity.Error, span));
    }

    private void ReportWarning(string id, string message, TextSpan span)
    {
        _diagnostics.Add(new Diagnostic(id, message, DiagnosticSeverity.Warning, span));
    }

    // ------------------------------------------------------------------
    // Naming convention checks (§3.6.1.1)
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns true if the name is snake_case (all lowercase letters, digits, and underscores).
    /// Single-character lowercase names are considered valid snake_case.
    /// </summary>
    private static bool IsSnakeCase(string name)
    {
        if (name.Length == 0) return true;
        foreach (char c in name)
        {
            if (c != '_' && !char.IsDigit(c) && !(char.IsLetter(c) && char.IsLower(c)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Emit GUML3001 warning if a member-access name is not snake_case (§3.6.1.1).
    /// </summary>
    private void CheckMemberAccessNaming(SyntaxToken nameToken)
    {
        if (!IsSnakeCase(nameToken.Text))
        {
            ReportWarning("GUML3001",
                $"Member access name '{nameToken.Text}' should use snake_case per §3.6.1.1 naming conventions",
                nameToken.FullSpan);
        }
    }

    /// <summary>
    /// Emit GUML3002 warning if a property/event/param/alias identifier is not snake_case (§3.6.1.1).
    /// </summary>
    private void CheckIdentifierNaming(SyntaxToken nameToken, string category)
    {
        if (!IsSnakeCase(nameToken.Text))
        {
            ReportWarning("GUML3002",
                $"{category} '{nameToken.Text}' should use snake_case per §3.6.1.1 naming conventions",
                nameToken.FullSpan);
        }
    }

    /// <summary>
    /// Emit GUML3002 warning if a prefixed identifier (e.g. #eventRef, @aliasRef)
    /// is not snake_case after stripping the leading sigil (§3.6.1.1).
    /// </summary>
    private void CheckPrefixedIdentifierNaming(SyntaxToken token, string category)
    {
        if (token.Text.Length > 1 && !IsSnakeCase(token.Text.Substring(1)))
        {
            ReportWarning("GUML3002",
                $"{category} '{token.Text}' should use snake_case per §3.6.1.1 naming conventions",
                token.FullSpan);
        }
    }
}
