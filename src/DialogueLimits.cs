namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Configurable resource and size limits for loading and executing dialogue graphs.
/// </summary>
public sealed record DialogueLimits
{
    /// <summary>
    /// Default maximum JSON payload size in bytes (1 MiB).
    /// </summary>
    public const int DefaultMaxJsonBytes = 1_048_576;

    /// <summary>
    /// Default maximum number of nodes in a dialogue graph.
    /// </summary>
    public const int DefaultMaxNodes = 10_000;

    /// <summary>
    /// Default maximum number of options per choice node.
    /// </summary>
    public const int DefaultMaxOptionsPerChoice = 32;

    /// <summary>
    /// Default maximum length of an expression in characters.
    /// </summary>
    public const int DefaultMaxExpressionLength = 1_000;

    /// <summary>
    /// Default maximum nesting depth of an expression.
    /// </summary>
    public const int DefaultMaxExpressionDepth = 32;

    /// <summary>
    /// Default maximum number of non-interactive steps before a loop error is raised.
    /// </summary>
    public const int DefaultMaxLoopSteps = 1_000;

    /// <summary>
    /// Gets the maximum JSON input size in UTF-8 bytes.
    /// </summary>
    public int MaxJsonBytes { get; init; } = DefaultMaxJsonBytes;

    /// <summary>
    /// Gets the maximum number of nodes allowed in a graph.
    /// </summary>
    public int MaxNodes { get; init; } = DefaultMaxNodes;

    /// <summary>
    /// Gets the maximum number of options allowed on a choice node.
    /// </summary>
    public int MaxOptionsPerChoice { get; init; } = DefaultMaxOptionsPerChoice;

    /// <summary>
    /// Gets the maximum character length of a condition or assignment expression.
    /// </summary>
    public int MaxExpressionLength { get; init; } = DefaultMaxExpressionLength;

    /// <summary>
    /// Gets the maximum AST nesting depth of an expression.
    /// </summary>
    public int MaxExpressionDepth { get; init; } = DefaultMaxExpressionDepth;

    /// <summary>
    /// Gets the maximum number of consecutive non-interactive nodes allowed in one transition.
    /// </summary>
    public int MaxLoopSteps { get; init; } = DefaultMaxLoopSteps;

    /// <summary>
    /// Gets the default limits instance.
    /// </summary>
    public static DialogueLimits Default { get; } = new();
}
