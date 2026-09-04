@{
    # An abdominal secondary is a training claim, not a list of every movement
    # that asks the trunk to stay upright. Keep an ID here only when the full
    # 45-second block gives the abdominal wall a meaningful stimulus through
    # visible trunk motion, substantial anti-movement work, or deliberate
    # whole-body muscular posing.
    RubricVersion = 1

    DynamicTrunkWork = @(
        17, 59, 124, 125, 132, 174, 176, 182, 183, 219, 227, 305,
        394, 395, 408, 449, 470, 547, 548, 570, 577, 618, 625,
        801, 804, 825, 884, 885, 905, 917, 973, 998
    )

    HighTensionAntiMovement = @(
        177, 186, 292, 542
    )

    IntentionalWholeBodyPosing = @(
        524, 525, 526, 527, 790
    )
}
