using System.Numerics;
using YSMViewer.Models.AnimationController;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class AnimationStateMachine
{
    private readonly AnimationControllerEntry _controller;
    private readonly AnimationContext _context;
    private readonly List<AnimationSlot> _activeSlots = [];
    private readonly Dictionary<string, BoneBlendState> _blendStates = [];
    private string _currentState;
    private bool _isInitialized;

    private readonly Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> _boneStates = [];

    public string CurrentState => _currentState ?? "";
    public bool IsInitialized => _isInitialized;

    public AnimationStateMachine(
        AnimationControllerEntry controller,
        AnimationContext context)
    {
        _controller = controller;
        _context = context;
        _currentState = controller.InitialState ?? "default";
    }

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        foreach (var (boneName, bone) in _context.BoneNodes)
        {
            _boneStates[boneName] = (bone.Position, bone.RotationQuaternion, bone.Scale);
        }

        foreach (var (boneName, _) in _context.BoneNodes)
        {
            if (!_blendStates.ContainsKey(boneName))
                _blendStates[boneName] = new BoneBlendState(boneName);
        }

        var initialState = _currentState;
        if (!string.IsNullOrEmpty(initialState) &&
            _controller.States.TryGetValue(initialState, out var state))
        {
            TransitionToState(initialState, null);
        }
    }

    public void Process(float tick, float deltaTime, bool isMoving)
    {
        if (!_isInitialized) return;

        CaptureBoneStates();

        _context.DeltaTime = deltaTime;
        _context.AnimTime = tick / 20f;
        _context.IsMoving = isMoving;
        _context.Molang.SetAnimVariable("anim_time", tick / 20f);
        _context.Molang.SetAnimVariable("delta_time", deltaTime);

        EvaluateTransitions();

        foreach (var slot in _activeSlots)
        {
            slot.Process(_context, tick, _context.Molang, isMoving);
        }

        foreach (var blendState in _blendStates.Values)
            blendState.Reset();

        foreach (var slot in _activeSlots)
        {
            if (!slot.IsActive) continue;
            var queues = slot.Instance.GetActiveQueues();
            foreach (var queue in queues)
            {
                if (_blendStates.TryGetValue(queue.BoneName, out var blendState))
                    blendState.AddSource(queue);
            }
        }

        foreach (var (boneName, blendState) in _blendStates)
        {
            if (!_context.BasePositions.TryGetValue(boneName, out var basePos))
                basePos = Vector3.Zero;
            if (!_context.BaseEulers.TryGetValue(boneName, out var baseEuler))
                baseEuler = Vector3.Zero;

            blendState.Blend(basePos, baseEuler, _context.Molang);
        }
    }

    public void ForEachTransform(Action<string, Vector3, Quaternion, Vector3> applyTransform)
    {
        foreach (var (boneName, blendState) in _blendStates)
        {
            if (!blendState.HasActiveSources) continue;

            if (!_context.BoneNodes.TryGetValue(boneName, out var bone)) continue;

            applyTransform(boneName, blendState.BlendedPosition, blendState.BlendedRotation, blendState.BlendedScale);
        }
    }

    private void CaptureBoneStates()
    {
        foreach (var (boneName, bone) in _context.BoneNodes)
        {
            _boneStates[boneName] = (bone.Position, bone.RotationQuaternion, bone.Scale);
        }
    }

    private void EvaluateTransitions()
    {
        if (!_controller.States.TryGetValue(_currentState, out var state)) return;
        if (state.Transitions is null) return;

        foreach (var transition in state.Transitions)
        {
            foreach (var (targetState, condition) in transition)
            {
                float result = _context.Molang.EvaluateString(condition);
                if (result > 0.5f)
                {
                    TransitionToState(targetState, condition);
                    return;
                }
            }
        }
    }

    private void TransitionToState(string stateName, string? triggerCondition)
    {
        if (!_controller.States.TryGetValue(stateName, out var newState)) return;

        if (_currentState is not null &&
            _controller.States.TryGetValue(_currentState, out var oldState))
        {
            ExecuteScripts(oldState.OnExit);
        }

        float blendTransitionDuration = newState.BlendTransition;

        foreach (var slot in _activeSlots)
            slot.Instance.BeginEnd(0f);

        _activeSlots.Clear();

        var oldStateName = _currentState;
        _currentState = stateName;

        if (newState.Animations is not null)
        {
            foreach (var animEntry in newState.Animations)
            {
                var slotRef = AnimationSlotReference.Parse(animEntry);

                if (!_context.Animations.TryGetValue(slotRef.AnimationName, out var anim))
                    continue;

                var instance = new AnimationControllerInstance(anim, _context);
                var slot = new AnimationSlot(slotRef.AnimationName, instance, _context.Molang);

                slot.SetCondition(slotRef.ConditionExpression);

                var currentBoneStates = new Dictionary<string, (Vector3, Quaternion, Vector3)>();
                foreach (var (boneName, bone) in _context.BoneNodes)
                {
                    currentBoneStates[boneName] = (bone.Position, bone.RotationQuaternion, bone.Scale);
                }

                slot.Instance.BeginStart(blendTransitionDuration, 0f, currentBoneStates);
                _activeSlots.Add(slot);
            }
        }

        ExecuteScripts(newState.OnEntry);

        if (newState.SoundEffects is not null)
        {
            foreach (var sound in newState.SoundEffects)
            {
                _context.Molang.AudioHost?.PlaySound(sound);
            }
        }
    }

    private void ExecuteScripts(List<string>? scripts)
    {
        if (scripts is null) return;
        foreach (var script in scripts)
        {
            var expr = _context.Molang.Parse(script);
            _context.Molang.Evaluate(expr);
        }
    }
}