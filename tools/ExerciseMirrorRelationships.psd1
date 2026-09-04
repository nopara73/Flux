@{
    # The reflection is part of the exercise, not merely an optional form aid.
    # Coverage is the minimum mirror height needed to perform the exercise.
    MirrorOnlyByCoverage = @{
        # A compact mirror that shows roughly the upper body is sufficient.
        UpperBody = @(
            515, 520, 521, 522, 523, 993
        )

        # The whole body must be visible, so these require a tall mirror.
        FullBody = @(
            524, 525, 526, 527, 528, 790
        )
    }

    # Continuous live self-view must substantially change execution under one
    # of these narrow criteria. Ordinary optional form checking is deliberately
    # insufficient. The current count is an audit result, never a target, quota,
    # ceiling, or reason to promote or demote an otherwise valid exercise.
    BenefitsGreatlyByCriterion = @{
        # The reflection continuously exposes guard, chamber, stance, strike
        # path, or defensive path in a technique-sensitive martial movement.
        TechnicalMartialArts = @(
            92, 93, 97, 178, 180, 181, 182, 183, 204, 205, 220, 231, 245, 258,
            274, 276, 278, 279, 280, 283, 285, 286, 287, 291, 294, 326, 327, 404, 556, 591,
            534, 535, 536, 541, 543, 545, 546, 575, 578, 583, 681, 684, 685, 687,
            884, 885, 886, 887
        )

        # The reflection materially changes the intended whole-body line,
        # placement, or pose rather than merely making the movement observable.
        DanceAndAlignmentSensitivePoses = @(
            58, 105, 107, 108, 190, 217, 232, 260, 609, 610, 666,
            560, 561, 562, 905
        )

        # Live feedback reveals knee, pelvis, trunk, or free-limb alignment in
        # demanding single-leg work where the relevant drift is hard to feel.
        ComplexSingleLegAlignment = @(
            21, 96, 115, 177, 186, 292, 367, 393, 529, 532, 537, 540,
            542, 996, 997
        )

        # The movement's main quality depends on seeing a plane, path, or
        # left-right symmetry error while it is happening.
        LivePlaneOrSymmetryCorrection = @(
            246, 265, 268, 329, 481, 531, 917
        )

        # The reflection supplies an eye-level fixation target while also
        # making unintended trunk motion and the head-turn path visible.
        GazeStabilityFeedback = @(
            202
        )

        # The intended movement is a subtle pelvis/spine position change whose
        # accuracy is difficult to judge proprioceptively; a side reflection
        # makes the actual tilt continuously visible.
        SubtlePelvicPositionFeedback = @(
            916
        )
    }

    # This second axis records how much of the body must be visible for the
    # substantial benefit above. It must exactly partition the criterion list.
    BenefitsGreatlyByCoverage = @{
        UpperBody = @(
            202, 204, 205, 220, 231, 245, 246, 258, 265, 268, 274, 276, 278, 279, 280,
            283, 285, 286, 287, 291, 294, 326, 327, 329, 481, 531,
            541, 543, 545, 546, 556, 591, 681, 884, 887, 917
        )

        FullBody = @(
            21, 58, 92, 93, 96, 97, 105, 107, 108, 115, 177, 178,
            180, 181, 182, 183, 186, 190, 217, 232, 260, 292, 367,
            393, 404, 529, 532, 534, 535, 536, 537, 540, 542,
            560, 561, 562, 575, 578, 583, 609, 610, 666, 684, 685, 687, 885, 886, 905,
            916, 996, 997
        )
    }

    # A mirror supplies no more than optional, ordinary form checking. Seeing
    # oneself or being able to compare against the demo does not qualify by
    # itself.
    Agnostic = @(
        15, 16, 17, 19, 20, 31, 32, 37, 41, 47, 56, 59,
        60, 94, 95, 98, 99, 100, 101, 102, 103, 104, 109, 110, 111,
        112, 113, 114, 116, 117, 118, 119, 120, 121, 122, 123, 124,
        125, 126, 127, 128, 129, 130, 131, 132, 133, 135, 136, 138,
        139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 150, 151,
        152, 154, 156, 159, 160, 161, 167, 168, 169, 170, 171, 173,
        174, 176, 179, 184, 185, 187, 188, 191, 192, 193, 194, 195,
        196, 197, 198, 199, 200, 201, 203, 211, 212, 213, 214, 215,
        216, 218, 219, 223, 224, 225, 227, 228, 230, 233, 234, 236,
        237, 238, 239, 240, 241, 242, 248, 251, 252, 253, 254, 255,
        256, 257, 261, 262, 263, 264, 266, 269, 270, 271,
        272, 273, 275, 277, 281, 282, 284, 288, 289, 290,
        293, 295, 296, 301, 302, 303, 304, 305, 307, 308, 309, 310,
        311, 314, 315, 321, 338, 340,
        341, 377, 379, 389, 390, 391, 392, 394, 395, 396, 397, 398,
        399, 400, 401, 402, 403, 405, 406, 407, 408, 409, 410, 411,
        412, 413, 414, 415, 416, 417, 418, 419, 420, 421, 422, 423,
        424, 425, 426, 427, 428, 429, 430, 431, 432, 433, 434, 435,
        436, 437, 438, 439, 440, 441, 442, 443, 444, 445, 446, 447,
        448, 449, 450, 451, 452, 453, 454, 455, 456, 457, 458, 459,
        460, 461, 462, 463, 464, 465, 466, 467, 468, 469, 470, 471,
        472, 473, 474, 475, 476, 477, 478, 479, 480, 482, 483, 484,
        485, 486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496,
        497, 498, 499, 500, 501, 502, 503, 504, 505, 506, 507, 508,
        509, 510, 511, 512, 513, 514, 516, 517, 518, 519, 530, 533, 538, 539, 544, 572,
        547, 548, 549, 550, 551, 552, 554, 555, 557,
        563, 564, 565, 566, 567, 568, 569, 570, 571, 573, 574,
        576, 577, 581, 582, 588, 608,
        611, 612, 613, 614, 615, 616, 617, 618, 619, 620, 625, 626,
        632, 636, 647, 648, 649, 654, 677, 678, 683, 686, 712, 733,
        740, 741, 742, 743, 744, 745, 746, 747, 748, 750, 751, 752,
        755, 756, 758, 784, 804,
        816, 818, 825, 831, 834, 836, 843, 845, 906, 910, 911, 913, 914,
        915, 918, 919, 939, 943, 948, 949, 954, 958, 960, 962, 969, 971, 973,
        986, 987, 998, 999, 1000,
        134, 137, 149, 153, 162, 163, 165, 166, 172, 175,
        579, 580, 584, 585, 586, 587, 603, 633,
        701, 702, 703, 704, 801, 835
    )
}
