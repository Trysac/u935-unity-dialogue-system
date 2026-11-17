namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Exception thrown when loading, validating, or running a dialogue fails.
/// </summary>
public sealed class DialogueException : ArgumentException
{
    /// <summary>
    /// Gets the stable machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogueException"/> class.
    /// </summary>
    public DialogueException()
        : base("dialogue error")
    {
        Code = "invalid_graph";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogueException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DialogueException(string message)
        : base(Truncate(message))
    {
        Code = "invalid_graph";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogueException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DialogueException(string message, Exception innerException)
        : base(Truncate(message), innerException)
    {
        Code = "invalid_graph";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogueException"/> class with an error code and message.
    /// </summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="message">The human-readable error message.</param>
    /// <param name="innerException">The optional inner exception.</param>
    public DialogueException(string code, string message, Exception? innerException = null)
        : base(Truncate(message), innerException)
    {
        ArgumentNullException.ThrowIfNull(code);
        Code = code;
    }

    private static string Truncate(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return "dialogue error";
        }

        return message.Length <= 200 ? message : string.Concat(message.AsSpan(0, 197), "...");
    }
}
