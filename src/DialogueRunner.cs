namespace Trysac.UnityDialogueSystem;

/// <summary>
/// Executes a <see cref="DialogueGraph"/> step by step against a <see cref="VariableStore"/> and <see cref="Localizer"/>.
/// </summary>
public sealed class DialogueRunner
{
    private readonly DialogueGraph _graph;
    private readonly VariableStore _variables;
    private readonly Localizer _localizer;
    private string? _currentNodeId;
    private bool _finished;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogueRunner"/> class.
    /// Any graph variables not yet declared in <paramref name="variables"/> are seeded with their default values.
    /// </summary>
    /// <param name="graph">The validated dialogue graph to run.</param>
    /// <param name="variables">The variable store holding dialogue state.</param>
    /// <param name="localizer">The localizer used to resolve line and choice texts.</param>
    public DialogueRunner(DialogueGraph graph, VariableStore variables, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(localizer);

        _graph = graph;
        _variables = variables;
        _localizer = localizer;
        _variables.SeedFromGraph(graph);
    }

    /// <summary>
    /// Starts (or restarts) the dialogue at <paramref name="nodeId"/> or the graph's <see cref="DialogueGraph.StartNode"/>.
    /// </summary>
    /// <param name="nodeId">Optional starting node ID; defaults to <see cref="DialogueGraph.StartNode"/>.</param>
    /// <returns>The first interactive <see cref="DialogueStep"/>.</returns>
    /// <exception cref="DialogueException">Thrown with <c>unknown_node</c> if <paramref name="nodeId"/> does not exist, or <c>loop_limit</c> if non-interactive nodes cycle.</exception>
    public DialogueStep Start(string? nodeId = null)
    {
        var target = nodeId ?? _graph.StartNode;
        if (!_graph.NodesById.ContainsKey(target))
        {
            throw new DialogueException("unknown_node", $"Unknown start node '{target}'.");
        }

        _finished = false;
        return RunUntilInteractive(target);
    }

    /// <summary>
    /// Advances past the current line step to the next interactive step.
    /// </summary>
    /// <returns>The next interactive <see cref="DialogueStep"/>.</returns>
    /// <exception cref="DialogueException">Thrown with <c>finished</c> if the dialogue has ended or not started, or <c>invalid_choice</c> if currently at a choice node.</exception>
    public DialogueStep Advance()
    {
        if (_finished || _currentNodeId is null)
        {
            throw new DialogueException("finished", "Cannot advance because the dialogue has finished or has not started.");
        }

        var currentNode = _graph.NodesById[_currentNodeId];
        if (currentNode is LineNode line)
        {
            return RunUntilInteractive(line.NextId);
        }

        if (currentNode is ChoiceNode)
        {
            throw new DialogueException("invalid_choice", "Cannot call Advance on a choice step; call Choose(index) instead.");
        }

        throw new DialogueException("finished", "Dialogue is already at an end node.");
    }

    /// <summary>
    /// Selects a visible option by zero-based <paramref name="index"/> on the current choice step and advances.
    /// </summary>
    /// <param name="index">Zero-based index into the currently visible choices.</param>
    /// <returns>The next interactive <see cref="DialogueStep"/>.</returns>
    /// <exception cref="DialogueException">Thrown with <c>finished</c> if ended, or <c>invalid_choice</c> if not at a choice step or <paramref name="index"/> is out of range.</exception>
    public DialogueStep Choose(int index)
    {
        if (_finished || _currentNodeId is null)
        {
            throw new DialogueException("finished", "Cannot choose because the dialogue has finished or has not started.");
        }

        var currentNode = _graph.NodesById[_currentNodeId];
        if (currentNode is not ChoiceNode choice)
        {
            throw new DialogueException("invalid_choice", "Current step is not a choice step.");
        }

        var visibleOptions = GetVisibleOptions(choice);
        if (index < 0 || index >= visibleOptions.Count)
        {
            throw new DialogueException("invalid_choice", $"Choice index {index} is out of range (visible choices: {visibleOptions.Count}).");
        }

        var selected = visibleOptions[index];
        return RunUntilInteractive(selected.NextId);
    }

    /// <summary>
    /// Captures a serializable snapshot of the runner's position and variable store.
    /// </summary>
    /// <returns>A <see cref="RunnerState"/> snapshot.</returns>
    public RunnerState State()
    {
        var (ints, bools, strings) = _variables.Snapshot();
        return new RunnerState(_currentNodeId, _finished, ints, bools, strings);
    }

    /// <summary>
    /// Restores the runner's position and variable values from a previously captured <see cref="RunnerState"/>.
    /// </summary>
    /// <param name="state">The state snapshot to restore.</param>
    /// <exception cref="DialogueException">Thrown if the state references an unknown node or incompatible variables.</exception>
    public void Restore(RunnerState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.CurrentNodeId is not null && !_graph.NodesById.ContainsKey(state.CurrentNodeId))
        {
            throw new DialogueException("unknown_node", $"Cannot restore unknown node '{state.CurrentNodeId}'.");
        }

        _variables.RestoreSnapshot(state.IntVariables, state.BoolVariables, state.StringVariables);
        _currentNodeId = state.CurrentNodeId;
        _finished = state.IsFinished;
    }

    private DialogueStep RunUntilInteractive(string startNodeId)
    {
        string nextId = startNodeId;
        int nonInteractiveSteps = 0;
        int maxSteps = _graph.Limits.MaxLoopSteps;

        while (true)
        {
            var node = _graph.NodesById[nextId];
            switch (node)
            {
                case SetNode setNode:
                    nonInteractiveSteps++;
                    if (nonInteractiveSteps >= maxSteps)
                    {
                        throw new DialogueException("loop_limit", $"Exceeded {maxSteps} consecutive non-interactive nodes.");
                    }
                    foreach (var assignment in setNode.Assignments)
                    {
                        var val = assignment.Expression.Evaluate(_variables);
                        _variables.SetUnchecked(assignment.TargetName, assignment.TargetType, val);
                    }
                    nextId = setNode.NextId;
                    break;

                case BranchNode branchNode:
                    nonInteractiveSteps++;
                    if (nonInteractiveSteps >= maxSteps)
                    {
                        throw new DialogueException("loop_limit", $"Exceeded {maxSteps} consecutive non-interactive nodes.");
                    }
                    bool taken = (bool)branchNode.Condition.Evaluate(_variables);
                    nextId = taken ? branchNode.ThenId : branchNode.ElseId;
                    break;

                case LineNode lineNode:
                    _currentNodeId = lineNode.Id;
                    var text = _localizer.Text(lineNode.TextKey, _variables);
                    return new DialogueStep(StepKind.Line, lineNode.Speaker, text, Array.Empty<string>());

                case ChoiceNode choiceNode:
                    _currentNodeId = choiceNode.Id;
                    var visible = GetVisibleOptions(choiceNode);
                    var choiceTexts = new List<string>(visible.Count);
                    foreach (var opt in visible)
                    {
                        choiceTexts.Add(_localizer.Text(opt.TextKey, _variables));
                    }
                    return new DialogueStep(StepKind.Choice, null, null, choiceTexts);

                case EndNode endNode:
                    _currentNodeId = endNode.Id;
                    _finished = true;
                    return new DialogueStep(StepKind.End, null, null, Array.Empty<string>());

                default:
                    throw new DialogueException("invalid_graph", $"Unsupported node type at '{nextId}'.");
            }
        }
    }

    private List<ChoiceOption> GetVisibleOptions(ChoiceNode choiceNode)
    {
        var visible = new List<ChoiceOption>(choiceNode.Options.Count);
        foreach (var opt in choiceNode.Options)
        {
            if (opt.Condition is null || (bool)opt.Condition.Evaluate(_variables))
            {
                visible.Add(opt);
            }
        }
        return visible;
    }
}
