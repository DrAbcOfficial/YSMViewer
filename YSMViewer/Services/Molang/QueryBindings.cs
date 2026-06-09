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
            ["delta_time"] = _ => service.SafeGetUserOrAnimVar("delta_time"),
            ["life_time"] = _ => service.SafeGetUserVar("life_time"),
            ["frame_time"] = _ => service.SafeGetUserOrAnimVar("frame_time"),
            ["anim_time_scale"] = _ => service.SafeGetUserVar("anim_time_scale", 1.0),

            ["all_animations_finished"] = _ => service.SafeGetUserVar("all_animations_finished"),
            ["any_animation_finished"] = _ => service.SafeGetUserVar("any_animation_finished"),

            ["ground_speed"] = _ => service.SafeGetUserVar("ground_speed"),
            ["vertical_speed"] = _ => service.SafeGetUserVar("vertical_speed"),
            ["yaw_speed"] = _ => service.SafeGetUserVar("yaw_speed"),
            ["modified_distance_moved"] = _ => service.SafeGetUserVar("modified_distance_moved"),
            ["walk_distance"] = _ => service.SafeGetUserVar("walk_distance"),
            ["distance_from_camera"] = _ => service.SafeGetUserVar("distance_from_camera"),
            ["cardinal_facing_2d"] = _ => service.SafeGetUserVar("cardinal_facing_2d"),

            ["position"] = p =>
            {
                int axis = p.Contains(0) ? p.GetInt(0) : 0;
                return axis switch
                {
                    0 => service.SafeGetUserVar("position_x"),
                    1 => service.SafeGetUserVar("position_y"),
                    2 => service.SafeGetUserVar("position_z"),
                    _ => 0.0,
                };
            },
            ["rotation_to_camera"] = p =>
            {
                int axis = p.Contains(0) ? p.GetInt(0) : 0;
                return axis == 0
                    ? service.SafeGetUserVar("camera_rotation_x")
                    : service.SafeGetUserVar("camera_rotation_y");
            },

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
            ["is_in_water"] = _ => service.SafeGetUserVar("is_in_water"),
            ["is_in_water_or_rain"] = _ => service.SafeGetUserVar("is_in_water_or_rain"),
            ["is_on_fire"] = _ => service.SafeGetUserVar("is_on_fire"),
            ["is_riding"] = _ => service.SafeGetUserVar("is_riding"),
            ["is_spectator"] = _ => service.SafeGetUserVar("is_spectator"),
            ["is_first_person"] = _ => service.SafeGetUserVar("is_first_person"),
            ["is_jumping"] = _ => service.SafeGetUserVar("is_jumping"),
            ["is_eating"] = _ => service.SafeGetUserVar("is_eating"),
            ["is_playing_dead"] = _ => service.SafeGetUserVar("is_playing_dead"),
            ["has_rider"] = _ => service.SafeGetUserVar("has_rider"),
            ["has_cape"] = _ => service.SafeGetUserVar("has_cape"),

            ["health"] = _ => service.SafeGetUserVar("health", 20.0),
            ["max_health"] = _ => service.SafeGetUserVar("max_health", 20.0),
            ["armor_value"] = _ => service.SafeGetUserVar("armor_value"),
            ["modified_move_speed"] = _ => service.SafeGetUserVar("modified_move_speed", 1.0),

            ["head_x_rotation"] = _ => service.SafeGetUserVar("head_x_rotation"),
            ["head_y_rotation"] = _ => service.SafeGetUserVar("head_y_rotation"),
            ["body_x_rotation"] = _ => service.SafeGetUserVar("body_x_rotation"),
            ["body_y_rotation"] = _ => service.SafeGetUserVar("body_y_rotation"),
            ["eye_target_x_rotation"] = _ => service.SafeGetUserVar("eye_target_x_rotation"),
            ["eye_target_y_rotation"] = _ => service.SafeGetUserVar("eye_target_y_rotation"),

            ["input_vertical"] = _ => service.SafeGetUserVar("input_vertical"),
            ["input_horizontal"] = _ => service.SafeGetUserVar("input_horizontal"),

            ["has_helmet"] = _ => service.SafeGetUserVar("has_helmet"),
            ["has_chestplate"] = _ => service.SafeGetUserVar("has_chestplate"),
            ["has_leggings"] = _ => service.SafeGetUserVar("has_leggings"),
            ["has_boots"] = _ => service.SafeGetUserVar("has_boots"),
            ["has_elytra"] = _ => service.SafeGetUserVar("has_elytra"),
            ["has_offhand"] = _ => service.SafeGetUserVar("has_offhand"),
            ["equipment_count"] = _ => service.SafeGetUserVar("equipment_count"),

            ["hurt_time"] = _ => service.SafeGetUserVar("hurt_time"),
            ["death_time"] = _ => service.SafeGetUserVar("death_time"),
            ["swing_time"] = _ => service.SafeGetUserVar("swing_time"),
            ["use_time"] = _ => service.SafeGetUserVar("use_time"),
            ["item_in_use_duration"] = _ => service.SafeGetUserVar("item_in_use_duration"),
            ["item_max_use_duration"] = _ => service.SafeGetUserVar("item_max_use_duration"),
            ["item_remaining_use_duration"] = _ => service.SafeGetUserVar("item_remaining_use_duration"),

            ["cape_flap_amount"] = _ => service.SafeGetUserVar("cape_flap_amount"),
            ["player_level"] = _ => service.SafeGetUserVar("player_level"),

            ["time_of_day"] = _ => service.SafeGetUserVar("time_of_day"),
            ["moon_phase"] = _ => service.SafeGetUserVar("moon_phase"),
            ["time_stamp"] = _ => service.SafeGetUserVar("time_stamp"),
            ["actor_count"] = _ => service.SafeGetUserVar("actor_count"),

            ["debug_output"] = p =>
            {
                var parts = new List<string>();
                for (int i = 0; p.Contains(i); i++)
                    parts.Add(p.Get(i).AsString());
                System.Diagnostics.Debug.WriteLine($"[Molang] {string.Join(", ", parts)}");
                return 0.0;
            },
        };

        return new QueryStruct(functions);
    }
}