using Aura3D.Core.Geometries;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using System.Drawing;
using System.Numerics;

namespace YSMViewer.Rendering.Aura3D;

public class SphericalGizmo : Node
{
    private const float RodHalf = 0.32f;
    private const float RodRadius = 0.04f;
    private const float TipRadius = 0.065f;

    public SphericalGizmo()
    {
        Name = "SphericalGizmo";

        var globe = new Mesh
        {
            Name = "GizmoGlobe",
            Geometry = new SphereGeometry(0.1f, 24, 24),
            Material = CreateMaterial(Color.FromArgb(80, 180, 180, 200)),
        };
        globe.Material.BlendMode = BlendMode.Translucent;
        AddChild(globe, AttachToParentRule.KeepLocal);

        AddAxis(new Vector3(1, 0, 0), Color.FromArgb(255, 250, 70, 70));
        AddAxis(new Vector3(0, 1, 0), Color.FromArgb(255, 70, 230, 90));
        AddAxis(new Vector3(0, 0, 1), Color.FromArgb(255, 70, 140, 255));
        AddAxis(new Vector3(-1, 0, 0), Color.FromArgb(140, 250, 70, 70));
        AddAxis(new Vector3(0, -1, 0), Color.FromArgb(140, 70, 230, 90));
        AddAxis(new Vector3(0, 0, -1), Color.FromArgb(140, 70, 140, 255));
    }

    private static Material CreateMaterial(Color color)
    {
        var tex = Texture.CreateFromColor(color);
        return new Material
        {
            BaseColor = tex,
            BlendMode = BlendMode.Translucent,
        };
    }

    private void AddAxis(Vector3 dir, Color color)
    {
        float len = RodHalf * 2f;
        float w = MathF.Abs(dir.X) > 0.5f ? len : RodRadius * 2f;
        float h = MathF.Abs(dir.Y) > 0.5f ? len : RodRadius * 2f;
        float d = MathF.Abs(dir.Z) > 0.5f ? len : RodRadius * 2f;

        var rod = new Mesh
        {
            Geometry = new BoxGeometry(w, h, d),
            Material = CreateMaterial(color),
            Position = dir * RodHalf
        };
        AddChild(rod, AttachToParentRule.KeepLocal);

        var tip = new Mesh
        {
            Geometry = new SphereGeometry(TipRadius, 12, 12),
            Material = CreateMaterial(color),
            Position = dir * len
        };
        AddChild(tip, AttachToParentRule.KeepLocal);
    }
}
