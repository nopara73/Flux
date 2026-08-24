namespace Flux.Data;

internal static class ExerciseDatabaseMigrationSql
{
    internal const string CopyExistingExercisesWithNeutralMirrorMetadata =
        """
        INSERT INTO exercises_v60 (
            id, name, video, practice, motion_profile, score,
            muscular_demand, only_feet_touch_ground, shoe_agnostic,
            max_space_meters,
            equipment, silent, exercise_mode, presentation,
            hold_frame_percent, side_sequence, direction_sequence,
            direction_partner_exercise_id,
            insect_compatibility, mirror_relationship, mirror_coverage,
            session_movement_id)
        SELECT
            id, name, video, practice, motion_profile, score, 0,
            only_feet_touch_ground, shoe_agnostic,
            CASE WHEN max_space_meters BETWEEN 1 AND 2
                THEN max_space_meters ELSE 2 END,
            'None', silent, exercise_mode, presentation,
            hold_frame_percent, side_sequence, direction_sequence, 0,
            insect_compatibility, 'Unreviewed', 'None', 0
        FROM exercises
        """;
}
