using RegexTokenizeGenerator;

namespace GUML;

public abstract class GumlExprNode : IPosInfo
{
    public GumlOpNode? Parent { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public abstract void Add(GumlExprNode node, bool isLeft = false);
}

public abstract class GumlOpNode : GumlExprNode
{
    public bool FirstPrecedence { get; set; }
    public string Op { get; init; }
    private GumlExprNode _right = null!;
    public GumlExprNode Right
    {
        get => _right;
        set
        {
            _right = value;
            _right.Parent = this;
        }
    }
}
