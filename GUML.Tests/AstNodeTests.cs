namespace GUML.Tests;

[TestClass]
public class InfixOpNodeTests
{
    [TestMethod]
    public void Add_Left_SetsLeftProperty()
    {
        var infix = new InfixOpNode { Op = "+" };
        var left = new GumlValueNode { ValueType = GumlValueType.Int, IntValue = 1 };

        infix.Add(left, isLeft: true);

        Assert.AreSame(left, infix.Left);
        Assert.AreSame(infix, left.Parent);
    }

    [TestMethod]
    public void Add_Right_SetsRightProperty()
    {
        var infix = new InfixOpNode { Op = "+" };
        var right = new GumlValueNode { ValueType = GumlValueType.Int, IntValue = 2 };

        infix.Add(right, isLeft: false);

        Assert.AreSame(right, infix.Right);
        Assert.AreSame(infix, right.Parent);
    }

    [TestMethod]
    public void Left_Setter_SetsParent()
    {
        var infix = new InfixOpNode { Op = "-" };
        var left = new GumlValueNode { ValueType = GumlValueType.Int };

        infix.Left = left;

        Assert.AreSame(infix, left.Parent);
    }

    [TestMethod]
    public void Right_Setter_SetsParent()
    {
        var infix = new InfixOpNode { Op = "*" };
        var right = new GumlValueNode { ValueType = GumlValueType.Int };

        infix.Right = right;

        Assert.AreSame(infix, right.Parent);
    }

    [TestMethod]
    public void OpPrecedence_ContainsAllOperators()
    {
        string[] expectedOps = ["||", "&&", "!=", "==", ">=", "<=", ">", "<", "+", "-", "*", "/", "%"];
        foreach (var op in expectedOps)
        {
            Assert.IsTrue(InfixOpNode.OpPrecedence.ContainsKey(op), $"Missing operator: {op}");
        }
    }

    [TestMethod]
    public void OpPrecedence_MultiplyHigherThanAdd()
    {
        Assert.IsTrue(InfixOpNode.OpPrecedence["*"] > InfixOpNode.OpPrecedence["+"]);
    }

    [TestMethod]
    public void OpPrecedence_ComparisonHigherThanLogical()
    {
        Assert.IsTrue(InfixOpNode.OpPrecedence["=="] > InfixOpNode.OpPrecedence["&&"]);
    }
}

[TestClass]
public class PrefixOpNodeTests
{
    [TestMethod]
    public void Add_Right_SetsRightProperty()
    {
        var prefix = new PrefixOpNode { Op = "!" };
        var operand = new GumlValueNode { ValueType = GumlValueType.Boolean, BooleanValue = true };

        prefix.Add(operand, isLeft: false);

        Assert.AreSame(operand, prefix.Right);
        Assert.AreSame(prefix, operand.Parent);
    }

    [TestMethod]
    public void Add_Left_ThrowsException()
    {
        var prefix = new PrefixOpNode { Op = "-" };
        var operand = new GumlValueNode { ValueType = GumlValueType.Int };

        Assert.ThrowsExactly<Exception>(() => prefix.Add(operand, isLeft: true));
    }

    [TestMethod]
    public void OpPrecedence_ContainsAllPrefixOps()
    {
        Assert.IsTrue(PrefixOpNode.OpPrecedence.ContainsKey("!"));
        Assert.IsTrue(PrefixOpNode.OpPrecedence.ContainsKey("+"));
        Assert.IsTrue(PrefixOpNode.OpPrecedence.ContainsKey("-"));
    }
}

[TestClass]
public class GumlValueNodeTests
{
    [TestMethod]
    public void Add_AlwaysThrows()
    {
        var node = new GumlValueNode
        {
            ValueType = GumlValueType.Int,
            IntValue = 1,
            Start = 0,
            End = 1,
            Line = 1,
            Column = 1
        };
        var child = new GumlValueNode { ValueType = GumlValueType.Int };

        Assert.ThrowsExactly<GumlParserException>(() => node.Add(child));
    }
}
