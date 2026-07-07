namespace YSMViewer.Models.AnimationController;

/// <summary>
/// Loop-type codes accepted by <c>ctrl.set_animation</c> in Bedrock animation
/// controllers. These are the integer values the molang binding returns and
/// that <c>IAnimationStateMachineHost.SetAnimation</c> receives.
/// </summary>
public enum ControllerSetAnimationLoopType
{
    Loop = 10,
    PlayOnce = 11,
    HoldOnLastFrame = 12,
}

/// <summary>
/// State-action codes returned by the <c>ctrl.state_*</c> molang bindings.
/// Kept as a single source of truth for the magic integers that historically
/// were duplicated between <c>CtrlBindings</c> and the animation state machine.
/// </summary>
public enum ControllerStateAction
{
    Continue = 2,
    Stop = 3,
    Pause = 4,
    Bypass = 5,
}
