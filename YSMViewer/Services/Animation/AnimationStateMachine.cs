using ConcreteMC.MolangSharp.Parser;
using System.Numerics;
using YSMViewer.Models.AnimationController;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class AnimationStateMachine(
    AnimationControllerEntry controller,
    AnimationContext context) : IAnimationStateMachineHost
{
    private const int MaxTransitionIterations = 8;

    private readonly AnimationControllerEntry _controller = controller;
    private readonly AnimationContext _context = context;
    private readonly List<AnimationSlot> _activeSlots = [];
    private readonly List<AnimationSlot> _fadingSlots = [];
    private readonly Dictionary<string, BoneBlendState> _blendStates = [];
    private string _currentState = controller.InitialState ?? "default";
    private bool _isInitialized;
    private float _currentTick;

    private readonly Dictionary<string, IExpression> _conditionCache = [];
    private readonly HashSet<string> _visitedStates = [];

    public string CurrentState => _currentState ?? "";
    public bool IsInitialized => _isInitialized;

    public void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        foreach (var (boneName, _) in _context.BoneNodes)
        {
            if (!_blendStates.ContainsKey(boneName))
                _blendStates[boneName] = new BoneBlendState(boneName);
        }

        PrecacheConditions();

        var initialState = _currentState;
        if (!string.IsNullOrEmpty(initialState) &&
            _controller.States.TryGetValue(initialState, out _))
        {
            TransitionToState(initialState, 0f);
        }
    }

    private void PrecacheConditions()
    {
        foreach (var (_, state) in _controller.States)
        {
            if (state.Transitions is null) continue;
            foreach (var transition in state.Transitions)
            {
                foreach (var (_, condition) in transition)
                {
                    if (!string.IsNullOrEmpty(condition) && !_conditionCache.ContainsKey(condition))
                        _conditionCache[condition] = _context.Molang.Parse(condition);
                }
            }
        }
    }

    public void Process(float tick, float deltaTime, bool isMoving)
    {
        if (!_isInitialized) return;

        _currentTick = tick;

        _context.DeltaTime = deltaTime;
        _context.AnimTime = tick;
        _context.IsMoving = isMoving;
        _context.Molang.SetAnimVariable("anim_time", tick);
        _context.Molang.SetAnimVariable("delta_time", deltaTime);

        EvaluateTransitions();

        foreach (var slot in _activeSlots)
            slot.Process(_context, tick, _context.Molang, isMoving);

        for (int i = _fadingSlots.Count - 1; i >= 0; i--)
        {
            _fadingSlots[i].Process(_context, tick, _context.Molang, isMoving);
            if (!_fadingSlots[i].Instance.IsRunning)
                _fadingSlots.RemoveAt(i);
        }

        foreach (var blendState in _blendStates.Values)
            blendState.Reset();

        foreach (var slot in _activeSlots)
        {
            if (!slot.IsActive) continue;
            foreach (var queue in slot.Instance.GetActiveQueues())
            {
                if (_blendStates.TryGetValue(queue.BoneName, out var blendState))
                    blendState.AddSource(queue);
            }
        }

        foreach (var slot in _fadingSlots)
        {
            if (!slot.IsActive) continue;
            foreach (var queue in slot.Instance.GetActiveQueues())
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
            if (!_context.BoneNodes.TryGetValue(boneName, out _)) continue;

            applyTransform(boneName, blendState.BlendedPosition, blendState.BlendedRotation, blendState.BlendedScale);
        }
    }

    public bool GetBoneVisibility(string boneName)
    {
        if (_blendStates.TryGetValue(boneName, out var blendState))
        {
            if (blendState.IsVisibilityControlled)
                return blendState.VisibilityValue;
        }
        return true;
    }

    private void EvaluateTransitions()
    {
        int iterations = MaxTransitionIterations;
        _visitedStates.Clear();
        _visitedStates.Add(_currentState);

        while (iterations-- > 0)
        {
            if (!_controller.States.TryGetValue(_currentState, out var state)) return;
            if (state.Transitions is null) return;

            bool fired = false;
            foreach (var transition in state.Transitions)
            {
                foreach (var (targetState, condition) in transition)
                {
                    float result;
                    if (_conditionCache.TryGetValue(condition, out var cachedExpr))
                        result = _context.Molang.Evaluate(cachedExpr);
                    else
                        result = _context.Molang.EvaluateString(condition);

                    if (result > 0f)
                    {
                        if (_visitedStates.Contains(targetState))
                            return;

                        _visitedStates.Add(targetState);
                        TransitionToState(targetState, _currentTick);
                        fired = true;
                        break;
                    }
                }
                if (fired) break;
            }
            if (!fired) return;

            if (_activeSlots.Count == 0 && _fadingSlots.Count == 0) return;
        }
    }

    private void TransitionToState(string stateName, float currentTick)
    {
        if (!_controller.States.TryGetValue(stateName, out var newState)) return;

        if (_currentState is not null &&
            _controller.States.TryGetValue(_currentState, out var oldState))
        {
            ExecuteScripts(oldState.OnExit);
        }

        float blendTransitionDuration = newState.BlendTransition;

        foreach (var slot in _activeSlots)
            slot.Instance.BeginEnd(currentTick);
        _fadingSlots.AddRange(_activeSlots);
        _activeSlots.Clear();

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
                    currentBoneStates[boneName] = (bone.Position, bone.RotationQuaternion, bone.Scale);

                slot.Instance.BeginStart(blendTransitionDuration, currentTick, currentBoneStates);
                _activeSlots.Add(slot);
            }
        }

        ExecuteScripts(newState.OnEntry);

        if (newState.SoundEffects is not null)
        {
            foreach (var sound in newState.SoundEffects)
            {
                if (!string.IsNullOrWhiteSpace(sound))
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

    public void SetAnimation(string name, int loopType)
    {
        if (_context.Animations.TryGetValue(name, out var anim))
        {
            var currentBoneStates = new Dictionary<string, (Vector3, Quaternion, Vector3)>();
            foreach (var (boneName, bone) in _context.BoneNodes)
                currentBoneStates[boneName] = (bone.Position, bone.RotationQuaternion, bone.Scale);

            var instance = new AnimationControllerInstance(anim, _context);
            var slot = new AnimationSlot(name, instance, _context.Molang);
            slot.Instance.BeginStart(0f, _currentTick, currentBoneStates);
            _activeSlots.Add(slot);
        }
    }

    public void SetTransitionLength(float seconds)
    {
    }

    void IAnimationStateMachineHost.Reset()
    {
        foreach (var slot in _activeSlots)
            slot.Instance.BeginEnd(_currentTick);
        _fadingSlots.AddRange(_activeSlots);
        _activeSlots.Clear();
    }
}