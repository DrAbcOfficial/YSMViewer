using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using ConcreteMC.MolangSharp.Runtime.Value;
using ConcreteMC.MolangSharp.Utils;

namespace YSMViewer.Services.Molang;

internal sealed class MolangVariableStruct(
    Dictionary<string, IMoValue> userVars,
    Dictionary<string, IMoValue> animVars,
    Action markDirty) : IMoStruct
{
    public object Value => this;

    public IMoValue Get(MoPath key, MoParams parameters)
    {
        if (key.HasChildren)
        {
            if (TryGetFlat(key.Value, out var parent) && parent is IMoStruct childStruct)
                return childStruct.Get(key.Next, parameters);
            return DoubleValue.Zero;
        }

        return TryGetFlat(key.Value, out var val) ? val : DoubleValue.Zero;
    }

    public void Set(MoPath key, IMoValue value)
    {
        if (key.HasChildren)
        {
            if (!userVars.TryGetValue(key.Value, out var parent) || parent is not IMoStruct childStruct)
            {
                childStruct = new VariableStruct();
                userVars[key.Value] = childStruct;
            }
            childStruct.Set(key.Next, value);
            markDirty();
            return;
        }

        userVars[key.Value] = value;
        markDirty();
    }

    public void Clear() { }

    private bool TryGetFlat(string name, out IMoValue value)
    {
        if (animVars.TryGetValue(name, out value!))
            return true;
        if (userVars.TryGetValue(name, out value!))
            return true;
        return false;
    }
}
