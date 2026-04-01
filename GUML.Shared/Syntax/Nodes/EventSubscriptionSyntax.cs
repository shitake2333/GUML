using GUML.Shared.Syntax.Nodes.Expressions;

namespace GUML.Shared.Syntax.Nodes;

/// <summary>
/// An event subscription: <c>#event_name: handler_expression</c>
/// </summary>
public sealed class EventSubscriptionSyntax : SyntaxNode
{
    public SyntaxToken EventRef { get; }
    public SyntaxToken Colon { get; }
    public ExpressionSyntax Handler { get; }
    public SyntaxToken? Comma { get; }

    public EventSubscriptionSyntax(SyntaxToken eventRef, SyntaxToken colon, ExpressionSyntax handler,
        SyntaxToken? comma)
        : base(SyntaxKind.EventSubscription)
    {
        EventRef = eventRef;
        Colon = colon;
        Handler = handler;
        Comma = comma;
    }

    public override int FullWidth =>
        EventRef.FullWidth + Colon.FullWidth + Handler.FullWidth + (Comma?.FullWidth ?? 0);

    public override IEnumerable<SyntaxNodeOrToken> ChildNodesAndTokens()
    {
        yield return EventRef;
        yield return Colon;
        yield return Handler;
        if (Comma != null) yield return Comma;
    }

}
