using YSMViewer.Models.Document;

namespace YSMViewer.ViewModels;

public sealed partial class MainViewModel
{
    private void BuildBoneTree()
    {
        BoneTreeRoots.Clear();
        if (_currentDocument is null) return;

        var boneParentMap = new Dictionary<string, string?>();
        foreach (var model in _currentDocument.Models)
        {
            foreach (var bone in model.Bones)
                boneParentMap[bone.Id] = bone.ParentId;
        }

        var rootBones = new List<YsmBoneInfo>();
        foreach (var model in _currentDocument.Models)
        {
            foreach (var bone in model.Bones)
            {
                if (bone.ParentId is null || !boneParentMap.ContainsKey(bone.ParentId))
                    rootBones.Add(bone);
            }
        }

        foreach (var bone in rootBones)
        {
            var item = BuildBoneTreeItem(bone, _currentDocument, []);
            if (item is not null)
                BoneTreeRoots.Add(item);
        }
    }

    private BoneTreeItemViewModel? BuildBoneTreeItem(YsmBoneInfo bone, YsmModelDocument document, HashSet<string> visited)
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

        var childBones = new List<YsmBoneInfo>();
        foreach (var model in document.Models)
        {
            foreach (var child in model.Bones)
            {
                if (child.ParentId == bone.Id)
                    childBones.Add(child);
            }
        }

        foreach (var child in childBones)
        {
            if (visited.Contains(child.Id))
                continue;
            var childItem = BuildBoneTreeItem(child, document, visited);
            if (childItem is not null)
                item.Children.Add(childItem);
        }

        item.HasChildren = item.Children.Count > 0;
        item.Icon = item.HasChildren ? "📁" : "🧊";
        return item;
    }

    public void ExpandAllBones()
    {
        foreach (var root in BoneTreeRoots)
            root.SetExpandedRecursive(true);
    }

    public void CollapseAllBones()
    {
        foreach (var root in BoneTreeRoots)
            root.SetExpandedRecursive(false);
    }
}
