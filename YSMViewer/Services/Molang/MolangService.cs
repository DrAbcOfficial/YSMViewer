using ConcreteMC.MolangSharp.Parser;
using ConcreteMC.MolangSharp.Parser.Exceptions;
using ConcreteMC.MolangSharp.Parser.Expressions;
using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Value;

namespace YSMViewer.Services.Molang;

public interface IAnimationStateMachineHost
{
    void SetAnimation(string name, int loopType);
    void SetTransitionLength(float seconds);
    void Reset();
    void IndicateReload();
}

public interface IAnimationAudioHost
{
    void PlaySound(string soundName);
    void StopSound(string soundName);
    void StopAllSounds();
    void SetVolume(float volume);
    void SetMuted(bool muted);
}

public sealed class MolangService
{
    private readonly MoLangRuntime _runtime;
    private readonly Dictionary<string, IMoValue> _userVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMoValue> _animVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IExpression> _parseCache = new(StringComparer.Ordinal);
    private Dictionary<string, IMoValue>? _cachedContext;
    private bool _contextDirty = true;

    public IAnimationStateMachineHost? StateMachineHost { get; set; }
    public IAnimationAudioHost? AudioHost { get; set; }
    public IReadOnlyDictionary<string, IAnimatableBone>? BoneNodes { get; set; }
    public PhysicsSimulator Physics { get; } = new();

    private readonly LazyFunctionStruct _fnStruct;
    private readonly MolangTempStruct _tempStruct = new();
    private readonly MolangContextStruct _contextStruct = new();

    public MolangService()
    {
        var env = new MoLangEnvironment();
        env.Structs["math"] = FixedMoLangMath.Library;
        env.Structs["query"] = QueryBindings.CreateQueryStruct(this);
        env.Structs["q"] = env.Structs["query"];
        env.Structs["ysm"] = YsmBindings.CreateYsmStruct(this);
        env.Structs["ctrl"] = CtrlBindings.CreateCtrlStruct(this);

        _fnStruct = FnBindings.CreateFnStruct(this);
        env.Structs["fn"] = _fnStruct;

        env.Structs["variable"] = new MolangVariableStruct(_userVariables, _animVariables);
        env.Structs["temp"] = _tempStruct;
        env.Structs["context"] = _contextStruct;
        env.Structs["c"] = _contextStruct;

        _runtime = new MoLangRuntime(env);

        MoLangParser.Factory = iterator =>
        {
            var parser = new MoLangParser(iterator);
            return parser;
        };
    }

    public void SetUserVariable(string name, float value)
    {
        _userVariables[StripPrefix(name)] = new DoubleValue(value);
        _contextDirty = true;
    }

    public IReadOnlyDictionary<string, IMoValue> UserVariables => _userVariables;

    public void SetAnimVariable(string name, float value)
    {
        _animVariables[StripPrefix(name)] = new DoubleValue(value);
        _contextDirty = true;
    }

    public void RegisterFunction(string name, byte[] data)
    {
        _fnStruct.RegisterFunction(name, data);
    }

    public IExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new NumberExpression(0.0);

        if (_parseCache.TryGetValue(expression, out var cached))
            return cached;

        try
        {
            var expr = MoLangParser.Parse(expression);
            _parseCache[expression] = expr;
            return expr;
        }
        catch (MoLangParserException)
        {
            _parseCache[expression] = new NumberExpression(0.0);
            return new NumberExpression(0.0);
        }
    }

    public float Evaluate(IExpression expr)
    {
        if (expr is NumberExpression num)
            return (float)num.Evaluate(null!, _runtime.Environment).AsDouble();

        var context = GetContext();
        var result = _runtime.Execute(expr, context);
        return (float)result.AsDouble();
    }

    public float EvaluateString(string expression)
    {
        if (float.TryParse(expression,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float f))
            return f;

        var expr = Parse(expression);
        return Evaluate(expr);
    }

    internal Dictionary<string, IMoValue> BuildContext() => GetContext();

    private Dictionary<string, IMoValue> GetContext()
    {
        if (_contextDirty)
        {
            _cachedContext ??= new Dictionary<string, IMoValue>(StringComparer.OrdinalIgnoreCase);
            _cachedContext.Clear();
            foreach (var kv in _userVariables)
                _cachedContext[kv.Key] = kv.Value;
            foreach (var kv in _animVariables)
                _cachedContext[kv.Key] = kv.Value;
            _contextDirty = false;
        }
        return _cachedContext!;
    }

    public void ResetFrame()
    {
        _animVariables.Clear();
        _tempStruct.Clear();
        _contextStruct.Clear();
        _contextDirty = true;
        PhysicsSimulator.UpdateAll();
    }

    public double SafeGetUserVar(string name, double defaultValue = 0.0)
    {
        if (_userVariables.TryGetValue(name, out var v))
            return v.AsDouble();
        return defaultValue;
    }

    internal double SafeGetUserOrAnimVar(string name, double defaultValue = 0.0)
    {
        if (_animVariables.TryGetValue(name, out var v))
            return v.AsDouble();
        if (_userVariables.TryGetValue(name, out var v2))
            return v2.AsDouble();
        return defaultValue;
    }

    private static string StripPrefix(string name)
    {
        var dotIdx = name.IndexOf('.');
        if (dotIdx <= 0) return name;

        var prefix = name.AsSpan(0, dotIdx);
        if (prefix.Equals("query", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("q", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("variable", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("v", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("context", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("c", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
            prefix.Equals("t", StringComparison.OrdinalIgnoreCase))
        {
            return name[(dotIdx + 1)..];
        }

        return name;
    }
}