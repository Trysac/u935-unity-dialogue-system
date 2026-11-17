namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Identifies the kind of step produced by a <see cref="DialogueRunner"/>.
/// </summary>
public enum StepKind
{
    /// <summary>A spoken line of dialogue.</summary>
    Line,

    /// <summary>A player choice among visible options.</summary>
    Choice,

    /// <summary>The end of the dialogue.</summary>
    End,
}

/// <summary>
/// Represents an interactive step reached while running a dialogue.
/// </summary>
/// <param name="Kind">The kind of step (<see cref="StepKind.Line"/>, <see cref="StepKind.Choice"/>, or <see cref="StepKind.End"/>).</param>
/// <param name="Speaker">The speaker identifier for a line step, or null for choice and end steps.</param>
/// <param name="Text">The localized line text for a line step, or null for choice and end steps.</param>
/// <param name="Choices">The localized option texts for a choice step, or an empty list otherwise.</param>
public sealed record DialogueStep(
    StepKind Kind,
    string? Speaker,
    string? Text,
    IReadOnlyList<string> Choices);
