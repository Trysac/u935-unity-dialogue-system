namespace Trysac.UnityDialogueSystem;

internal enum VariableType
{
    Int,
    Bool,
    String,
}

internal sealed record VariableDeclaration(string Name, VariableType Type, object DefaultValue);

/// <summary>
/// Stores typed dialogue variables (<see cref="int"/>, <see cref="bool"/>, or <see cref="string"/>).
/// Only declared variables may be read or modified.
/// </summary>
public sealed class VariableStore
{
    private readonly Dictionary<string, VariableType> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new empty instance of the <see cref="VariableStore"/> class.
    /// </summary>
    public VariableStore()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableStore"/> class seeded with the defaults declared in a <see cref="DialogueGraph"/>.
    /// </summary>
    /// <param name="graph">The dialogue graph whose variable declarations and defaults will populate this store.</param>
    public VariableStore(DialogueGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        foreach (var decl in graph.VariableDeclarations.Values)
        {
            _types[decl.Name] = decl.Type;
            _values[decl.Name] = decl.DefaultValue;
        }
    }

    /// <summary>
    /// Declares or initializes an integer variable and sets its value.
    /// If the variable was already declared with a different type, throws <see cref="DialogueException"/> with code <c>type_mismatch</c>.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The integer value.</param>
    /// <returns>This <see cref="VariableStore"/> instance for fluent chaining.</returns>
    public VariableStore Declare(string name, int value)
    {
        ArgumentNullException.ThrowIfNull(name);
        DeclareInternal(name, VariableType.Int, value);
        return this;
    }

    /// <summary>
    /// Declares or initializes a boolean variable and sets its value.
    /// If the variable was already declared with a different type, throws <see cref="DialogueException"/> with code <c>type_mismatch</c>.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The boolean value.</param>
    /// <returns>This <see cref="VariableStore"/> instance for fluent chaining.</returns>
    public VariableStore Declare(string name, bool value)
    {
        ArgumentNullException.ThrowIfNull(name);
        DeclareInternal(name, VariableType.Bool, value);
        return this;
    }

    /// <summary>
    /// Declares or initializes a string variable and sets its value.
    /// If the variable was already declared with a different type, throws <see cref="DialogueException"/> with code <c>type_mismatch</c>.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The string value.</param>
    /// <returns>This <see cref="VariableStore"/> instance for fluent chaining.</returns>
    public VariableStore Declare(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        DeclareInternal(name, VariableType.String, value);
        return this;
    }

    /// <summary>
    /// Sets the value of a declared integer variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The integer value.</param>
    /// <exception cref="DialogueException">Thrown with <c>unknown_variable</c> if not declared, or <c>type_mismatch</c> if not an int.</exception>
    public void Set(string name, int value)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureDeclaredAndType(name, VariableType.Int);
        _values[name] = value;
    }

    /// <summary>
    /// Sets the value of a declared boolean variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The boolean value.</param>
    /// <exception cref="DialogueException">Thrown with <c>unknown_variable</c> if not declared, or <c>type_mismatch</c> if not a bool.</exception>
    public void Set(string name, bool value)
    {
        ArgumentNullException.ThrowIfNull(name);
        EnsureDeclaredAndType(name, VariableType.Bool);
        _values[name] = value;
    }

    /// <summary>
    /// Sets the value of a declared string variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The string value.</param>
    /// <exception cref="DialogueException">Thrown with <c>unknown_variable</c> if not declared, or <c>type_mismatch</c> if not a string.</exception>
    public void Set(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        EnsureDeclaredAndType(name, VariableType.String);
        _values[name] = value;
    }

    /// <summary>
    /// Gets the current value of a declared variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value (<see cref="int"/>, <see cref="bool"/>, or <see cref="string"/>).</returns>
    /// <exception cref="DialogueException">Thrown with <c>unknown_variable</c> if the variable is not declared.</exception>
    public object Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_values.TryGetValue(name, out var value))
        {
            throw new DialogueException("unknown_variable", $"Unknown variable '{name}'.");
        }

        return value;
    }

    internal bool TryGetValue(string name, out object? value) => _values.TryGetValue(name, out value);

    internal bool TryGetType(string name, out VariableType type) => _types.TryGetValue(name, out type);

    internal void SeedFromGraph(DialogueGraph graph)
    {
        foreach (var decl in graph.VariableDeclarations.Values)
        {
            if (_types.TryGetValue(decl.Name, out var existingType))
            {
                if (existingType != decl.Type)
                {
                    throw new DialogueException("type_mismatch", $"Variable '{decl.Name}' has type '{existingType}' in store but '{decl.Type}' in graph.");
                }
            }
            else
            {
                _types[decl.Name] = decl.Type;
                _values[decl.Name] = decl.DefaultValue;
            }
        }
    }

    internal void SetUnchecked(string name, VariableType type, object value)
    {
        EnsureDeclaredAndType(name, type);
        _values[name] = value;
    }

    internal (Dictionary<string, int> Ints, Dictionary<string, bool> Bools, Dictionary<string, string> Strings) Snapshot()
    {
        var ints = new Dictionary<string, int>(StringComparer.Ordinal);
        var bools = new Dictionary<string, bool>(StringComparer.Ordinal);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (name, type) in _types)
        {
            var val = _values[name];
            switch (type)
            {
                case VariableType.Int:
                    ints[name] = (int)val;
                    break;
                case VariableType.Bool:
                    bools[name] = (bool)val;
                    break;
                case VariableType.String:
                    strings[name] = (string)val;
                    break;
            }
        }

        return (ints, bools, strings);
    }

    internal void RestoreSnapshot(
        IReadOnlyDictionary<string, int> ints,
        IReadOnlyDictionary<string, bool> bools,
        IReadOnlyDictionary<string, string> strings)
    {
        foreach (var (k, v) in ints)
        {
            Set(k, v);
        }

        foreach (var (k, v) in bools)
        {
            Set(k, v);
        }

        foreach (var (k, v) in strings)
        {
            Set(k, v);
        }
    }

    private void DeclareInternal(string name, VariableType type, object value)
    {
        if (_types.TryGetValue(name, out var existing) && existing != type)
        {
            throw new DialogueException("type_mismatch", $"Variable '{name}' is already declared with a different type.");
        }

        _types[name] = type;
        _values[name] = value;
    }

    private void EnsureDeclaredAndType(string name, VariableType expected)
    {
        if (!_types.TryGetValue(name, out var actual))
        {
            throw new DialogueException("unknown_variable", $"Unknown variable '{name}'.");
        }

        if (actual != expected)
        {
            throw new DialogueException("type_mismatch", $"Variable '{name}' expects type '{actual}', not '{expected}'.");
        }
    }
}
