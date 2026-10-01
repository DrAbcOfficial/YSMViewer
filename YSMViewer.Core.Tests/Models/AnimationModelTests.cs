using System.Text;
using System.Text.Json;
using YSMViewer.Models;

namespace YSMViewer.Core.Tests.Models;

/// <summary>
/// Playback-parameter parsing on <see cref="MinecraftAnimation"/>:
/// blend_weight/start_delay/loop_delay accept numbers and MoLang strings,
/// and a MoLang blend_weight must not break whole-file deserialization
/// (it used to be a hard float property).
/// </summary>
public sealed class AnimationModelTests
{
    [Fact]
    public void PlaybackParams_AcceptNumbersAndMolangStrings()
    {
        var json = """
            {
              "format_version": "1.8.0",
              "animations": {
                "animation.test.weighted": {
                  "animation_length": 1,
                  "blend_weight": "query.health < 10 ? 0.5 : 1",
                  "bones": { "b": { "rotation": [0, 90, 0] } }
                },
                "animation.test.delayed": {
                  "animation_length": 2,
                  "loop": true,
                  "start_delay": 0.25,
                  "loop_delay": "q.some_delay",
                  "anim_time_update": "query.anim_time + query.delta_time * 2",
                  "bones": { "b": { "position": [1, 2, 3] } }
                },
                "animation.test.numeric_weight": {
                  "animation_length": 1,
                  "blend_weight": 0.5,
                  "bones": { "b": { "scale": 2 } }
                }
              }
            }
            """;

        var file = JsonSerializer.Deserialize(
            Encoding.UTF8.GetBytes(json), YsmJsonContext.Default.MinecraftAnimationFile);

        Assert.NotNull(file);

        var weighted = file.Animations["animation.test.weighted"];
        Assert.Equal("query.health < 10 ? 0.5 : 1", weighted.BlendWeightExpression);
        Assert.Equal(1f, weighted.BlendWeight);

        var delayed = file.Animations["animation.test.delayed"];
        Assert.Equal(0.25f, delayed.StartDelay);
        Assert.Null(delayed.StartDelayExpression);
        Assert.Equal("q.some_delay", delayed.LoopDelayExpression);
        Assert.Equal(0f, delayed.LoopDelay);
        Assert.Equal("query.anim_time + query.delta_time * 2", delayed.AnimTimeUpdate);

        var numeric = file.Animations["animation.test.numeric_weight"];
        Assert.Equal(0.5f, numeric.BlendWeight);
        Assert.Null(numeric.BlendWeightExpression);
    }
}
