using Avalonia.Threading;
using YSMViewer.Rendering;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel
{
    private DispatcherTimer? _animationTimer;
    private bool _suppressAnimChanged;

    private void ResetAnimationState()
    {
        HasAnimations = false;
        HasExtraAnimations = false;
        AnimationNames.Clear();
        ExtraAnimationGroups.Clear();
        CurrentAnimationName = string.Empty;
        _suppressAnimChanged = false;
        IsAnimating = false;
    }

    private void PopulateAnimationData(Models.Document.YsmModelDocument document)
    {
        if (!SupportsAnimation || Renderer is not IAnimationRenderer animRenderer)
            return;

        if (document.Animations.Count <= 0)
            return;

        PopulateExtraAnimationData(document);

        HasAnimations = animRenderer.AnimationNames.Count > 0 || HasExtraAnimations;

        foreach (var name in animRenderer.AnimationNames)
            AnimationNames.Add(name);

        CanPreviousAnimation = animRenderer.AnimationNames.Count > 0;
        CanNextAnimation = animRenderer.AnimationNames.Count > 0;
    }

    private void PopulateExtraAnimationData(Models.Document.YsmModelDocument document)
    {
        ExtraAnimationGroups.Clear();
        HasExtraAnimations = document.ExtraAnimations.HasEntries;
        if (!HasExtraAnimations)
            return;

        if (document.ExtraAnimations.RootEntries.Count > 0)
        {
            var rootGroup = new ExtraAnimationGroupViewModel { Name = "Root" };
            foreach (var entry in document.ExtraAnimations.RootEntries)
                rootGroup.Entries.Add(CreateExtraAnimationItem(entry));
            ExtraAnimationGroups.Add(rootGroup);
        }

        foreach (var group in document.ExtraAnimations.Groups)
        {
            if (group.Entries.Count == 0)
                continue;

            var groupVm = new ExtraAnimationGroupViewModel { Name = group.DisplayName };
            foreach (var entry in group.Entries)
                groupVm.Entries.Add(CreateExtraAnimationItem(entry));
            ExtraAnimationGroups.Add(groupVm);
        }
    }

    private ExtraAnimationItemViewModel CreateExtraAnimationItem(Models.Document.YsmExtraAnimationEntry entry)
    {
        return new ExtraAnimationItemViewModel
        {
            DisplayName = entry.DisplayName,
            AnimationName = entry.Key,
            Category = entry.Category,
            OriginalIndex = entry.OriginalIndex,
            OnSelected = item => SelectAnimation(item.AnimationName),
        };
    }

    partial void OnIsAnimatingChanged(bool value)
    {
        if (_suppressAnimChanged) return;

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
                animRenderer.StopAnimation();
            }
        }
    }

    public void SelectAnimation(string name)
    {
        if (Renderer is not IAnimationRenderer animRenderer) return;
        if (!animRenderer.AnimationNames.Contains(name) && !UseAnimationController && !HasExtraAnimations) return;

        CurrentAnimationName = name;
        animRenderer.PlayAnimation(name);
        AnimationProgress = 0f;
        AnimationTimeText = string.Empty;
        _suppressAnimChanged = true;
        IsAnimating = true;
        _suppressAnimChanged = false;
        StartAnimationTimer();
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
        _suppressAnimChanged = false;
        IsAnimating = false;
        CurrentAnimationName = string.Empty;
        AnimationProgress = 0f;
        AnimationTimeText = string.Empty;
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
        StopAnimationTimer();
        int interval = IsDesktop ? 16 : 100;
        _animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(interval),
            DispatcherPriority.Render,
            (_, _) => UpdateAnimation(interval / 1000f));
        _animationTimer.Start();
    }

    private void StopAnimationTimer()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }
}
