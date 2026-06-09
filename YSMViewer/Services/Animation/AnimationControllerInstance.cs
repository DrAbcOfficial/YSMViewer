using System.Numerics;
using YSMViewer.Models;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public enum AnimationResamplerState
{
    Idle,
    BeginningTransition,
    Running,
    EndingTransition
}

public sealed class AnimationControllerInstance(
    MinecraftAnimation animation,
    AnimationContext context)
{
    private const float DefaultEndingTransitionDuration = 0.15f;

    private readonly MinecraftAnimation _animation = animation;
    private readonly AnimationContext _context = context;
    private readonly Dictionary<string, BoneAnimationQueue> _boneQueues = [];
    private readonly List<BoneAnimationQueue> _activeQueues = [];

    private AnimationResamplerState _state = AnimationResamplerState.Idle;
    private float _currentTick;
    private float _tickOffset;
    private float _beginningTransitionElapsed;
    private float _beginningTransitionDuration;
    private float _endingTransitionElapsed;
    private float _endingTransitionDuration = DefaultEndingTransitionDuration;
    private bool _isAnimationFinished = true;

    private readonly Dictionary<string, Vector3> _basePositions = new(context.BasePositions);
    private readonly Dictionary<string, Vector3> _baseEulers = new(context.BaseEulers);

    public bool IsRunning => _state != AnimationResamplerState.Idle;
    public bool IsAnimationFinished => _isAnimationFinished;

    public float EvaluateBlendWeight(MolangService molang)
    {
        if (_animation.BlendWeight <= 0f && _animation.Loop)
            return 1f;
        return _animation.BlendWeight > 0f ? _animation.BlendWeight : 1f;
    }

    public void InitializeBoneQueues(
        IReadOnlyDictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> currentBoneStates)
    {
        _boneQueues.Clear();
        _activeQueues.Clear();

        if (_animation.Bones is null) return;

        foreach (var (boneName, boneAnim) in _animation.Bones)
        {
            if (!_basePositions.TryGetValue(boneName, out var basePos))
                basePos = Vector3.Zero;
            if (!_baseEulers.TryGetValue(boneName, out var baseEuler))
                baseEuler = Vector3.Zero;

            var queue = new BoneAnimationQueue(boneName, basePos, baseEuler);

            if (currentBoneStates.TryGetValue(boneName, out var state))
                queue.CaptureSnapshot(state.pos, state.rot, state.scale);

            queue.ApplyAnimation(boneAnim);
            _boneQueues[boneName] = queue;
            _activeQueues.Add(queue);
        }
    }

    public void BeginStart(float blendTransitionDuration, float currentTick,
        IReadOnlyDictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> currentBoneStates)
    {
        _beginningTransitionDuration = blendTransitionDuration > 0f ? blendTransitionDuration : DefaultEndingTransitionDuration;
        _tickOffset = currentTick;
        _currentTick = 0f;
        _beginningTransitionElapsed = 0f;
        _endingTransitionElapsed = 0f;
        _isAnimationFinished = false;
        _state = AnimationResamplerState.BeginningTransition;

        InitializeBoneQueues(currentBoneStates);

        foreach (var queue in _activeQueues)
            queue.ProcessBeginningTransition(0f, 0f, _context.Molang);
    }

    public void BeginEnd(float currentTick)
    {
        if (_state == AnimationResamplerState.Running ||
            _state == AnimationResamplerState.BeginningTransition)
        {
            foreach (var queue in _activeQueues)
                queue.CacheCurrentValues();

            _tickOffset = currentTick;
            _endingTransitionDuration = _beginningTransitionDuration > 0f
                ? _beginningTransitionDuration
                : DefaultEndingTransitionDuration;
            _endingTransitionElapsed = 0f;
            _isAnimationFinished = true;
            _state = AnimationResamplerState.EndingTransition;

            foreach (var queue in _activeQueues)
                queue.ProcessEndingTransition(0f);
        }
        else
        {
            _state = AnimationResamplerState.Idle;
        }
    }

    public void Process(float tick, MolangService molang)
    {
        if (_state == AnimationResamplerState.Idle)
            return;

        float adjustedTick = MathF.Max(tick - _tickOffset, 0f);

        switch (_state)
        {
            case AnimationResamplerState.BeginningTransition:
                ProcessBeginningTransition(adjustedTick, molang);
                break;
            case AnimationResamplerState.Running:
                ProcessRunning(adjustedTick, molang);
                break;
            case AnimationResamplerState.EndingTransition:
                ProcessEndingTransition();
                break;
        }
    }

    private void ProcessBeginningTransition(float adjustedTick, MolangService molang)
    {
        _beginningTransitionElapsed += _context.DeltaTime;
        float progress = _beginningTransitionDuration > 0f
            ? _beginningTransitionElapsed / _beginningTransitionDuration
            : 1f;

        if (progress >= 1f)
        {
            float animationTick = adjustedTick - _beginningTransitionDuration;
            _tickOffset += _beginningTransitionDuration;
            _currentTick = MathF.Max(animationTick, 0f);
            _state = AnimationResamplerState.Running;
            ProcessRunning(_currentTick, molang);
            return;
        }

        foreach (var queue in _activeQueues)
        {
            queue.SetBlendWeight(EvaluateBlendWeight(molang));
            queue.ProcessBeginningTransition(progress, adjustedTick, molang);
        }
    }

    private void ProcessRunning(float adjustedTick, MolangService molang)
    {
        _currentTick = adjustedTick;
        float length = _animation.AnimationLength;

        if (length <= 0f) return;

        if (adjustedTick >= length)
        {
            if (_animation.LoopMode == AnimationLoopMode.HoldOnLastFrame)
            {
                _currentTick = length;
            }
            else if (_animation.LoopMode == AnimationLoopMode.Loop)
            {
                _currentTick = adjustedTick % length;
            }
            else
            {
                _currentTick = length;
                BeginEnd(adjustedTick + _tickOffset);
                return;
            }
        }

        float animTimeSeconds = _currentTick;
        _context.Molang.SetAnimVariable("anim_time", animTimeSeconds);

        foreach (var queue in _activeQueues)
        {
            queue.SetBlendWeight(EvaluateBlendWeight(molang));
            queue.ProcessRunning(_currentTick, molang);
        }
    }

    private void ProcessEndingTransition()
    {
        _endingTransitionElapsed += _context.DeltaTime;
        float progress = _endingTransitionDuration > 0f
            ? _endingTransitionElapsed / _endingTransitionDuration
            : 1f;

        if (progress >= 1f)
        {
            _state = AnimationResamplerState.Idle;
            foreach (var queue in _activeQueues)
                queue.Clear();
            return;
        }

        foreach (var queue in _activeQueues)
        {
            queue.SetBlendWeight(EvaluateBlendWeight(_context.Molang));
            queue.ProcessEndingTransition(progress);
        }
    }

    public BoneAnimationQueue? GetBoneQueue(string boneName)
        => _boneQueues.TryGetValue(boneName, out var q) ? q : null;

    public IReadOnlyList<BoneAnimationQueue> GetActiveQueues() => _activeQueues;
}