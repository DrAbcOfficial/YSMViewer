using ConcreteMC.MolangSharp.Parser;
using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Models.AnimationController;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class AnimationStateMachine(
    AnimationControllerEntry controller,
    AnimationContext context) : IAnimationStateMachineHost
{
    private const int MaxTransitionIterations = 8;
    private const int MaxSubControllerDepth = 5;
    private const string EntryPrefix = "ysm-entry-";

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
    private float _dynamicTransitionLength;
    private float _currentStateEnterTick;

    private AnimationStateMachine? _childController;
    private string _controllerName = "";
    private int _depth;

    public void SetParentInfo(string name, int depth)
    {
        _controllerName = name;
        _depth = depth;
    }

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

        if (_childController is not null)
        {
            _childController.Process(tick, deltaTime, isMoving);
        }
        else
        {
            foreach (var slot in _activeSlots)
                slot.Process(_context, tick, _context.Molang, isMoving);

            for (int i = _fadingSlots.Count - 1; i >= 0; i--)
            {
                _fadingSlots[i].Process(_context, tick, _context.Molang, isMoving);
                if (!_fadingSlots[i].Instance.IsRunning)
                    _fadingSlots.RemoveAt(i);
            }
        }

        foreach (var blendState in _blendStates.Values)
            blendState.Reset();

        if (_childController is not null)
        {
            _childController.ForEachTransform((boneName, pos, rot, scale) =>
            {
                if (_blendStates.TryGetValue(boneName, out var bs))
                {
                    bs.BlendViaShortestPath = true;
                }
            });
        }

        foreach (var slot in _activeSlots)
        {
            if (!slot.IsActive) continue;
            foreach (var queue in slot.Instance.GetActiveQueues())
            {
                if (_blendStates.TryGetValue(queue.BoneName, out var blendState))
                {
                    blendState.AddSource(queue, Math.Clamp(slot.BlendWeight, 0f, 1f));
                    blendState.BlendViaShortestPath = slot.BlendViaShortestPath;
                }
            }
        }

        foreach (var slot in _fadingSlots)
        {
            if (!slot.IsActive) continue;
            foreach (var queue in slot.Instance.GetActiveQueues())
            {
                if (_blendStates.TryGetValue(queue.BoneName, out var blendState))
                {
                    blendState.AddSource(queue, Math.Clamp(slot.BlendWeight, 0f, 1f));
                    if (!blendState.BlendViaShortestPath)
                        blendState.BlendViaShortestPath = slot.BlendViaShortestPath;
                }
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

        bool allFinished = _activeSlots.Count > 0 && _activeSlots.All(s => s.Instance.IsAnimationFinished);
        bool anyFinished = _activeSlots.Any(s => s.Instance.IsAnimationFinished);
        _context.Molang.SetAnimVariable("all_animations_finished", allFinished ? 1f : 0f);
        _context.Molang.SetAnimVariable("any_animation_finished", anyFinished ? 1f : 0f);
    }

    public void ForEachTransform(Action<string, Vector3, Quaternion, Vector3> applyTransform)
    {
        if (_childController is not null)
        {
            _childController.ForEachTransform(applyTransform);
            return;
        }

        foreach (var (boneName, blendState) in _blendStates)
        {
            if (!blendState.HasActiveSources) continue;
            if (!_context.BoneNodes.TryGetValue(boneName, out _)) continue;

            applyTransform(boneName, blendState.BlendedPosition, blendState.BlendedRotation, blendState.BlendedScale);
        }
    }

    public bool GetBoneVisibility(string boneName)
    {
        if (_childController is not null)
            return _childController.GetBoneVisibility(boneName);

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

                    if (result != 0f)
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
        }
    }

    private void TransitionToState(string stateName, float currentTick)
    {
        if (!_controller.States.TryGetValue(stateName, out var newState))
        {
            if ("ysm-builtin".Equals(stateName, StringComparison.OrdinalIgnoreCase))
                System.Diagnostics.Debug.WriteLine($"[AnimationStateMachine] State 'ysm-builtin' has no equivalent in viewer (predicate-based controller not available). Skipping.");
            return;
        }

        if (_currentState is not null &&
            _controller.States.TryGetValue(_currentState, out var oldState))
        {
            ExecuteScripts(oldState.OnExit);
        }

        float blendTransitionDuration = _dynamicTransitionLength > 0f
            ? _dynamicTransitionLength
            : newState.BlendTransition.Constant;
        if (_dynamicTransitionLength <= 0f && !newState.BlendTransition.IsConstant && newState.BlendTransition.Curve is not null)
            blendTransitionDuration = EvaluateBlendTransitionCurve(newState.BlendTransition.Curve, currentTick - _currentStateEnterTick);
        _dynamicTransitionLength = 0f;

        foreach (var slot in _activeSlots)
            slot.Instance.BeginEnd(currentTick);
        _fadingSlots.AddRange(_activeSlots);
        _activeSlots.Clear();

        _currentState = stateName;
        _currentStateEnterTick = currentTick;

        var subName = ExtractSubControllerName(stateName);
        if (subName is not null && _depth < MaxSubControllerDepth && _context.AllControllers is not null)
        {
            string subControllerKey = _depth > 0
                ? $"{_controllerName}.{subName}"
                : subName;
            string fullKey = $"{_context.ControllerNameHint}.{subControllerKey}";

            if (_context.AllControllers.TryGetValue(fullKey, out var subController))
            {
                _childController = new AnimationStateMachine(subController, _context);
                _childController.SetParentInfo(subControllerKey, _depth + 1);
                _childController.Initialize();
            }
            else if (_context.AllControllers.TryGetValue(subControllerKey, out subController))
            {
                _childController = new AnimationStateMachine(subController, _context);
                _childController.SetParentInfo(subControllerKey, _depth + 1);
                _childController.Initialize();
            }
        }
        else if (subName is null)
        {
            _childController = null;
        }

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
                slot.BlendViaShortestPath = newState.BlendViaShortestPath;

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
            if (loopType == (int)ControllerSetAnimationLoopType.Loop
                || loopType == (int)ControllerSetAnimationLoopType.PlayOnce
                || loopType == (int)ControllerSetAnimationLoopType.HoldOnLastFrame)
            {
                instance.LoopModeOverride = loopType switch
                {
                    (int)ControllerSetAnimationLoopType.Loop => AnimationLoopMode.Loop,
                    (int)ControllerSetAnimationLoopType.PlayOnce => AnimationLoopMode.PlayOnce,
                    (int)ControllerSetAnimationLoopType.HoldOnLastFrame => AnimationLoopMode.HoldOnLastFrame,
                    _ => (AnimationLoopMode?)null
                };
            }
            var slot = new AnimationSlot(name, instance, _context.Molang);
            slot.Instance.BeginStart(0f, _currentTick, currentBoneStates);
            _activeSlots.Add(slot);
        }
    }

    public void SetTransitionLength(float seconds)
    {
        _dynamicTransitionLength = seconds;
    }

    void IAnimationStateMachineHost.Reset()
    {
        foreach (var slot in _activeSlots)
            slot.Instance.BeginEnd(_currentTick);
        _fadingSlots.AddRange(_activeSlots);
        _activeSlots.Clear();
    }

    public void IndicateReload()
    {
        foreach (var slot in _fadingSlots)
            slot.Instance.BeginEnd(_currentTick);
    }

    private static float EvaluateBlendTransitionCurve(Dictionary<float, float> curve, float time)
    {
        if (curve.Count == 0) return 0f;
        var sorted = curve.OrderBy(kv => kv.Key).ToList();
        if (sorted.Count == 1) return sorted[0].Value;

        if (time <= sorted[0].Key) return sorted[0].Value;
        if (time >= sorted[^1].Key) return sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Key && time <= sorted[i + 1].Key)
            {
                float span = sorted[i + 1].Key - sorted[i].Key;
                float t = span > 0f ? (time - sorted[i].Key) / span : 0f;
                return sorted[i].Value + (sorted[i + 1].Value - sorted[i].Value) * t;
            }
        }

        return sorted[^1].Value;
    }

    private static string? ExtractSubControllerName(string stateName)
    {
        if (stateName.StartsWith(EntryPrefix, StringComparison.OrdinalIgnoreCase))
            return stateName[EntryPrefix.Length..];
        return null;
    }
}