using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using ConcreteMC.MolangSharp.Runtime.Value;
using ConcreteMC.MolangSharp.Utils;

namespace YSMViewer.Services.Molang;

internal sealed class NullMolangStruct : IMoStruct
{
    public static NullMolangStruct Instance { get; } = new();

    public object Value => this;

    private NullMolangStruct() { }

    public IMoValue Get(MoPath key, MoParams parameters) => DoubleValue.Zero;

    public void Set(MoPath key, IMoValue value) { }

    public void Clear() { }
}
