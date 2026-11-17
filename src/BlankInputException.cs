namespace Trysac.UnityDialogueSystem;

/// <summary>The input is empty or holds only whitespace.</summary>
public sealed class BlankInputException : ArgumentException
{
    /// <summary>Creates the exception with the message <c>input is blank</c>.</summary>
    public BlankInputException()
        : base("input is blank")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public BlankInputException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BlankInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
