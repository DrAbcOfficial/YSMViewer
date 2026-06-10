using ConcreteMC.MolangSharp.Parser;
using YSMViewer.Services.Molang;

namespace YSMViewer.Services.Animation;

public sealed class AnimationSlot(string name, AnimationControllerInstance instance, MolangService molang)
{
    private readonly AnimationControllerInstance _instance = instance;
    private IExpression? _conditionExpr;
    private bool _conditionActive = true;
    private float _conditionWeight = 1f;
    public bool BlendViaShortestPath { get; set; }

    public string AnimationName { get; } = name;
    public AnimationControllerInstance Instance => _instance;
    public bool IsActive => _conditionActive && _instance.IsRunning;
    public float BlendWeight => _instance.IsRunning ? _conditionWeight * _instance.EvaluateBlendWeight(_molang) : 0f;

    private readonly MolangService _molang = molang;

    public void SetCondition(string? molangCondition)
    {
        if (string.IsNullOrEmpty(molangCondition))
        {
            _conditionExpr = null;
            _conditionActive = true;
            _conditionWeight = 1f;
        }
        else
        {
            _conditionExpr = _molang.Parse(molangCondition);
        }
    }

    public void EvaluateCondition(MolangService molang)
    {
        if (_conditionExpr is not null)
        {
            float result = molang.Evaluate(_conditionExpr);
            _conditionActive = result != 0f;
            _conditionWeight = _conditionActive ? result : 0f;
        }
        else
        {
            _conditionActive = true;
            _conditionWeight = 1f;
        }
    }

    public void Process(AnimationContext context, float tick, MolangService molang, bool isMoving)
    {
        EvaluateCondition(molang);
        _instance.Process(tick, molang);
    }

    public BoneAnimationQueue? GetBoneQueue(string boneName)
        => _instance.GetBoneQueue(boneName);
}