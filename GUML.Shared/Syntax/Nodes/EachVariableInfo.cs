namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// Information about an each-block local variable (index or value),
/// produced by semantic analysis when resolving identifiers inside each blocks.
/// </summary>
/// <param name="Name">The variable name as it appears in the source.</param>
/// <param name="IsIndex">Whether this is the index variable (<c>true</c>) or the value variable (<c>false</c>).</param>
/// <param name="EachBlock">The enclosing <see cref="EachBlockSyntax"/> that declares this variable.</param>
/// <param name="ResolvedType">The inferred type name, or <c>null</c> if the type could not be resolved.</param>
public sealed record EachVariableInfo(
    string Name,
    bool IsIndex,
    EachBlockSyntax EachBlock,
    string? ResolvedType);
