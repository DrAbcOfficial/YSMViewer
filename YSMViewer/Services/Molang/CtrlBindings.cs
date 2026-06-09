using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;

namespace YSMViewer.Services.Molang;

internal static class CtrlBindings
{
    public static QueryStruct CreateCtrlStruct(MolangService service)
    {
        var functions = new Dictionary<string, Func<MoParams, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["loop"] = _ => (object)10,
            ["play_once"] = _ => (object)11,
            ["hold_on_last_frame"] = _ => (object)12,
            ["state_continue"] = _ => (object)2,
            ["state_stop"] = _ => (object)3,
            ["state_pause"] = _ => (object)4,
            ["state_bypass"] = _ => (object)5,
            ["set_animation"] = p =>
            {
                service.StateMachineHost?.SetAnimation(p.GetString(0), (int)p.GetDouble(1));
                return 0.0;
            },
            ["set_beginning_transition_length"] = p =>
            {
                service.StateMachineHost?.SetTransitionLength((float)p.GetDouble(0));
                return 0.0;
            },
            ["reset"] = _ =>
            {
                service.StateMachineHost?.Reset();
                return 0.0;
            },
        };

        return new QueryStruct(functions);
    }
}