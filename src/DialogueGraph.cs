using System.Text;
using System.Text.Json;

namespace Trysac.UnityDialogueSystem;

internal abstract record GraphNode(string Id);

internal sealed record LineNode(string Id, string Speaker, string TextKey, string NextId) : GraphNode(Id);

internal sealed record ChoiceOption(string TextKey, ExprNode? Condition, string NextId);

internal sealed record ChoiceNode(string Id, IReadOnlyList<ChoiceOption> Options) : GraphNode(Id);

internal sealed record SetNode(string Id, IReadOnlyList<CompiledAssignment> Assignments, string NextId) : GraphNode(Id);

internal sealed record BranchNode(string Id, ExprNode Condition, string ThenId, string ElseId) : GraphNode(Id);

internal sealed record EndNode(string Id) : GraphNode(Id);

/// <summary>
/// An immutable, validated dialogue graph loaded from JSON.
/// </summary>
public sealed class DialogueGraph
{
    internal Dictionary<string, VariableDeclaration> VariableDeclarations { get; }
    internal Dictionary<string, GraphNode> NodesById { get; }
    internal DialogueLimits Limits { get; }

    /// <summary>
    /// Gets the ordered list of node IDs in the graph.
    /// </summary>
    public IReadOnlyList<string> NodeIds { get; }

    /// <summary>
    /// Gets the ID of the starting node.
    /// </summary>
    public string StartNode { get; }

    private DialogueGraph(
        string startNode,
        IReadOnlyList<string> nodeIds,
        Dictionary<string, VariableDeclaration> variableDeclarations,
        Dictionary<string, GraphNode> nodesById,
        DialogueLimits limits)
    {
        StartNode = startNode;
        NodeIds = nodeIds;
        VariableDeclarations = variableDeclarations;
        NodesById = nodesById;
        Limits = limits;
    }

    /// <summary>
    /// Parses and validates a dialogue graph from a JSON string.
    /// Validates all node links, variable declarations, conditions, and assignment expressions up front.
    /// </summary>
    /// <param name="json">The JSON string representing the dialogue graph.</param>
    /// <param name="limits">Optional custom size and nesting limits.</param>
    /// <returns>The validated <see cref="DialogueGraph"/>.</returns>
    /// <exception cref="DialogueException">Thrown when the JSON or graph structure is invalid.</exception>
    public static DialogueGraph Load(string json, DialogueLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        var effectiveLimits = limits ?? DialogueLimits.Default;

        int byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > effectiveLimits.MaxJsonBytes)
        {
            throw new DialogueException("input_too_large", $"Dialogue JSON size ({byteCount} bytes) exceeds limit of {effectiveLimits.MaxJsonBytes} bytes.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException ex)
        {
            throw new DialogueException("invalid_graph", "Failed to parse dialogue JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new DialogueException("invalid_graph", "Root JSON element must be an object.");
            }

            var startNode = GetRequiredString(root, "start");
            var variables = ParseVariables(root);
            var (nodeIds, nodesById) = ParseNodes(root, variables, effectiveLimits);

            ValidateReferences(startNode, nodesById);

            return new DialogueGraph(startNode, nodeIds, variables, nodesById, effectiveLimits);
        }
    }

    private static Dictionary<string, VariableDeclaration> ParseVariables(JsonElement root)
    {
        var result = new Dictionary<string, VariableDeclaration>(StringComparer.Ordinal);
        if (!root.TryGetProperty("variables", out var varsElem))
        {
            return result;
        }

        if (varsElem.ValueKind != JsonValueKind.Object)
        {
            throw new DialogueException("invalid_graph", "'variables' property must be a JSON object.");
        }

        foreach (var prop in varsElem.EnumerateObject())
        {
            var name = prop.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DialogueException("invalid_graph", "Variable name cannot be empty.");
            }

            var def = prop.Value;
            if (def.ValueKind != JsonValueKind.Object)
            {
                throw new DialogueException("invalid_graph", $"Variable definition for '{name}' must be an object.");
            }

            var typeStr = GetRequiredString(def, "type");
            if (!def.TryGetProperty("default", out var defaultElem))
            {
                throw new DialogueException("invalid_graph", $"Variable '{name}' is missing a 'default' value.");
            }

            switch (typeStr)
            {
                case "int":
                    if (defaultElem.ValueKind != JsonValueKind.Number || !defaultElem.TryGetInt32(out int intVal))
                    {
                        throw new DialogueException("type_mismatch", $"Default value for int variable '{name}' must be a 32-bit integer.");
                    }
                    result[name] = new VariableDeclaration(name, VariableType.Int, intVal);
                    break;

                case "bool":
                    if (defaultElem.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        throw new DialogueException("type_mismatch", $"Default value for bool variable '{name}' must be a boolean.");
                    }
                    result[name] = new VariableDeclaration(name, VariableType.Bool, defaultElem.GetBoolean());
                    break;

                case "string":
                    if (defaultElem.ValueKind != JsonValueKind.String)
                    {
                        throw new DialogueException("type_mismatch", $"Default value for string variable '{name}' must be a string.");
                    }
                    result[name] = new VariableDeclaration(name, VariableType.String, defaultElem.GetString()!);
                    break;

                default:
                    throw new DialogueException("invalid_graph", $"Unsupported variable type '{typeStr}' for variable '{name}'.");
            }
        }

        return result;
    }

    private static (List<string> NodeIds, Dictionary<string, GraphNode> NodesById) ParseNodes(
        JsonElement root,
        Dictionary<string, VariableDeclaration> variables,
        DialogueLimits limits)
    {
        if (!root.TryGetProperty("nodes", out var nodesElem) || nodesElem.ValueKind != JsonValueKind.Array)
        {
            throw new DialogueException("invalid_graph", "Graph must contain a 'nodes' array.");
        }

        int count = nodesElem.GetArrayLength();
        if (count == 0)
        {
            throw new DialogueException("invalid_graph", "Graph 'nodes' array cannot be empty.");
        }

        if (count > limits.MaxNodes)
        {
            throw new DialogueException("input_too_large", $"Node count ({count}) exceeds maximum of {limits.MaxNodes}.");
        }

        VariableType ResolveVar(string varName)
        {
            if (!variables.TryGetValue(varName, out var decl))
            {
                throw new DialogueException("unknown_variable", $"Unknown variable '{varName}'.");
            }
            return decl.Type;
        }

        var nodeIds = new List<string>(count);
        var nodesById = new Dictionary<string, GraphNode>(count, StringComparer.Ordinal);

        foreach (var nodeElem in nodesElem.EnumerateArray())
        {
            if (nodeElem.ValueKind != JsonValueKind.Object)
            {
                throw new DialogueException("invalid_graph", "Each node in 'nodes' must be an object.");
            }

            var id = GetRequiredString(nodeElem, "id");
            if (nodesById.ContainsKey(id))
            {
                throw new DialogueException("duplicate_node", $"Duplicate node id '{id}'.");
            }

            var kind = GetRequiredString(nodeElem, "kind");
            GraphNode parsedNode = kind switch
            {
                "line" => ParseLineNode(id, nodeElem),
                "choice" => ParseChoiceNode(id, nodeElem, ResolveVar, limits),
                "set" => ParseSetNode(id, nodeElem, ResolveVar, limits),
                "branch" => ParseBranchNode(id, nodeElem, ResolveVar, limits),
                "end" => new EndNode(id),
                _ => throw new DialogueException("invalid_graph", $"Unknown node kind '{kind}' on node '{id}'."),
            };

            nodeIds.Add(id);
            nodesById[id] = parsedNode;
        }

        return (nodeIds, nodesById);
    }

    private static LineNode ParseLineNode(string id, JsonElement elem)
    {
        var speaker = GetRequiredString(elem, "speaker");
        var textKey = GetRequiredString(elem, "text");
        var nextId = GetRequiredString(elem, "next");
        return new LineNode(id, speaker, textKey, nextId);
    }

    private static ChoiceNode ParseChoiceNode(
        string id,
        JsonElement elem,
        Func<string, VariableType> resolveVar,
        DialogueLimits limits)
    {
        if (!elem.TryGetProperty("options", out var optsElem) || optsElem.ValueKind != JsonValueKind.Array)
        {
            throw new DialogueException("invalid_graph", $"Choice node '{id}' must have an 'options' array.");
        }

        int optCount = optsElem.GetArrayLength();
        if (optCount == 0)
        {
            throw new DialogueException("invalid_graph", $"Choice node '{id}' must have at least one option.");
        }

        if (optCount > limits.MaxOptionsPerChoice)
        {
            throw new DialogueException("input_too_large", $"Choice node '{id}' has {optCount} options, exceeding limit of {limits.MaxOptionsPerChoice}.");
        }

        var options = new List<ChoiceOption>(optCount);
        foreach (var optElem in optsElem.EnumerateArray())
        {
            if (optElem.ValueKind != JsonValueKind.Object)
            {
                throw new DialogueException("invalid_graph", $"Option in choice node '{id}' must be an object.");
            }

            var textKey = GetRequiredString(optElem, "text");
            var nextId = GetRequiredString(optElem, "next");
            ExprNode? condExpr = null;

            if (optElem.TryGetProperty("condition", out var condElem) && condElem.ValueKind != JsonValueKind.Null)
            {
                if (condElem.ValueKind != JsonValueKind.String)
                {
                    throw new DialogueException("invalid_graph", $"Option condition in node '{id}' must be a string.");
                }
                condExpr = CompileCondition(condElem.GetString()!, resolveVar, limits);
            }

            options.Add(new ChoiceOption(textKey, condExpr, nextId));
        }

        return new ChoiceNode(id, options);
    }

    private static SetNode ParseSetNode(
        string id,
        JsonElement elem,
        Func<string, VariableType> resolveVar,
        DialogueLimits limits)
    {
        if (!elem.TryGetProperty("assign", out var assignElem) || assignElem.ValueKind != JsonValueKind.Array)
        {
            throw new DialogueException("invalid_graph", $"Set node '{id}' must have an 'assign' array.");
        }

        int count = assignElem.GetArrayLength();
        if (count == 0)
        {
            throw new DialogueException("invalid_graph", $"Set node '{id}' must have at least one assignment.");
        }

        var assignments = new List<CompiledAssignment>(count);
        foreach (var item in assignElem.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new DialogueException("invalid_graph", $"Assignment in set node '{id}' must be a string.");
            }

            assignments.Add(ExpressionParser.ParseAssignment(item.GetString()!, limits, resolveVar));
        }

        var nextId = GetRequiredString(elem, "next");
        return new SetNode(id, assignments, nextId);
    }

    private static BranchNode ParseBranchNode(
        string id,
        JsonElement elem,
        Func<string, VariableType> resolveVar,
        DialogueLimits limits)
    {
        var condStr = GetRequiredString(elem, "condition");
        var condExpr = CompileCondition(condStr, resolveVar, limits);
        var thenId = GetRequiredString(elem, "then");
        var elseId = GetRequiredString(elem, "else");
        return new BranchNode(id, condExpr, thenId, elseId);
    }

    private static ExprNode CompileCondition(
        string source,
        Func<string, VariableType> resolveVar,
        DialogueLimits limits)
    {
        var expr = ExpressionParser.Parse(source, limits);
        var type = expr.CheckType(resolveVar);
        if (type != VariableType.Bool)
        {
            throw new DialogueException("type_mismatch", $"Condition '{source}' must evaluate to bool, but evaluates to '{type}'.");
        }
        return expr;
    }

    private static void ValidateReferences(string startNode, Dictionary<string, GraphNode> nodesById)
    {
        EnsureNodeExists(startNode, nodesById, "start");

        foreach (var node in nodesById.Values)
        {
            switch (node)
            {
                case LineNode line:
                    EnsureNodeExists(line.NextId, nodesById, line.Id);
                    break;
                case ChoiceNode choice:
                    foreach (var opt in choice.Options)
                    {
                        EnsureNodeExists(opt.NextId, nodesById, choice.Id);
                    }
                    break;
                case SetNode set:
                    EnsureNodeExists(set.NextId, nodesById, set.Id);
                    break;
                case BranchNode branch:
                    EnsureNodeExists(branch.ThenId, nodesById, branch.Id);
                    EnsureNodeExists(branch.ElseId, nodesById, branch.Id);
                    break;
            }
        }
    }

    private static void EnsureNodeExists(string targetId, Dictionary<string, GraphNode> nodesById, string fromContext)
    {
        if (!nodesById.ContainsKey(targetId))
        {
            throw new DialogueException("unknown_node", $"Node '{fromContext}' references unknown node '{targetId}'.");
        }
    }

    private static string GetRequiredString(JsonElement obj, string propName)
    {
        if (!obj.TryGetProperty(propName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            throw new DialogueException("invalid_graph", $"Missing or non-string required property '{propName}'.");
        }

        var val = prop.GetString();
        if (string.IsNullOrEmpty(val))
        {
            throw new DialogueException("invalid_graph", $"Property '{propName}' cannot be empty.");
        }

        return val;
    }
}
