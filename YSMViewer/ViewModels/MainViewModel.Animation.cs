using Avalonia.Threading;
using System.Globalization;
using YSMViewer.Models.Document;
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
        HasOrphanExtraAnimationSettings = false;
        HasRawAnimations = false;
        AnimationNames.Clear();
        ExtraAnimationGroups.Clear();
        OrphanExtraAnimationSettingsGroups.Clear();
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

        HasRawAnimations = animRenderer.AnimationNames.Count > 0;
        HasAnimations = HasRawAnimations || HasExtraAnimations;

        foreach (var name in animRenderer.AnimationNames)
            AnimationNames.Add(name);

        CanPreviousAnimation = HasRawAnimations;
        CanNextAnimation = HasRawAnimations;
    }

    private void PopulateExtraAnimationData(Models.Document.YsmModelDocument document)
    {
        ExtraAnimationGroups.Clear();
        HasExtraAnimations = document.ExtraAnimations.HasEntries;
        var settingsGroups = BuildExtraAnimationSettingsGroups(document);
        var usedSettingsGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!HasExtraAnimations)
        {
            PopulateOrphanExtraAnimationSettings(settingsGroups, usedSettingsGroupIds);
            return;
        }

        if (document.ExtraAnimations.RootEntries.Count > 0)
        {
            var rootGroup = new ExtraAnimationGroupViewModel { Name = "Root" };
            foreach (var entry in document.ExtraAnimations.RootEntries)
                rootGroup.Entries.Add(CreateExtraAnimationItem(entry, settingsGroups, usedSettingsGroupIds));
            ExtraAnimationGroups.Add(rootGroup);
        }

        foreach (var group in document.ExtraAnimations.Groups)
        {
            if (group.Entries.Count == 0)
                continue;

            var groupVm = new ExtraAnimationGroupViewModel { Name = group.DisplayName };
            foreach (var entry in group.Entries)
                groupVm.Entries.Add(CreateExtraAnimationItem(entry, settingsGroups, usedSettingsGroupIds));
            ExtraAnimationGroups.Add(groupVm);
        }

        PopulateOrphanExtraAnimationSettings(settingsGroups, usedSettingsGroupIds);
    }

    private ExtraAnimationItemViewModel CreateExtraAnimationItem(
        YsmExtraAnimationEntry entry,
        IReadOnlyDictionary<string, ExtraAnimationSettingsGroupViewModel> settingsGroups,
        HashSet<string> usedSettingsGroupIds)
    {
        ExtraAnimationSettingsGroupViewModel? settingsGroup = null;
        if (!string.IsNullOrWhiteSpace(entry.ConfigGroupId)
            && settingsGroups.TryGetValue(entry.ConfigGroupId, out settingsGroup))
        {
            usedSettingsGroupIds.Add(entry.ConfigGroupId);
        }

        return new ExtraAnimationItemViewModel
        {
            DisplayName = entry.DisplayName,
            AnimationName = entry.Key,
            Category = entry.Category,
            OriginalIndex = entry.OriginalIndex,
            SettingsGroup = settingsGroup,
            OnSelected = item => SelectAnimation(item.AnimationName),
        };
    }

    private Dictionary<string, ExtraAnimationSettingsGroupViewModel> BuildExtraAnimationSettingsGroups(YsmModelDocument document)
    {
        var result = new Dictionary<string, ExtraAnimationSettingsGroupViewModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in document.ExtraAnimations.ButtonDefinitions)
        {
            var group = new ExtraAnimationSettingsGroupViewModel
            {
                GroupId = definition.Id,
                Name = string.IsNullOrWhiteSpace(definition.Name) ? definition.Id : definition.Name,
                Description = definition.Description,
            };

            foreach (var form in definition.Forms)
            {
                var formVm = CreateExtraAnimationForm(form);
                if (formVm is not null)
                    group.Forms.Add(formVm);
            }

            if (group.Forms.Count > 0)
                result[definition.Id] = group;
        }

        return result;
    }

    private ExtraAnimationFormViewModel? CreateExtraAnimationForm(YsmExtraAnimationForm form)
    {
        if (Renderer is not IAnimationRenderer { MolangService: { } molang })
            return null;

        var type = form.Type.ToLowerInvariant();
        return type switch
        {
            "checkbox" => CreateBooleanForm(form, molang),
            "range" => CreateRangeForm(form, molang),
            "radio" => CreateRadioForm(form, molang),
            _ => null,
        };
    }

    private static ExtraAnimationBooleanFormViewModel CreateBooleanForm(YsmExtraAnimationForm form, Services.Molang.MolangService molang)
    {
        var item = new ExtraAnimationBooleanFormViewModel(value => molang.ExecutePreviewExpression($"{form.Value}={(value ? "1" : "0")}"))
        {
            Title = FormTitle(form),
            Description = form.Description,
            Value = molang.EvaluatePreviewExpression(form.Value) > 0.5f,
        };
        return item;
    }

    private static ExtraAnimationRangeFormViewModel CreateRangeForm(YsmExtraAnimationForm form, Services.Molang.MolangService molang)
    {
        var min = form.Min;
        var max = form.Max > form.Min ? form.Max : form.Min + 1f;
        var item = new ExtraAnimationRangeFormViewModel(value => molang.ExecutePreviewExpression($"{form.Value}={value.ToString(CultureInfo.InvariantCulture)}"))
        {
            Title = FormTitle(form),
            Description = form.Description,
            Min = min,
            Max = max,
            Step = form.Step > 0f ? form.Step : 0.1f,
            Value = Math.Clamp(molang.EvaluatePreviewExpression(form.Value), min, max),
        };
        return item;
    }

    private static ExtraAnimationRadioFormViewModel CreateRadioForm(YsmExtraAnimationForm form, Services.Molang.MolangService molang)
    {
        var current = Math.Round(molang.EvaluatePreviewExpression(form.Value));
        var item = new ExtraAnimationRadioFormViewModel(option =>
        {
            if (option is not null)
                molang.ExecutePreviewExpression(option.Expression);
        })
        {
            Title = FormTitle(form),
            Description = form.Description,
        };

        foreach (var label in form.Labels)
        {
            var option = new ExtraAnimationRadioOptionViewModel { Label = label.Label, Expression = label.Expression };
            item.Options.Add(option);
            if (double.TryParse(label.Label, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericLabel)
                && Math.Abs(numericLabel - current) < 0.001)
            {
                item.SelectedOption = option;
            }
        }

        item.SelectedOption ??= item.Options.FirstOrDefault();
        return item;
    }

    private static string FormTitle(YsmExtraAnimationForm form)
    {
        return string.IsNullOrWhiteSpace(form.Title) ? form.Value : form.Title;
    }

    private void PopulateOrphanExtraAnimationSettings(
        IReadOnlyDictionary<string, ExtraAnimationSettingsGroupViewModel> settingsGroups,
        HashSet<string> usedSettingsGroupIds)
    {
        OrphanExtraAnimationSettingsGroups.Clear();
        foreach (var group in settingsGroups.Values.OrderBy(g => g.GroupId, StringComparer.OrdinalIgnoreCase))
        {
            if (!usedSettingsGroupIds.Contains(group.GroupId))
                OrphanExtraAnimationSettingsGroups.Add(group);
        }
        HasOrphanExtraAnimationSettings = OrphanExtraAnimationSettingsGroups.Count > 0;
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
