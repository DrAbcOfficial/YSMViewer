using System.Numerics;
using YSMViewer.Services;

namespace YSMViewer.Core.Tests.Services;

public sealed class AnimationServiceTests
{
    [Fact]
    public void CreateBlockbenchQuaternion_Identity_ReturnsIdentity()
    {
        var q = AnimationService.CreateBlockbenchQuaternion(Vector3.Zero);

        Assert.Equal(Quaternion.Identity, q);
    }

    [Fact]
    public void CreateBlockbenchQuaternion_X90_ProducesExpectedRotation()
    {
        var q = AnimationService.CreateBlockbenchQuaternion(new Vector3(90, 0, 0));

        var v = Vector3.Transform(Vector3.UnitY, q);

        Assert.True(Math.Abs(v.Z - 1f) < 0.0001f, $"Expected (0,0,1) but got {v}");
    }

    [Fact]
    public void CreateBlockbenchQuaternion_Y90_ProducesExpectedRotation()
    {
        var q = AnimationService.CreateBlockbenchQuaternion(new Vector3(0, 90, 0));

        var v = Vector3.Transform(Vector3.UnitX, q);

        Assert.True(Math.Abs(v.Z + 1f) < 0.0001f, $"Expected (0,0,-1) but got {v}");
    }

    [Fact]
    public void CreateBlockbenchQuaternion_NaN_ReturnsIdentity()
    {
        var q = AnimationService.CreateBlockbenchQuaternion(new Vector3(float.NaN, 0, 0));

        Assert.Equal(Quaternion.Identity, q);
    }

    [Fact]
    public void CreateBlockbenchQuaternion_Infinity_ReturnsIdentity()
    {
        var q = AnimationService.CreateBlockbenchQuaternion(new Vector3(float.PositiveInfinity, 0, 0));

        Assert.Equal(Quaternion.Identity, q);
    }
}
