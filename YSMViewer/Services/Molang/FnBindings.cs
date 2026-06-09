using System;
using System.Collections.Generic;
using System.Text;
using ConcreteMC.MolangSharp.Parser;
using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using ConcreteMC.MolangSharp.Runtime.Value;
using ConcreteMC.MolangSharp.Utils;

namespace YSMViewer.Services.Molang;

internal sealed class LazyFunctionStruct : IMoStruct
{
    private readonly MolangService _service;
    private readonly Dictionary<string, IExpression?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _functionSources = new(StringComparer.OrdinalIgnoreCase);

    public LazyFunctionStruct(MolangService service)
    {
        _service = service;
    }

    public void RegisterFunction(string name, byte[] source)
    {
        _functionSources[name] = source;
    }

    public IMoValue Get(MoPath path, MoParams parameters)
    {
        var name = path.Value;
        if (!_cache.TryGetValue(name, out var expr) || expr is null)
        {
            if (!_functionSources.TryGetValue(name, out var source))
                return DoubleValue.Zero;

            var sourceText = Encoding.UTF8.GetString(source);
            expr = _service.Parse(sourceText);
            _cache[name] = expr;
        }

        var context = _service.BuildContext();
        var result = _service.Evaluate(expr);
        return new DoubleValue(result);
    }

    public void Set(MoPath key, IMoValue value) { }

    public void Clear() => _cache.Clear();

    public object Value => _functionSources;
}

internal static class FnBindings
{
    public static LazyFunctionStruct CreateFnStruct(MolangService service)
    {
        return new LazyFunctionStruct(service);
    }
}