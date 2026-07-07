using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;
using YSMViewer.Models.AnimationController;

namespace YSMViewer.Services.Molang;

internal static class CtrlBindings
{
    public static QueryStruct CreateCtrlStruct(MolangService service)
    {
        var functions = new Dictionary<string, Func<MoParams, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["loop"] = _ => (object)(int)ControllerSetAnimationLoopType.Loop,
            ["play_once"] = _ => (object)(int)ControllerSetAnimationLoopType.PlayOnce,
            ["hold_on_last_frame"] = _ => (object)(int)ControllerSetAnimationLoopType.HoldOnLastFrame,
            ["state_continue"] = _ => (object)(int)ControllerStateAction.Continue,
            ["state_stop"] = _ => (object)(int)ControllerStateAction.Stop,
            ["state_pause"] = _ => (object)(int)ControllerStateAction.Pause,
            ["state_bypass"] = _ => (object)(int)ControllerStateAction.Bypass,
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
            ["indicate_reload"] = _ =>
            {
                service.StateMachineHost?.IndicateReload();
                return 0.0;
            },
        };

        return new QueryStruct(functions);
    }
}