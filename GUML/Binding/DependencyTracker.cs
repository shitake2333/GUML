using System.ComponentModel;
using GUML.Shared.Syntax;
using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Binding;

/// <summary>
/// Tracks property dependencies from a binding expression CST
/// and subscribes to <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// for precise change notification filtering.
/// </summary>
public sealed class DependencyTracker : IDisposable
{
    private readonly List<(INotifyPropertyChanged Source, PropertyChangedEventHandler Handler)> _subscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Collects all controller property names referenced in the given expression CST.
    /// Performs a static walk of the CST to find <see cref="MemberAccessExpressionSyntax"/> chains
    /// whose root is a <see cref="ReferenceExpressionSyntax"/> with <see cref="SyntaxKind.GlobalRefToken"/>
    /// (e.g., <c>$controller.SomeProperty</c>).
    /// </summary>
    /// <param name="expression">The expression CST to analyze.</param>
    /// <returns>A set of top-level property names that the expression depends on.</returns>
    public static HashSet<string> CollectDependencies(ExpressionSyntax expression)
    {
        var deps = new HashSet<string>();
        CollectDependenciesRecursive(expression, deps);
        return deps;
    }

    /// <summary>
    /// Subscribes to <see cref="INotifyPropertyChanged.PropertyChanged"/> on the given source object,
    /// filtered by property names. When any of the specified properties change, the callback is invoked.
    /// </summary>
    /// <param name="source">The object implementing <see cref="INotifyPropertyChanged"/>.</param>
    /// <param name="propertyNames">
    /// Property names to filter on. If empty, all property changes trigger the callback.
    /// </param>
    /// <param name="callback">Action invoked when a tracked property changes.</param>
    public void Subscribe(
        INotifyPropertyChanged source,
        IReadOnlySet<string> propertyNames,
        Action callback)
    {
#if !NETSTANDARD2_0
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed) throw new ObjectDisposedException(nameof(DependencyTracker));
#endif

        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (propertyNames.Count == 0 ||
                (e.PropertyName != null && propertyNames.Contains(e.PropertyName)))
            {
                callback();
            }
        };

        source.PropertyChanged += handler;
        _subscriptions.Add((source, handler));
    }

    /// <summary>
    /// Unsubscribes all registered <see cref="INotifyPropertyChanged.PropertyChanged"/> handlers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach ((INotifyPropertyChanged source, PropertyChangedEventHandler handler) in _subscriptions)
        {
            source.PropertyChanged -= handler;
        }

        _subscriptions.Clear();
    }

    private static void CollectDependenciesRecursive(ExpressionSyntax node, HashSet<string> deps)
    {
        switch (node)
        {
            case MemberAccessExpressionSyntax memberAccess:
                // Check if root is a global ref (e.g., $controller.Property)
                var root = GetMemberAccessRoot(memberAccess);
                if (root is ReferenceExpressionSyntax { Identifier.Kind: SyntaxKind.GlobalRefToken })
                {
                    // The first member name after the global ref is the dependency
                    string? firstMember = GetFirstMemberName(memberAccess);
                    if (firstMember != null)
                        deps.Add(firstMember);
                }

                // Also recurse into the expression subtree for other dependencies
                CollectDependenciesRecursive(memberAccess.Expression, deps);
                break;

            case StructExpressionSyntax structExpr:
                if (structExpr.PositionalArgs != null)
                {
                    foreach (var arg in structExpr.PositionalArgs)
                        CollectDependenciesRecursive(arg, deps);
                }

                if (structExpr.NamedArgs != null)
                {
                    foreach (var prop in structExpr.NamedArgs.Properties)
                        CollectDependenciesRecursive(prop.Value, deps);
                }

                break;

            case BinaryExpressionSyntax binaryExpr:
                CollectDependenciesRecursive(binaryExpr.Left, deps);
                CollectDependenciesRecursive(binaryExpr.Right, deps);
                break;

            case PrefixUnaryExpressionSyntax prefixExpr:
                CollectDependenciesRecursive(prefixExpr.Operand, deps);
                break;

            case ConditionalExpressionSyntax conditional:
                CollectDependenciesRecursive(conditional.Condition, deps);
                CollectDependenciesRecursive(conditional.WhenTrue, deps);
                CollectDependenciesRecursive(conditional.WhenFalse, deps);
                break;

            case CallExpressionSyntax call:
                CollectDependenciesRecursive(call.Expression, deps);
                foreach (var arg in call.Arguments)
                    CollectDependenciesRecursive(arg, deps);
                break;

            case ResourceExpressionSyntax resource:
                CollectDependenciesRecursive(resource.Path, deps);
                break;

            case DictionaryLiteralExpressionSyntax dict:
                foreach (var entry in dict.Entries)
                {
                    CollectDependenciesRecursive(entry.Key, deps);
                    CollectDependenciesRecursive(entry.Value, deps);
                }

                break;

            case ArrayLiteralExpressionSyntax array:
                foreach (var elem in array.Elements)
                    CollectDependenciesRecursive(elem, deps);
                break;

            case ParenthesizedExpressionSyntax paren:
                CollectDependenciesRecursive(paren.Expression, deps);
                break;

            case ObjectLiteralExpressionSyntax objLit:
                foreach (var prop in objLit.Properties)
                    CollectDependenciesRecursive(prop.Value, deps);
                break;

            case ObjectCreationExpressionSyntax objCreate:
                foreach (var prop in objCreate.Properties)
                    CollectDependenciesRecursive(prop.Value, deps);
                break;

            case TemplateStringExpressionSyntax templateStr:
                foreach (var part in templateStr.Parts)
                {
                    if (part is TemplateStringInterpolationSyntax interp)
                        CollectDependenciesRecursive(interp.Expression, deps);
                }

                break;
        }
    }

    /// <summary>
    /// Walks the <see cref="MemberAccessExpressionSyntax.Expression"/> chain
    /// to find the root expression node (the leftmost non-member-access expression).
    /// </summary>
    private static ExpressionSyntax GetMemberAccessRoot(MemberAccessExpressionSyntax memberAccess)
    {
        ExpressionSyntax current = memberAccess;
        while (current is MemberAccessExpressionSyntax m)
            current = m.Expression;
        return current;
    }

    /// <summary>
    /// For a member access chain like <c>$controller.Foo.Bar</c>, returns the first
    /// member name after the root reference (i.e. <c>"Foo"</c>).
    /// </summary>
    private static string? GetFirstMemberName(MemberAccessExpressionSyntax memberAccess)
    {
        if (memberAccess.Expression is MemberAccessExpressionSyntax inner)
            return GetFirstMemberName(inner);
        // Expression is the root (e.g., $controller), Name is the first member
        return memberAccess.Name.Text;
    }
}
