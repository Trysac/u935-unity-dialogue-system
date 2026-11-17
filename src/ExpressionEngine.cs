using System.Globalization;
using System.Text;

namespace Trysac.UnityDialogueSystem;

internal enum TokenKind
{
    End,
    IntLiteral,
    BoolLiteral,
    StringLiteral,
    Identifier,
    OrOr,
    AndAnd,
    EqualEqual,
    BangEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Bang,
    LParen,
    RParen,
}

internal readonly record struct Token(TokenKind Kind, string Text, int IntValue, bool BoolValue);

internal abstract record ExprNode
{
    public abstract VariableType CheckType(Func<string, VariableType> resolveVariable);
    public abstract object Evaluate(VariableStore variables);
}

internal sealed record LiteralExpr(VariableType Type, object Value) : ExprNode
{
    public override VariableType CheckType(Func<string, VariableType> resolveVariable) => Type;
    public override object Evaluate(VariableStore variables) => Value;
}

internal sealed record VariableExpr(string Name) : ExprNode
{
    public override VariableType CheckType(Func<string, VariableType> resolveVariable) => resolveVariable(Name);
    public override object Evaluate(VariableStore variables) => variables.Get(Name);
}

internal sealed record UnaryExpr(TokenKind Operator, ExprNode Operand) : ExprNode
{
    public override VariableType CheckType(Func<string, VariableType> resolveVariable)
    {
        var operandType = Operand.CheckType(resolveVariable);
        return Operator switch
        {
            TokenKind.Bang => operandType == VariableType.Bool
                ? VariableType.Bool
                : throw new DialogueException("type_mismatch", "Operator '!' requires a bool operand."),
            TokenKind.Minus => operandType == VariableType.Int
                ? VariableType.Int
                : throw new DialogueException("type_mismatch", "Unary '-' requires an int operand."),
            _ => throw new DialogueException("invalid_expression", "Invalid unary operator."),
        };
    }

    public override object Evaluate(VariableStore variables)
    {
        var val = Operand.Evaluate(variables);
        if (Operator == TokenKind.Bang)
        {
            return !(bool)val;
        }

        var intVal = (int)val;
        try
        {
            return checked(-intVal);
        }
        catch (OverflowException ex)
        {
            throw new DialogueException("overflow", "Integer overflow in unary negation.", ex);
        }
    }
}

internal sealed record BinaryExpr(TokenKind Operator, ExprNode Left, ExprNode Right) : ExprNode
{
    public override VariableType CheckType(Func<string, VariableType> resolveVariable)
    {
        var leftType = Left.CheckType(resolveVariable);
        var rightType = Right.CheckType(resolveVariable);

        switch (Operator)
        {
            case TokenKind.OrOr:
            case TokenKind.AndAnd:
                if (leftType != VariableType.Bool || rightType != VariableType.Bool)
                {
                    throw new DialogueException("type_mismatch", "Logical operators '&&' and '||' require bool operands.");
                }
                return VariableType.Bool;

            case TokenKind.EqualEqual:
            case TokenKind.BangEqual:
                if (leftType != rightType)
                {
                    throw new DialogueException("type_mismatch", $"Cannot compare '{leftType}' with '{rightType}'.");
                }
                return VariableType.Bool;

            case TokenKind.Less:
            case TokenKind.LessEqual:
            case TokenKind.Greater:
            case TokenKind.GreaterEqual:
                if (leftType != VariableType.Int || rightType != VariableType.Int)
                {
                    throw new DialogueException("type_mismatch", "Relational operators require int operands.");
                }
                return VariableType.Bool;

            case TokenKind.Plus:
                if (leftType == VariableType.Int && rightType == VariableType.Int)
                {
                    return VariableType.Int;
                }
                if (leftType == VariableType.String && rightType == VariableType.String)
                {
                    return VariableType.String;
                }
                throw new DialogueException("type_mismatch", "Operator '+' requires two int or two string operands.");

            case TokenKind.Minus:
            case TokenKind.Star:
            case TokenKind.Slash:
            case TokenKind.Percent:
                if (leftType != VariableType.Int || rightType != VariableType.Int)
                {
                    throw new DialogueException("type_mismatch", "Arithmetic operators '-', '*', '/', '%' require int operands.");
                }
                return VariableType.Int;

            default:
                throw new DialogueException("invalid_expression", "Unsupported binary operator.");
        }
    }

    public override object Evaluate(VariableStore variables)
    {
        if (Operator == TokenKind.OrOr)
        {
            var l = (bool)Left.Evaluate(variables);
            if (l)
            {
                return true;
            }
            return (bool)Right.Evaluate(variables);
        }

        if (Operator == TokenKind.AndAnd)
        {
            var l = (bool)Left.Evaluate(variables);
            if (!l)
            {
                return false;
            }
            return (bool)Right.Evaluate(variables);
        }

        var leftVal = Left.Evaluate(variables);
        var rightVal = Right.Evaluate(variables);

        switch (Operator)
        {
            case TokenKind.EqualEqual:
                return Equals(leftVal, rightVal);
            case TokenKind.BangEqual:
                return !Equals(leftVal, rightVal);
            case TokenKind.Less:
                return (int)leftVal < (int)rightVal;
            case TokenKind.LessEqual:
                return (int)leftVal <= (int)rightVal;
            case TokenKind.Greater:
                return (int)leftVal > (int)rightVal;
            case TokenKind.GreaterEqual:
                return (int)leftVal >= (int)rightVal;
            case TokenKind.Plus:
                if (leftVal is string ls && rightVal is string rs)
                {
                    return string.Concat(ls, rs);
                }
                try
                {
                    return checked((int)leftVal + (int)rightVal);
                }
                catch (OverflowException ex)
                {
                    throw new DialogueException("overflow", "Integer overflow in addition.", ex);
                }
            case TokenKind.Minus:
                try
                {
                    return checked((int)leftVal - (int)rightVal);
                }
                catch (OverflowException ex)
                {
                    throw new DialogueException("overflow", "Integer overflow in subtraction.", ex);
                }
            case TokenKind.Star:
                try
                {
                    return checked((int)leftVal * (int)rightVal);
                }
                catch (OverflowException ex)
                {
                    throw new DialogueException("overflow", "Integer overflow in multiplication.", ex);
                }
            case TokenKind.Slash:
                {
                    var divisor = (int)rightVal;
                    if (divisor == 0)
                    {
                        throw new DialogueException("division_by_zero", "Division by zero.");
                    }
                    try
                    {
                        return checked((int)leftVal / divisor);
                    }
                    catch (OverflowException ex)
                    {
                        throw new DialogueException("overflow", "Integer overflow in division.", ex);
                    }
                }
            case TokenKind.Percent:
                {
                    var divisor = (int)rightVal;
                    if (divisor == 0)
                    {
                        throw new DialogueException("division_by_zero", "Modulo by zero.");
                    }
                    try
                    {
                        return checked((int)leftVal % divisor);
                    }
                    catch (OverflowException ex)
                    {
                        throw new DialogueException("overflow", "Integer overflow in modulo.", ex);
                    }
                }
            default:
                throw new DialogueException("invalid_expression", "Unsupported binary operator.");
        }
    }
}

internal sealed record CompiledAssignment(string TargetName, VariableType TargetType, ExprNode Expression);

internal static class ExpressionParser
{
    public static ExprNode Parse(string source, DialogueLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > limits.MaxExpressionLength)
        {
            throw new DialogueException("input_too_large", $"Expression exceeds maximum length of {limits.MaxExpressionLength} characters.");
        }

        var tokens = Tokenize(source);
        var state = new ParserState(tokens, limits.MaxExpressionDepth);
        var expr = state.ParseExpression(1);
        if (state.Current.Kind != TokenKind.End)
        {
            throw new DialogueException("invalid_expression", $"Unexpected token '{state.Current.Text}' in expression.");
        }

        return expr;
    }

    public static CompiledAssignment ParseAssignment(
        string assignmentSource,
        DialogueLimits limits,
        Func<string, VariableType> resolveVariable)
    {
        ArgumentNullException.ThrowIfNull(assignmentSource);
        if (assignmentSource.Length > limits.MaxExpressionLength)
        {
            throw new DialogueException("input_too_large", $"Assignment exceeds maximum length of {limits.MaxExpressionLength} characters.");
        }

        int eqIndex = FindAssignmentEquals(assignmentSource);
        if (eqIndex <= 0)
        {
            throw new DialogueException("invalid_expression", $"Invalid assignment syntax: '{assignmentSource}'.");
        }

        var targetName = assignmentSource[..eqIndex].Trim();
        var rhsSource = assignmentSource[(eqIndex + 1)..].Trim();

        if (!IsValidIdentifier(targetName) || targetName is "true" or "false")
        {
            throw new DialogueException("invalid_expression", $"Invalid variable name '{targetName}' in assignment.");
        }

        if (rhsSource.Length == 0)
        {
            throw new DialogueException("invalid_expression", "Assignment right-hand side is empty.");
        }

        var targetType = resolveVariable(targetName);
        var expr = Parse(rhsSource, limits);
        var exprType = expr.CheckType(resolveVariable);
        if (exprType != targetType)
        {
            throw new DialogueException("type_mismatch", $"Cannot assign '{exprType}' to variable '{targetName}' of type '{targetType}'.");
        }

        return new CompiledAssignment(targetName, targetType, expr);
    }

    private static int FindAssignmentEquals(string text)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && c == '=')
            {
                bool prevIsOp = i > 0 && (text[i - 1] is '=' or '!' or '<' or '>');
                bool nextIsEq = i + 1 < text.Length && text[i + 1] == '=';
                if (!prevIsOp && !nextIsEq)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (!(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }

        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        int n = source.Length;

        while (i < n)
        {
            char c = source[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '|' && i + 1 < n && source[i + 1] == '|')
            {
                tokens.Add(new Token(TokenKind.OrOr, "||", 0, false));
                i += 2;
                continue;
            }

            if (c == '&' && i + 1 < n && source[i + 1] == '&')
            {
                tokens.Add(new Token(TokenKind.AndAnd, "&&", 0, false));
                i += 2;
                continue;
            }

            if (c == '=' && i + 1 < n && source[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.EqualEqual, "==", 0, false));
                i += 2;
                continue;
            }

            if (c == '!' && i + 1 < n && source[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.BangEqual, "!=", 0, false));
                i += 2;
                continue;
            }

            if (c == '<')
            {
                if (i + 1 < n && source[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.LessEqual, "<=", 0, false));
                    i += 2;
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Less, "<", 0, false));
                    i++;
                }
                continue;
            }

            if (c == '>')
            {
                if (i + 1 < n && source[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.GreaterEqual, ">=", 0, false));
                    i += 2;
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Greater, ">", 0, false));
                    i++;
                }
                continue;
            }

            switch (c)
            {
                case '+':
                    tokens.Add(new Token(TokenKind.Plus, "+", 0, false));
                    i++;
                    continue;
                case '-':
                    tokens.Add(new Token(TokenKind.Minus, "-", 0, false));
                    i++;
                    continue;
                case '*':
                    tokens.Add(new Token(TokenKind.Star, "*", 0, false));
                    i++;
                    continue;
                case '/':
                    tokens.Add(new Token(TokenKind.Slash, "/", 0, false));
                    i++;
                    continue;
                case '%':
                    tokens.Add(new Token(TokenKind.Percent, "%", 0, false));
                    i++;
                    continue;
                case '!':
                    tokens.Add(new Token(TokenKind.Bang, "!", 0, false));
                    i++;
                    continue;
                case '(':
                    tokens.Add(new Token(TokenKind.LParen, "(", 0, false));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenKind.RParen, ")", 0, false));
                    i++;
                    continue;
            }

            if (c == '\'')
            {
                i++;
                var sb = new StringBuilder();
                bool closed = false;
                while (i < n)
                {
                    char sc = source[i];
                    if (sc == '\\' && i + 1 < n && (source[i + 1] == '\'' || source[i + 1] == '\\'))
                    {
                        sb.Append(source[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (sc == '\'')
                    {
                        closed = true;
                        i++;
                        break;
                    }
                    sb.Append(sc);
                    i++;
                }

                if (!closed)
                {
                    throw new DialogueException("invalid_expression", "Unterminated string literal in expression.");
                }

                tokens.Add(new Token(TokenKind.StringLiteral, sb.ToString(), 0, false));
                continue;
            }

            if (char.IsAsciiDigit(c))
            {
                int start = i;
                while (i < n && char.IsAsciiDigit(source[i]))
                {
                    i++;
                }

                var numSpan = source.AsSpan(start, i - start);
                if (!int.TryParse(numSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int intVal))
                {
                    if (numSpan.SequenceEqual("2147483648".AsSpan()) && tokens.Count > 0 && tokens[^1].Kind == TokenKind.Minus)
                    {
                        // Handled below only if needed, otherwise overflow
                    }
                    throw new DialogueException("overflow", $"Integer literal '{numSpan.ToString()}' overflows 32-bit signed integer.");
                }

                tokens.Add(new Token(TokenKind.IntLiteral, numSpan.ToString(), intVal, false));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
                {
                    i++;
                }

                var ident = source[start..i];
                if (ident == "true")
                {
                    tokens.Add(new Token(TokenKind.BoolLiteral, ident, 0, true));
                }
                else if (ident == "false")
                {
                    tokens.Add(new Token(TokenKind.BoolLiteral, ident, 0, false));
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Identifier, ident, 0, false));
                }
                continue;
            }

            throw new DialogueException("invalid_expression", $"Unexpected character '{c}' in expression.");
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, 0, false));
        return tokens;
    }

    private sealed class ParserState
    {
        private readonly List<Token> _tokens;
        private readonly int _maxDepth;
        private int _pos;

        public ParserState(List<Token> tokens, int maxDepth)
        {
            _tokens = tokens;
            _maxDepth = maxDepth;
        }

        public Token Current => _tokens[_pos];

        private Token Consume()
        {
            var t = _tokens[_pos];
            if (_pos < _tokens.Count - 1)
            {
                _pos++;
            }
            return t;
        }

        private void CheckDepth(int depth)
        {
            if (depth > _maxDepth)
            {
                throw new DialogueException("too_deep", $"Expression exceeds maximum nesting depth of {_maxDepth}.");
            }
        }

        public ExprNode ParseExpression(int depth)
        {
            CheckDepth(depth);
            return ParseOr(depth);
        }

        private ExprNode ParseOr(int depth)
        {
            var left = ParseAnd(depth);
            while (Current.Kind == TokenKind.OrOr)
            {
                var op = Consume().Kind;
                var right = ParseAnd(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseAnd(int depth)
        {
            var left = ParseEquality(depth);
            while (Current.Kind == TokenKind.AndAnd)
            {
                var op = Consume().Kind;
                var right = ParseEquality(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseEquality(int depth)
        {
            var left = ParseRelational(depth);
            while (Current.Kind is TokenKind.EqualEqual or TokenKind.BangEqual)
            {
                var op = Consume().Kind;
                var right = ParseRelational(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseRelational(int depth)
        {
            var left = ParseAdditive(depth);
            while (Current.Kind is TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual)
            {
                var op = Consume().Kind;
                var right = ParseAdditive(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseAdditive(int depth)
        {
            var left = ParseMultiplicative(depth);
            while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                var op = Consume().Kind;
                var right = ParseMultiplicative(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseMultiplicative(int depth)
        {
            var left = ParseUnary(depth);
            while (Current.Kind is TokenKind.Star or TokenKind.Slash or TokenKind.Percent)
            {
                var op = Consume().Kind;
                var right = ParseUnary(depth + 1);
                left = new BinaryExpr(op, left, right);
            }
            return left;
        }

        private ExprNode ParseUnary(int depth)
        {
            if (Current.Kind is TokenKind.Bang or TokenKind.Minus)
            {
                CheckDepth(depth);
                var op = Consume().Kind;
                var operand = ParseUnary(depth + 1);
                return new UnaryExpr(op, operand);
            }

            return ParsePrimary(depth);
        }

        private ExprNode ParsePrimary(int depth)
        {
            CheckDepth(depth);
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.IntLiteral:
                    Consume();
                    return new LiteralExpr(VariableType.Int, token.IntValue);
                case TokenKind.BoolLiteral:
                    Consume();
                    return new LiteralExpr(VariableType.Bool, token.BoolValue);
                case TokenKind.StringLiteral:
                    Consume();
                    return new LiteralExpr(VariableType.String, token.Text);
                case TokenKind.Identifier:
                    Consume();
                    return new VariableExpr(token.Text);
                case TokenKind.LParen:
                    Consume();
                    var inner = ParseExpression(depth + 1);
                    if (Current.Kind != TokenKind.RParen)
                    {
                        throw new DialogueException("invalid_expression", "Expected closing parenthesis ')'.");
                    }
                    Consume();
                    return inner;
                default:
                    throw new DialogueException("invalid_expression", "Expected primary expression.");
            }
        }
    }
}
