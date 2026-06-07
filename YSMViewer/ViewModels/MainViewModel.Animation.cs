using Avalonia.Threading;
using YSMViewer.Rendering;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel
{
    private DispatcherTimer? _animationTimer;

    private void ResetAnimationState()
    {
        HasAnimations = false;
        AnimationNames.Clear();
        CurrentAnimationName = string.Empty;
        IsAnimating = false;
    }

    private void PopulateAnimationData(Models.Document.YsmModelDocument document)
    {
        if (!SupportsAnimation || Renderer is not IAnimationRenderer animRenderer)
            return;

        if (document.Animations.Count <= 0)
            return;

        HasAnimations = animRenderer.AnimationNames.Count > 0;

        foreach (var name in animRenderer.AnimationNames)
            AnimationNames.Add(name);

        CanPreviousAnimation = animRenderer.AnimationNames.Count > 0;
        CanNextAnimation = animRenderer.AnimationNames.Count > 0;

        if (AnimationNames.Count > 0 && string.IsNullOrEmpty(CurrentAnimationName))
            CurrentAnimationName = AnimationNames[0];
    }

    partial void OnIsAnimatingChanged(bool value)
    {
        if (Renderer is IAnimationRenderer animRenderer)
        {
            if (value && CurrentAnimationName is { Length: > 0 })
            {
                animRenderer.PlayAnimation(CurrentAnimationName);
                StartAnimationTimer();
            }
            else if (!value)
            {
                StopAnimationTimer();
            }
        }
    }

    public void SelectAnimation(string name)
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (!animRenderer.AnimationNames.Contains(name)) return;

        CurrentAnimationName = name;
        animRenderer.PlayAnimation(name);
        AnimationProgress = 0f;
        AnimationTimeText = string.Empty;
        IsAnimating = true;
        UpdateAnimationNavigationState();
    }

    public void NextAnimation()
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (animRenderer.AnimationNames.Count == 0) return;

        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var nextIndex = (currentIndex + 1) % names.Count;
        SelectAnimation(names[nextIndex]);
    }

    public void StopAnimation()
    {
        StopAnimationTimer();
        IsAnimating = false;
        CurrentAnimationName = string.Empty;
        AnimationProgress = 0f;
        AnimationTimeText = string.Empty;

        if (Renderer is IAnimationRenderer animRenderer)
            animRenderer.StopAnimation();
    }

    public void PreviousAnimation()
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (animRenderer.AnimationNames.Count == 0) return;

        var names = AnimationNames;
        int currentIndex = names.IndexOf(CurrentAnimationName);
        if (currentIndex < 0) currentIndex = 0;
        var prevIndex = (currentIndex - 1 + names.Count) % names.Count;
        SelectAnimation(names[prevIndex]);
    }

    private void UpdateAnimationNavigationState()
    {
        if (Renderer is IAnimationRenderer animRenderer)
        {
            var names = animRenderer.AnimationNames;
            CanPreviousAnimation = names.Count > 0;
            CanNextAnimation = names.Count > 0;
        }
    }

    public void UpdateAnimation(float deltaTime)
    {
        if (!IsAnimating || Renderer is not IAnimationRenderer animRenderer) return;

        float duration = animRenderer.AnimationDuration;
        if (duration > 0f)
        {
            float current = animRenderer.AnimationCurrentTime;
            AnimationProgress = current / duration;
            int curSec = (int)current;
            int totalSec = (int)duration;
            AnimationTimeText = $"{curSec / 60}:{curSec % 60:D2} / {totalSec / 60}:{totalSec % 60:D2}";
        }
        else
        {
            AnimationProgress = 0f;
            AnimationTimeText = string.Empty;
        }
    }

    private void StartAnimationTimer()
    {
        if (_animationTimer is not null) return;
        _animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            (_, _) => UpdateAnimation(0.016f));
        _animationTimer.Start();
    }

    private void StopAnimationTimer()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }
}
