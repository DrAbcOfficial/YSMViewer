using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using YSMViewer.Services.Molang;

namespace YSMViewer.ViewModels;

public sealed partial class MolangPanelViewModel : ObservableObject
{
    private readonly MolangService? _molangService;

    [ObservableProperty]
    private ObservableCollection<MolangVariableItem> _variables = [];

    public MolangPanelViewModel() { }

    public MolangPanelViewModel(MolangService molangService)
    {
        _molangService = molangService;
    }

    public static readonly Dictionary<string, (float Min, float Max, float Default, float Step)> VariablePresets = new()
    {
        ["query.ground_speed"] = (0f, 10f, 0f, 0.1f),
        ["query.is_on_ground"] = (0f, 1f, 1f, 1f),
        ["query.is_moving"] = (0f, 1f, 0f, 1f),
        ["query.is_sneaking"] = (0f, 1f, 0f, 1f),
        ["query.is_swimming"] = (0f, 1f, 0f, 1f),
        ["query.is_flying"] = (0f, 1f, 0f, 1f),
        ["query.is_sprinting"] = (0f, 1f, 0f, 1f),
        ["query.is_gliding"] = (0f, 1f, 0f, 1f),
        ["query.is_blocking"] = (0f, 1f, 0f, 1f),
        ["query.is_using_item"] = (0f, 1f, 0f, 1f),
        ["query.is_sleeping"] = (0f, 1f, 0f, 1f),
        ["query.is_in_water"] = (0f, 1f, 0f, 1f),
        ["query.is_in_water_or_rain"] = (0f, 1f, 0f, 1f),
        ["query.is_on_fire"] = (0f, 1f, 0f, 1f),
        ["query.is_riding"] = (0f, 1f, 0f, 1f),
        ["query.is_spectator"] = (0f, 1f, 0f, 1f),
        ["query.is_first_person"] = (0f, 1f, 0f, 1f),
        ["query.is_jumping"] = (0f, 1f, 0f, 1f),
        ["query.is_eating"] = (0f, 1f, 0f, 1f),
        ["query.is_playing_dead"] = (0f, 1f, 0f, 1f),
        ["query.has_rider"] = (0f, 1f, 0f, 1f),
        ["query.has_cape"] = (0f, 1f, 0f, 1f),
        ["query.has_helmet"] = (0f, 1f, 0f, 1f),
        ["query.has_chestplate"] = (0f, 1f, 0f, 1f),
        ["query.has_leggings"] = (0f, 1f, 0f, 1f),
        ["query.has_boots"] = (0f, 1f, 0f, 1f),
        ["query.has_elytra"] = (0f, 1f, 0f, 1f),
        ["query.has_offhand"] = (0f, 1f, 0f, 1f),
        ["query.health"] = (0f, 20f, 20f, 0.5f),
        ["query.max_health"] = (0f, 20f, 20f, 1f),
        ["query.hurt_time"] = (0f, 10f, 0f, 1f),
        ["query.death_time"] = (0f, 20f, 0f, 1f),
        ["query.armor_value"] = (0f, 20f, 0f, 1f),
        ["query.head_x_rotation"] = (-90f, 90f, 0f, 1f),
        ["query.head_y_rotation"] = (-180f, 180f, 0f, 1f),
        ["query.body_x_rotation"] = (-180f, 180f, 0f, 1f),
        ["query.body_y_rotation"] = (-180f, 180f, 0f, 1f),
        ["query.eye_target_x_rotation"] = (-90f, 90f, 0f, 1f),
        ["query.eye_target_y_rotation"] = (-180f, 180f, 0f, 1f),
        ["query.input_vertical"] = (-1f, 1f, 0f, 0.1f),
        ["query.input_horizontal"] = (-1f, 1f, 0f, 0.1f),
        ["query.vertical_speed"] = (-10f, 10f, 0f, 0.1f),
        ["query.yaw_speed"] = (-50f, 50f, 0f, 1f),
        ["query.modified_move_speed"] = (0f, 10f, 1f, 0.1f),
        ["query.modified_distance_moved"] = (0f, 100f, 0f, 1f),
        ["query.walk_distance"] = (0f, 100f, 0f, 1f),
        ["query.distance_from_camera"] = (0f, 50f, 0f, 0.5f),
        ["query.time_of_day"] = (0f, 1f, 0f, 0.01f),
        ["query.moon_phase"] = (0f, 7f, 0f, 1f),
        ["query.player_level"] = (0f, 100f, 0f, 1f),
        ["query.item_in_use_duration"] = (0f, 10f, 0f, 0.1f),
        ["query.item_max_use_duration"] = (0f, 100f, 32f, 1f),
        ["query.item_remaining_use_duration"] = (0f, 100f, 0f, 1f),
        ["query.cape_flap_amount"] = (0f, 1f, 0f, 0.1f),
    };

    public void DiscoverVariables(IEnumerable<string> molangExpressions)
    {
        var foundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var expr in molangExpressions)
            FindQueryVariables(expr, foundNames);

        var existingNames = new HashSet<string>(
            Variables.Select(v => v.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var name in foundNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (existingNames.Contains(name)) continue;

            var item = CreateDefaultItem(name);
            item.ValueChanged += OnVariableValueChanged;
            Variables.Add(item);
        }
    }

    private static void FindQueryVariables(string expression, HashSet<string> found)
    {
        var queryMatches = Regex.Matches(
            expression, @"\b(?:query|q)\.\w+(?:\.\w+)*\b",
            RegexOptions.IgnoreCase);

        foreach (Match m in queryMatches)
        {
            var val = m.Value.ToLowerInvariant();
            if (val.StartsWith("q."))
                val = "query." + val[2..];
            found.Add(val);
        }

        var varMatches = Regex.Matches(
            expression, @"\b(?:variable|v)\.\w+(?:\.\w+)*\b",
            RegexOptions.IgnoreCase);

        foreach (Match m in varMatches)
        {
            var val = m.Value.ToLowerInvariant();
            if (val.StartsWith("v."))
                val = "variable." + val[2..];
            found.Add(val);
        }
    }

    private static MolangVariableItem CreateDefaultItem(string name)
    {
        if (VariablePresets.TryGetValue(name, out var preset))
        {
            var isBool = preset.Max == 1f && preset.Step >= 1f && preset.Min == 0f;
            return new MolangVariableItem
            {
                Name = name,
                DefaultValue = preset.Default,
                Value = preset.Default,
                MinValue = preset.Min,
                MaxValue = preset.Max,
                Step = preset.Step,
                IsBoolean = isBool,
                ControlType = isBool ? MolangControlType.Toggle : MolangControlType.Slider,
            };
        }

        var lower = name.ToLowerInvariant();
        return lower switch
        {
            _ when lower.Contains("is_") || lower.Contains("has_") => new MolangVariableItem
            {
                Name = name,
                DefaultValue = 0f,
                Value = 0f,
                MinValue = 0f,
                MaxValue = 1f,
                Step = 1f,
                IsBoolean = true,
                ControlType = MolangControlType.Toggle,
            },
            _ when lower.Contains("input_") => new MolangVariableItem
            {
                Name = name,
                DefaultValue = 0f,
                Value = 0f,
                MinValue = -1f,
                MaxValue = 1f,
                Step = 0.1f,
                ControlType = MolangControlType.Slider,
            },
            _ when lower.Contains("rotation") => new MolangVariableItem
            {
                Name = name,
                DefaultValue = 0f,
                Value = 0f,
                MinValue = -180f,
                MaxValue = 180f,
                Step = 1f,
                ControlType = MolangControlType.Slider,
            },
            _ when lower.Contains("health") => new MolangVariableItem
            {
                Name = name,
                DefaultValue = 20f,
                Value = 20f,
                MinValue = 0f,
                MaxValue = 20f,
                Step = 1f,
                ControlType = MolangControlType.Slider,
            },
            _ => new MolangVariableItem
            {
                Name = name,
                DefaultValue = 0f,
                Value = 0f,
                MinValue = 0f,
                MaxValue = 1f,
                Step = 0.1f,
                ControlType = MolangControlType.Slider,
            },
        };
    }

    private void OnVariableValueChanged(MolangVariableItem item, float value)
    {
        _molangService?.SetUserVariable(item.Name, value);
    }

    [RelayCommand]
    private void ResetAllToDefaults()
    {
        foreach (var v in Variables)
            v.Value = v.DefaultValue;
    }
}