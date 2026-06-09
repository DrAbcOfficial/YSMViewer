using System;
using System.Collections.Generic;
using ConcreteMC.MolangSharp.Parser;
using ConcreteMC.MolangSharp.Parser.Exceptions;
using ConcreteMC.MolangSharp.Parser.Expressions;
using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using ConcreteMC.MolangSharp.Runtime.Value;

namespace YSMViewer.Services.Molang;

public interface IAnimationStateMachineHost
{
    void SetAnimation(string name, int loopType);
    void SetTransitionLength(float seconds);
    void Reset();
}

public interface IAnimationAudioHost
{
    void PlaySound(string soundName);
    void StopSound(string soundName);
}

public sealed class MolangService
{
    private readonly MoLangRuntime _runtime;
    private readonly Dictionary<string, IMoValue> _userVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMoValue> _animVariables = new(StringComparer.OrdinalIgnoreCase);

    public IAnimationStateMachineHost? StateMachineHost { get; set; }
    public IAnimationAudioHost? AudioHost { get; set; }
    public IReadOnlyDictionary<string, IAnimatableBone>? BoneNodes { get; set; }
    public PhysicsSimulator Physics { get; } = new();

    private readonly LazyFunctionStruct _fnStruct;

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

        _runtime = new MoLangRuntime(env);

        MoLangParser.Factory = iterator =>
        {
            var parser = new MoLangParser(iterator);
            return parser;
        };
    }

    public void SetUserVariable(string name, float value)
    {
        _userVariables[name] = new DoubleValue(value);
    }

    public IReadOnlyDictionary<string, IMoValue> UserVariables => _userVariables;

    public void SetAnimVariable(string name, float value)
    {
        _animVariables[name] = new DoubleValue(value);
    }

    public void RegisterFunction(string name, byte[] data)
    {
        _fnStruct.RegisterFunction(name, data);
    }

    public IExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return new NumberExpression(0.0);

        try
        {
            return MoLangParser.Parse(expression);
        }
        catch (MoLangParserException)
        {
            return new NumberExpression(0.0);
        }
    }

    public float Evaluate(IExpression expr)
    {
        if (expr is NumberExpression num)
            return (float)num.Evaluate(null!, _runtime.Environment).AsDouble();

        var context = BuildContext();
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

    internal Dictionary<string, IMoValue> BuildContext()
    {
        var ctx = new Dictionary<string, IMoValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _userVariables)
            ctx[kv.Key] = kv.Value;
        foreach (var kv in _animVariables)
            ctx[kv.Key] = kv.Value;
        return ctx;
    }

    public void ResetFrame()
    {
        _animVariables.Clear();
        Physics.UpdateAll();
    }

    internal double SafeGetUserVar(string name, double defaultValue = 0.0)
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
}