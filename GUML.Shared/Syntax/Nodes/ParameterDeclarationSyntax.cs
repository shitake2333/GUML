using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// A parameter declaration: <c>[/// doc] param Type name</c> or <c>[/// doc] param Type name := defaultExpr</c>
/// </summary>
public sealed class ParameterDeclarationSyntax : SyntaxNode
{
    /// <summary>
    /// Optional documentation comment block preceding this parameter declaration.
    /// </summary>
    public DocumentationCommentSyntax? DocumentationComment { get; }

    public SyntaxToken ParamKeyword { get; }
    public SyntaxToken TypeName { get; }
    public SyntaxToken Name { get; }

    /// <summary>
    /// The default-value operator (<c>:</c> or <c>:=</c>), if present.
    /// </summary>
    public SyntaxToken? DefaultOperator { get; }

    /// <summary>
    /// The default value expression, if present.
    /// </summary>
    public ExpressionSyntax? DefaultValue { get; }

    public ParameterDeclarationSyntax(
        DocumentationCommentSyntax? documentationComment,
        SyntaxToken paramKeyword,
        SyntaxToken typeName,
        SyntaxToken name,
        SyntaxToken? defaultOperator,
        ExpressionSyntax? defaultValue)
        : base(SyntaxKind.ParameterDeclaration)
    {
        DocumentationComment = documentationComment;
        ParamKeyword = paramKeyword;
        TypeName = typeName;
        Name = name;
        DefaultOperator = defaultOperator;
        DefaultValue = defaultValue;
    }

    public override int FullWidth =>
        (DocumentationComment?.FullWidth ?? 0)
        + ParamKeyword.FullWidth + TypeName.FullWidth + Name.FullWidth
        + (DefaultOperator?.FullWidth ?? 0) + (DefaultValue?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        if (DocumentationComment != null) yield return DocumentationComment;
        yield return ParamKeyword;
        yield return TypeName;
        yield return Name;
        if (DefaultOperator != null) yield return DefaultOperator;
        if (DefaultValue != null) yield return DefaultValue;
    }

}
