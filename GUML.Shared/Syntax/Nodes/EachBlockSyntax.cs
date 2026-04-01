using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// An each block in block form: <c>each source { |idx, val| ... }</c>
/// or projection form: <c>each source => paramName</c>.
/// Optionally with params: <c>each (options) source { ... }</c>.
/// </summary>
public sealed class EachBlockSyntax : SyntaxNode
{
    public SyntaxToken EachKeyword { get; }
    public EachParamsSyntax? Params { get; }
    public ExpressionSyntax DataSource { get; }

    // Block form: { |idx, val| body }
    public SyntaxToken? OpenBrace { get; }
    public SyntaxToken? OpenPipe { get; }
    public SyntaxToken? IndexName { get; }
    public SyntaxToken? Comma { get; }
    public SyntaxToken? ValueName { get; }
    public SyntaxToken? ClosePipe { get; }
    public SyntaxList<SyntaxNode>? Body { get; }
    public SyntaxToken? CloseBrace { get; }

    // Projection form: => paramName
    public SyntaxToken? FatArrow { get; }
    public SyntaxToken? ProjectionName { get; }

    public EachBlockSyntax(
        SyntaxToken eachKeyword,
        EachParamsSyntax? @params,
        ExpressionSyntax dataSource,
        SyntaxToken? openBrace,
        SyntaxToken? openPipe,
        SyntaxToken? indexName,
        SyntaxToken? comma,
        SyntaxToken? valueName,
        SyntaxToken? closePipe,
        SyntaxList<SyntaxNode>? body,
        SyntaxToken? closeBrace,
        SyntaxToken? fatArrow,
        SyntaxToken? projectionName)
        : base(SyntaxKind.EachBlock)
    {
        EachKeyword = eachKeyword;
        Params = @params;
        DataSource = dataSource;
        OpenBrace = openBrace;
        OpenPipe = openPipe;
        IndexName = indexName;
        Comma = comma;
        ValueName = valueName;
        ClosePipe = closePipe;
        Body = body;
        CloseBrace = closeBrace;
        FatArrow = fatArrow;
        ProjectionName = projectionName;
    }

    public override int FullWidth
    {
        get
        {
            int w = EachKeyword.FullWidth
                    + (Params?.FullWidth ?? 0)
                    + DataSource.FullWidth;

            // Block form
            w += OpenBrace?.FullWidth ?? 0;
            w += OpenPipe?.FullWidth ?? 0;
            w += IndexName?.FullWidth ?? 0;
            w += Comma?.FullWidth ?? 0;
            w += ValueName?.FullWidth ?? 0;
            w += ClosePipe?.FullWidth ?? 0;
            if (Body != null)
            {
                foreach (var node in Body)
                    w += node.FullWidth;
            }

            w += CloseBrace?.FullWidth ?? 0;

            // Projection form
            w += FatArrow?.FullWidth ?? 0;
            w += ProjectionName?.FullWidth ?? 0;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return EachKeyword;
        if (Params != null) yield return Params;
        yield return DataSource;

        // Block form
        if (OpenBrace != null) yield return OpenBrace;
        if (OpenPipe != null) yield return OpenPipe;
        if (IndexName != null) yield return IndexName;
        if (Comma != null) yield return Comma;
        if (ValueName != null) yield return ValueName;
        if (ClosePipe != null) yield return ClosePipe;
        if (Body != null)
        {
            foreach (var t in Body)
                yield return t;
        }

        if (CloseBrace != null) yield return CloseBrace;

        // Projection form
        if (FatArrow != null) yield return FatArrow;
        if (ProjectionName != null) yield return ProjectionName;
    }

}

/// <summary>
/// Each-block parameters: <c>( propName: value, ... )</c>
/// </summary>
public sealed class EachParamsSyntax : SyntaxNode
{
    public SyntaxToken OpenParen { get; }
    public ObjectLiteralExpressionSyntax ObjectLiteral { get; }
    public SyntaxToken CloseParen { get; }

    public EachParamsSyntax(SyntaxToken openParen, ObjectLiteralExpressionSyntax objectLiteral,
        SyntaxToken closeParen)
        : base(SyntaxKind.EachParams)
    {
        OpenParen = openParen;
        ObjectLiteral = objectLiteral;
        CloseParen = closeParen;
    }

    /// <summary>
    /// Convenience accessor: the property assignments inside the object literal.
    /// </summary>
    public SyntaxList<PropertyAssignmentSyntax> Properties => ObjectLiteral.Properties;

    public override int FullWidth
    {
        get
        {
            int w = OpenParen.FullWidth;
            w += ObjectLiteral.FullWidth;
            w += CloseParen.FullWidth;
            return w;
        }
    }

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return OpenParen;
        yield return ObjectLiteral;
        yield return CloseParen;
    }

}
