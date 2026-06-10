using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using ConcreteMC.MolangSharp.Runtime.Value;
using ConcreteMC.MolangSharp.Utils;

namespace YSMViewer.Services.Molang;

internal sealed class MolangContextStruct : IMoStruct
{
    private readonly Dictionary<string, IMoValue> _vars = new(StringComparer.OrdinalIgnoreCase);

    public object Value => this;

    public IMoValue Get(MoPath key, MoParams parameters)
    {
        if (key.HasChildren)
        {
            if (_vars.TryGetValue(key.Value, out var parent) && parent is IMoStruct childStruct)
                return childStruct.Get(key.Next, parameters);
            return DoubleValue.Zero;
        }

        return _vars.TryGetValue(key.Value, out var val) ? val : DoubleValue.Zero;
    }

    public void Set(MoPath key, IMoValue value)
    {
        if (key.HasChildren)
        {
            if (!_vars.TryGetValue(key.Value, out var container) || container is not IMoStruct childStruct)
            {
                childStruct = new VariableStruct();
                _vars[key.Value] = childStruct;
            }
            childStruct.Set(key.Next, value);
            return;
        }

        _vars[key.Value] = value;
    }

    public void Clear() => _vars.Clear();
}