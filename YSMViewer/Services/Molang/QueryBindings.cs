using ConcreteMC.MolangSharp.Runtime;
using ConcreteMC.MolangSharp.Runtime.Struct;

namespace YSMViewer.Services.Molang;

internal static class QueryBindings
{
    public static QueryStruct CreateQueryStruct(MolangService service)
    {
        var functions = new Dictionary<string, Func<MoParams, object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anim_time"] = _ => service.SafeGetUserOrAnimVar("anim_time"),
            ["life_time"] = _ => service.SafeGetUserVar("life_time"),
            ["frame_time"] = _ => service.SafeGetUserOrAnimVar("frame_time"),
            ["anim_time_scale"] = _ => service.SafeGetUserVar("anim_time_scale", 1.0),
            ["ground_speed"] = _ => service.SafeGetUserVar("ground_speed"),
            ["vertical_speed"] = _ => service.SafeGetUserVar("vertical_speed"),
            ["is_on_ground"] = _ => service.SafeGetUserVar("is_on_ground"),
            ["is_moving"] = _ => service.SafeGetUserVar("is_moving"),
            ["is_sneaking"] = _ => service.SafeGetUserVar("is_sneaking"),
            ["is_sleeping"] = _ => service.SafeGetUserVar("is_sleeping"),
            ["is_swimming"] = _ => service.SafeGetUserVar("is_swimming"),
            ["is_flying"] = _ => service.SafeGetUserVar("is_flying"),
            ["is_gliding"] = _ => service.SafeGetUserVar("is_gliding"),
            ["is_sprinting"] = _ => service.SafeGetUserVar("is_sprinting"),
            ["is_blocking"] = _ => service.SafeGetUserVar("is_blocking"),
            ["is_using_item"] = _ => service.SafeGetUserVar("is_using_item"),
            ["health"] = _ => service.SafeGetUserVar("health", 20.0),
            ["max_health"] = _ => service.SafeGetUserVar("max_health", 20.0),
            ["armor_value"] = _ => service.SafeGetUserVar("armor_value"),
            ["modified_move_speed"] = _ => service.SafeGetUserVar("modified_move_speed", 1.0),
            ["head_x_rotation"] = _ => service.SafeGetUserVar("head_x_rotation"),
            ["head_y_rotation"] = _ => service.SafeGetUserVar("head_y_rotation"),
            ["body_x_rotation"] = _ => service.SafeGetUserVar("body_x_rotation"),
            ["body_y_rotation"] = _ => service.SafeGetUserVar("body_y_rotation"),
            ["input_vertical"] = _ => service.SafeGetUserVar("input_vertical"),
            ["input_horizontal"] = _ => service.SafeGetUserVar("input_horizontal"),
            ["has_helmet"] = _ => service.SafeGetUserVar("has_helmet"),
            ["has_chestplate"] = _ => service.SafeGetUserVar("has_chestplate"),
            ["has_leggings"] = _ => service.SafeGetUserVar("has_leggings"),
            ["has_boots"] = _ => service.SafeGetUserVar("has_boots"),
            ["has_elytra"] = _ => service.SafeGetUserVar("has_elytra"),
            ["has_offhand"] = _ => service.SafeGetUserVar("has_offhand"),
            ["hurt_time"] = _ => service.SafeGetUserVar("hurt_time"),
            ["death_time"] = _ => service.SafeGetUserVar("death_time"),
            ["swing_time"] = _ => service.SafeGetUserVar("swing_time"),
            ["use_time"] = _ => service.SafeGetUserVar("use_time"),
        };

        return new QueryStruct(functions);
    }
}