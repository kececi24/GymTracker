namespace GymTracker.Api.Data;

public static class CatalogSeedData
{
    public static readonly IReadOnlyList<Exercise> Exercises =
    [
        // Legs (4)
        new()
        {
            Id = "barbell-back-squat",
            Name = "Barbell Back Squat",
            PrimaryMuscleGroup = "Legs",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Compound lower-body squat performed with a barbell across the upper back."
        },
        new()
        {
            Id = "romanian-deadlift",
            Name = "Romanian Deadlift",
            PrimaryMuscleGroup = "Legs",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Hip-hinge movement targeting the hamstrings and glutes."
        },
        new()
        {
            Id = "leg-press",
            Name = "Leg Press",
            PrimaryMuscleGroup = "Legs",
            Category = "Resistance",
            DefaultEquipment = "Machine",
            Description = "Machine-based compound knee and hip extension exercise."
        },
        new()
        {
            Id = "walking-lunge",
            Name = "Dumbbell Walking Lunge",
            PrimaryMuscleGroup = "Legs",
            Category = "Resistance",
            DefaultEquipment = "Dumbbell",
            Description = "Unilateral lower-body stride exercise for quadriceps and glute stability."
        },

        // Chest (4)
        new()
        {
            Id = "barbell-bench-press",
            Name = "Barbell Bench Press",
            PrimaryMuscleGroup = "Chest",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Horizontal pressing movement targeting the pectorals, anterior deltoids, and triceps."
        },
        new()
        {
            Id = "incline-dumbbell-press",
            Name = "Incline Dumbbell Press",
            PrimaryMuscleGroup = "Chest",
            Category = "Resistance",
            DefaultEquipment = "Dumbbell",
            Description = "Incline pressing exercise emphasizing the clavicular head of the pectorals."
        },
        new()
        {
            Id = "cable-chest-fly",
            Name = "Cable Chest Fly",
            PrimaryMuscleGroup = "Chest",
            Category = "Resistance",
            DefaultEquipment = "Cable",
            Description = "Horizontal adduction isolation movement for the chest."
        },
        new()
        {
            Id = "push-up",
            Name = "Push-Up",
            PrimaryMuscleGroup = "Chest",
            Category = "Bodyweight",
            DefaultEquipment = "Bodyweight",
            Description = "Bodyweight horizontal press for chest, shoulders, and trunk stability."
        },

        // Back (4)
        new()
        {
            Id = "conventional-deadlift",
            Name = "Conventional Deadlift",
            PrimaryMuscleGroup = "Back",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Full posterior-chain pull from the floor."
        },
        new()
        {
            Id = "barbell-bent-over-row",
            Name = "Barbell Bent-Over Row",
            PrimaryMuscleGroup = "Back",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Horizontal pulling exercise for the latissimus dorsi and upper back."
        },
        new()
        {
            Id = "lat-pulldown",
            Name = "Wide-Grip Lat Pulldown",
            PrimaryMuscleGroup = "Back",
            Category = "Resistance",
            DefaultEquipment = "Cable",
            Description = "Vertical pulling movement targeting the latissimus dorsi."
        },
        new()
        {
            Id = "pull-up",
            Name = "Pull-Up",
            PrimaryMuscleGroup = "Back",
            Category = "Bodyweight",
            DefaultEquipment = "Bodyweight",
            Description = "Bodyweight vertical pull targeting the lats and upper back."
        },

        // Shoulders (3)
        new()
        {
            Id = "overhead-barbell-press",
            Name = "Overhead Barbell Press",
            PrimaryMuscleGroup = "Shoulders",
            Category = "Resistance",
            DefaultEquipment = "Barbell",
            Description = "Standing vertical press for anterior and medial deltoids."
        },
        new()
        {
            Id = "dumbbell-lateral-raise",
            Name = "Dumbbell Lateral Raise",
            PrimaryMuscleGroup = "Shoulders",
            Category = "Resistance",
            DefaultEquipment = "Dumbbell",
            Description = "Shoulder abduction isolation exercise for the medial deltoid."
        },
        new()
        {
            Id = "face-pull",
            Name = "Rope Face Pull",
            PrimaryMuscleGroup = "Shoulders",
            Category = "Resistance",
            DefaultEquipment = "Cable",
            Description = "Upper-back and rear-deltoid pull for shoulder health and posture."
        },

        // Arms (3)
        new()
        {
            Id = "dumbbell-bicep-curl",
            Name = "Dumbbell Bicep Curl",
            PrimaryMuscleGroup = "Arms",
            Category = "Resistance",
            DefaultEquipment = "Dumbbell",
            Description = "Elbow flexion exercise targeting the biceps brachii."
        },
        new()
        {
            Id = "tricep-rope-pushdown",
            Name = "Tricep Rope Pushdown",
            PrimaryMuscleGroup = "Arms",
            Category = "Resistance",
            DefaultEquipment = "Cable",
            Description = "Cable elbow extension movement targeting the triceps."
        },
        new()
        {
            Id = "parallel-bar-dip",
            Name = "Parallel Bar Dip",
            PrimaryMuscleGroup = "Arms",
            Category = "Bodyweight",
            DefaultEquipment = "Bodyweight",
            Description = "Bodyweight pressing movement targeting the triceps and lower chest."
        },

        // Core (2)
        new()
        {
            Id = "hanging-leg-raise",
            Name = "Hanging Leg Raise",
            PrimaryMuscleGroup = "Core",
            Category = "Bodyweight",
            DefaultEquipment = "Bodyweight",
            Description = "Bodyweight hip and trunk flexion exercise for the anterior core."
        },
        new()
        {
            Id = "cable-woodchopper",
            Name = "Cable Woodchopper",
            PrimaryMuscleGroup = "Core",
            Category = "Resistance",
            DefaultEquipment = "Cable",
            Description = "Rotational resistance exercise for the obliques and trunk."
        }
    ];

    public static readonly IReadOnlyList<ProgramTemplate> Templates =
    [
        new()
        {
            Id = "full-body-foundation",
            Name = "Full Body Foundation",
            Description = "Balanced three-day full-body template covering major compound movements.",
            RecommendedExperienceLevel = "Beginner"
        },
        new()
        {
            Id = "upper-lower-strength",
            Name = "Upper / Lower Strength Split",
            Description = "Four-day upper and lower body strength progression program.",
            RecommendedExperienceLevel = "Intermediate"
        },
        new()
        {
            Id = "push-pull-legs-hypertrophy",
            Name = "Push / Pull / Legs Hypertrophy",
            Description = "Six-day split grouping pressing, pulling, and lower-body volume.",
            RecommendedExperienceLevel = "Advanced"
        }
    ];

    public static readonly IReadOnlyList<ProgramTemplateExercise> TemplateExercises =
    [
        // Template 1: Full Body Foundation
        new()
        {
            Id = 1,
            TemplateId = "full-body-foundation",
            ExerciseId = "barbell-back-squat",
            DisplayOrder = 1,
            TargetSets = 3,
            TargetReps = 8,
            TargetLoadKg = 40m,
            RestSeconds = 120
        },
        new()
        {
            Id = 2,
            TemplateId = "full-body-foundation",
            ExerciseId = "barbell-bench-press",
            DisplayOrder = 2,
            TargetSets = 3,
            TargetReps = 8,
            TargetLoadKg = 35m,
            RestSeconds = 120
        },
        new()
        {
            Id = 3,
            TemplateId = "full-body-foundation",
            ExerciseId = "barbell-bent-over-row",
            DisplayOrder = 3,
            TargetSets = 3,
            TargetReps = 10,
            TargetLoadKg = 30m,
            RestSeconds = 90
        },
        new()
        {
            Id = 4,
            TemplateId = "full-body-foundation",
            ExerciseId = "hanging-leg-raise",
            DisplayOrder = 4,
            TargetSets = 3,
            TargetReps = 12,
            TargetLoadKg = null,
            RestSeconds = 60
        },

        // Template 2: Upper / Lower Strength Split
        new()
        {
            Id = 5,
            TemplateId = "upper-lower-strength",
            ExerciseId = "barbell-bench-press",
            DisplayOrder = 1,
            TargetSets = 4,
            TargetReps = 6,
            TargetLoadKg = 60m,
            RestSeconds = 150
        },
        new()
        {
            Id = 6,
            TemplateId = "upper-lower-strength",
            ExerciseId = "overhead-barbell-press",
            DisplayOrder = 2,
            TargetSets = 3,
            TargetReps = 8,
            TargetLoadKg = 40m,
            RestSeconds = 120
        },
        new()
        {
            Id = 7,
            TemplateId = "upper-lower-strength",
            ExerciseId = "barbell-back-squat",
            DisplayOrder = 3,
            TargetSets = 4,
            TargetReps = 6,
            TargetLoadKg = 80m,
            RestSeconds = 150
        },
        new()
        {
            Id = 8,
            TemplateId = "upper-lower-strength",
            ExerciseId = "romanian-deadlift",
            DisplayOrder = 4,
            TargetSets = 3,
            TargetReps = 8,
            TargetLoadKg = 70m,
            RestSeconds = 120
        },

        // Template 3: Push / Pull / Legs Hypertrophy
        new()
        {
            Id = 9,
            TemplateId = "push-pull-legs-hypertrophy",
            ExerciseId = "incline-dumbbell-press",
            DisplayOrder = 1,
            TargetSets = 4,
            TargetReps = 10,
            TargetLoadKg = 24m,
            RestSeconds = 90
        },
        new()
        {
            Id = 10,
            TemplateId = "push-pull-legs-hypertrophy",
            ExerciseId = "lat-pulldown",
            DisplayOrder = 2,
            TargetSets = 4,
            TargetReps = 10,
            TargetLoadKg = 55m,
            RestSeconds = 90
        },
        new()
        {
            Id = 11,
            TemplateId = "push-pull-legs-hypertrophy",
            ExerciseId = "leg-press",
            DisplayOrder = 3,
            TargetSets = 4,
            TargetReps = 12,
            TargetLoadKg = 120m,
            RestSeconds = 120
        },
        new()
        {
            Id = 12,
            TemplateId = "push-pull-legs-hypertrophy",
            ExerciseId = "dumbbell-lateral-raise",
            DisplayOrder = 4,
            TargetSets = 3,
            TargetReps = 15,
            TargetLoadKg = 10m,
            RestSeconds = 60
        }
    ];
}
