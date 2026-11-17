namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Evaluates boolean expressions against a <see cref="VariableStore"/>.
/// </summary>
public static class Condition
{
    /// <summary>
    /// Parses, type-checks, and evaluates a boolean condition expression against the given <see cref="VariableStore"/>.
    /// </summary>
    /// <param name="expression">The boolean condition expression.</param>
    /// <param name="variables">The variable store providing variable types and values.</param>
    /// <returns>True if the condition evaluates to true; otherwise false.</returns>
    /// <exception cref="DialogueException">Thrown if the expression is invalid, too deep, references an unknown variable, or does not produce a boolean result.</exception>
    public static bool Evaluate(string expression, VariableStore variables)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(variables);

        var ast = ExpressionParser.Parse(expression, DialogueLimits.Default);
        var resultType = ast.CheckType(name =>
        {
            if (!variables.TryGetType(name, out var varType))
            {
                throw new DialogueException("unknown_variable", $"Unknown variable '{name}'.");
            }
            return varType;
        });

        if (resultType != VariableType.Bool)
        {
            throw new DialogueException("type_mismatch", $"Condition must evaluate to 'bool', but evaluated to '{resultType}'.");
        }

        return (bool)ast.Evaluate(variables);
    }
}
