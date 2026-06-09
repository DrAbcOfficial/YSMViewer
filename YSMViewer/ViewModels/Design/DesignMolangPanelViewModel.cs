using System.Collections.ObjectModel;
using YSMViewer.ViewModels;

namespace YSMViewer.ViewModels.Design;

public static class DesignMolangPanelViewModel
{
    public static MolangPanelViewModel Instance => new()
    {
        Variables = new ObservableCollection<MolangVariableItem>
        {
            new() { Name = "query.ground_speed", Value = 2.5f, MinValue = 0, MaxValue = 10, Step = 0.1f, DefaultValue = 0f },
            new() { Name = "query.is_on_ground", Value = 1f, IsBoolean = true, ControlType = MolangControlType.Toggle, DefaultValue = 1f, MinValue = 0, MaxValue = 1, Step = 1f },
            new() { Name = "query.is_sneaking",  Value = 0f, IsBoolean = true, ControlType = MolangControlType.Toggle, DefaultValue = 0f, MinValue = 0, MaxValue = 1, Step = 1f },
            new() { Name = "query.health",       Value = 20f, MinValue = 0, MaxValue = 20, Step = 1f, DefaultValue = 20f },
        }
    };
}