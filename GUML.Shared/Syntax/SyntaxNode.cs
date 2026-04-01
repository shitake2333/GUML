namespace GUML.Shared.Syntax;

/// <summary>
/// Base class for all syntax nodes in the GUML concrete syntax tree (red tree layer).
/// Each node knows its kind, parent, absolute position, and children.
/// </summary>
public abstract class SyntaxNode
{
    /// <summary>
    /// The kind of syntax this node represents.
    /// </summary>
    public SyntaxKind Kind { get; }

    /// <summary>
    /// The parent node, or null for the root node.
    /// </summary>
    public SyntaxNode? Parent { get; internal set; }

    /// <summary>
    /// The absolute character offset of this node in the source text (start of first token's leading trivia).
    /// </summary>
    internal int Position { get; set; }

    /// <summary>
    /// The span including leading/trailing trivia of all child tokens.
    /// </summary>
    public TextSpan FullSpan => new TextSpan(Position, FullWidth);

    /// <summary>
    /// The span excluding leading trivia of the first token and trailing trivia of the last token.
    /// </summary>
    public TextSpan Span
    {
        get
        {
            var first = FirstToken();
            var last = LastToken();
            if (first == null || last == null)
                return new TextSpan(Position, 0);
            int start = first.Span.Start;
            int end = last.Span.End;
            return TextSpan.FromBounds(start, end);
        }
    }

    /// <summary>
    /// The total width in characters of this node, including all trivia.
    /// </summary>
    public abstract int FullWidth { get; }

    /// <summary>
    /// Whether this node or any descendant contains diagnostics.
    /// </summary>
    public virtual bool ContainsDiagnostics => false;

    protected SyntaxNode(SyntaxKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// List all child nodes and tokens in document order.
    /// Subclasses override this to enumerate their fields.
    /// </summary>
    public abstract IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens();

    /// <summary>
    /// Enumerate all descendant nodes (depth-first, pre-order).
    /// </summary>
    public IEnumerable<SyntaxNode> DescendantNodes()
    {
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsNode)
            {
                var node = child.AsNode();
                yield return node;
                foreach (var desc in node.DescendantNodes())
                    yield return desc;
            }
        }
    }

    /// <summary>
    /// Enumerate all descendant tokens (depth-first).
    /// </summary>
    public IEnumerable<SyntaxToken> DescendantTokens()
    {
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
            {
                yield return child.AsToken();
            }
            else
            {
                foreach (var token in child.AsNode().DescendantTokens())
                    yield return token;
            }
        }
    }

    /// <summary>
    /// Find the deepest node that fully contains the given position.
    /// </summary>
    public SyntaxNode? FindNode(int position)
    {
        if (!FullSpan.Contains(position))
            return null;

        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsNode)
            {
                var found = child.AsNode().FindNode(position);
                if (found != null)
                    return found;
            }
        }

        return this;
    }

    /// <summary>
    /// Find the token at the given position.
    /// </summary>
    public SyntaxToken? FindToken(int position)
    {
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
            {
                var token = child.AsToken();
                if (token.FullSpan.Contains(position))
                    return token;
            }
            else
            {
                var found = child.AsNode().FindToken(position);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the first token in this subtree.
    /// </summary>
    public SyntaxToken? FirstToken()
    {
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
                return child.AsToken();
            var t = child.AsNode().FirstToken();
            if (t != null)
                return t;
        }

        return null;
    }

    /// <summary>
    /// Get the last token in this subtree.
    /// </summary>
    public SyntaxToken? LastToken()
    {
        SyntaxToken? last = null;
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
                last = child.AsToken();
            else
            {
                var t = child.AsNode().LastToken();
                if (t != null)
                    last = t;
            }
        }

        return last;
    }

    /// <summary>
    /// Reconstruct the full source text from this node (full fidelity, including trivia).
    /// </summary>
    public string ToFullString()
    {
        var sb = new StringBuilder(FullWidth);
        AppendFullString(sb);
        return sb.ToString();
    }

    internal void AppendFullString(StringBuilder sb)
    {
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
                sb.Append(child.AsToken().ToFullString());
            else
                child.AsNode().AppendFullString(sb);
        }
    }

    public override string ToString() => ToFullString();

    /// <summary>
    /// Set position and parent for this node and all children.
    /// Called once when the red tree is constructed.
    /// </summary>
    internal void ComputePositions(SyntaxNode? parent, int position)
    {
        Parent = parent;
        Position = position;
        int offset = position;
        foreach (var child in ChildNodesAndTokens())
        {
            if (child.IsToken)
            {
                var token = child.AsToken();
                token._parentNode = this;
                token._position = offset;
                offset += token.FullWidth;
            }
            else
            {
                var node = child.AsNode();
                node.ComputePositions(this, offset);
                offset += node.FullWidth;
            }
        }
    }
}
