using YSMViewer.Models.Document;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel
{
    private void BuildBoneTree()
    {
        BoneGroups.Clear();
        if (_currentDocument is null) return;

        for (int i = 0; i < _currentDocument.Models.Count; i++)
        {
            var model = _currentDocument.Models[i];
            var componentVm = Components[i];

            var boneParentMap = new Dictionary<string, string?>();
            foreach (var bone in model.Bones)
                boneParentMap[bone.Id] = bone.ParentId;

            var group = new ComponentBoneGroupViewModel
            {
                Name = componentVm.Name,
                IsVisible = componentVm.IsVisible,
                ComponentId = componentVm.ComponentId,
            };

            var visited = new HashSet<string>();
            foreach (var bone in model.Bones)
            {
                if (bone.ParentId is null || !boneParentMap.ContainsKey(bone.ParentId))
                {
                    var item = BuildBoneTreeItem(bone, model.Bones, visited);
                    if (item is not null)
                        group.BoneRoots.Add(item);
                }
            }

            componentVm.BoneGroup = group;
            BoneGroups.Add(group);
        }
    }

    private BoneTreeItemViewModel? BuildBoneTreeItem(YsmBoneInfo bone, IReadOnlyList<YsmBoneInfo> bones, HashSet<string> visited)
    {
        if (!visited.Add(bone.Id))
            return null;

        var item = new BoneTreeItemViewModel
        {
            Name = bone.Name,
            BoneId = bone.Id,
            IsVisible = true,
            OnVisibilityToggled = (id, vis) => SetBoneVisible(id, vis),
        };

        foreach (var child in bones)
        {
            if (child.ParentId == bone.Id && !visited.Contains(child.Id))
            {
                var childItem = BuildBoneTreeItem(child, bones, visited);
                if (childItem is not null)
                    item.Children.Add(childItem);
            }
        }

        item.HasChildren = item.Children.Count > 0;
        item.Icon = item.HasChildren ? "📁" : "🧊";
        return item;
    }

    public void ExpandAllBones()
    {
        foreach (var group in BoneGroups)
            group.ExpandAll();
    }

    public void CollapseAllBones()
    {
        foreach (var group in BoneGroups)
            group.CollapseAll();
    }
}
