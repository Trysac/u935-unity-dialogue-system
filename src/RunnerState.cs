namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Serializable snapshot of a <see cref="DialogueRunner"/> and its <see cref="VariableStore"/>.
/// </summary>
/// <param name="CurrentNodeId">The ID of the current interactive node, or null if the runner has not started or has finished.</param>
/// <param name="IsFinished">True if the dialogue has reached an end step; otherwise false.</param>
/// <param name="IntVariables">Snapshot of integer variables.</param>
/// <param name="BoolVariables">Snapshot of boolean variables.</param>
/// <param name="StringVariables">Snapshot of string variables.</param>
public sealed record RunnerState(
    string? CurrentNodeId,
    bool IsFinished,
    IReadOnlyDictionary<string, int> IntVariables,
    IReadOnlyDictionary<string, bool> BoolVariables,
    IReadOnlyDictionary<string, string> StringVariables);
