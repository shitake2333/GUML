namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// Information about a named node (@alias) declaration,
/// produced by semantic analysis when resolving alias references.
/// </summary>
/// <param name="Name">Alias name including the '@' prefix (e.g. "@my_label").</param>
/// <param name="TypeName">Component type name of the aliased node (e.g. "Label").</param>
/// <param name="DeclarationToken">The AliasRefToken at the declaration site.</param>
/// <param name="DocumentationComment">Optional documentation comment from the component declaration.</param>
public sealed record AliasInfo(
    string Name,
    string TypeName,
    SyntaxToken DeclarationToken,
    DocumentationCommentSyntax? DocumentationComment = null);
