# Every retained catalog record receives one explicit scheduling decision:
# either it belongs to exactly one mandatory sequence below or it appears in
# StandaloneIds. Nothing silently defaults to a standalone exercise.
@{
    # Ordered exercise families that are valuable only when every block is
    # completed in one uninterrupted sequence. Each member expands to its own
    # reviewed 45-second side/direction blocks; Flux inserts 15 seconds between
    # blocks and records one outcome only after the final block.
    Sequences = @{
        '96' = @(96, 540)                 # Figure-four squat sides, then alternating integration
        '104' = @(104, 626)               # Sumo squat repetitions, then hold
        '113' = @(113, 135)               # Overhead squat repetitions, then hold
        '115' = @(115, 532)               # Pistol squat sides, then alternating integration
        '120' = @(120, 184)               # Split-squat pulses, then holds on both sides
        '123' = @(123, 117, 199)          # Lateral-lunge repetitions, holds, then side-to-side integration
        '143' = @(143, 538)               # Reverse-lunge/front-kick sides, then alternating integration
        '160' = @(160, 533)               # Standing donkey-kick sides, then alternating integration
        '177' = @(177, 186)               # Hip-airplane repetitions, then holds on both sides
        '178' = @(178, 535)               # Crescent-kick sides, then alternating integration
        '179' = @(179, 539)               # Hip-hinge rear-leg-raise sides, then alternating integration
        '180' = @(180, 534)               # Front-snap-kick sides, then alternating integration
        '181' = @(181, 536)               # Side-thrust-kick sides, then alternating integration
        '211' = @(211, 213)               # Wrist-flexion and wrist-extension stretches on both sides
        '214' = @(214, 755)               # Inward, then outward wrist circles on both sides
        '220' = @(220, 543)               # Rising-block sides, then alternating integration
        '223' = @(223, 756)               # Inward, then outward controlled wrist circles on both sides
        '252' = @(252, 253, 254)          # Three calf-raise foot angles
        '261' = @(261, 677)               # Reverse-fly repetitions, then hold
        '264' = @(264, 406)               # Synchronous arm circles, then windmill-style integration
        '285' = @(285, 541)               # Inside-block sides, then alternating integration
        '286' = @(286, 545)               # Outside-block sides, then alternating integration
        '288' = @(288, 758)               # Forward, then backward knee-and-ankle circles on both sides
        '291' = @(291, 294)               # Inward, then outward alternating knife-hand strikes
        '292' = @(292, 542)               # Standing-bird-dog sides, then alternating integration
        '302' = @(302, 304)               # Forward, then backward arm circles while marching
        '307' = @(307, 310)               # Self-resisted neck flexion, then extension
        '327' = @(327, 546)               # Horizontal-elbow-strike sides, then alternating integration
        '329' = @(329, 531)               # Shoulder-CAR sides, then alternating integration
        '367' = @(367, 529)               # Single-leg deadlift sides, then alternating integration
        '392' = @(392, 399, 400)          # Three complete breathing-squat patterns
        '393' = @(393, 537)               # Deadlift-to-runner-march sides, then alternating integration
        '414' = @(414, 418)               # Fixed-thumb sides, then alternating-thumb head turns
        '415' = @(415, 416)               # Fixed-thumb nod and tilt planes on both sides
        '420' = @(420, 421, 426)          # Jumping-, seal-, and cross-jack series
        '459' = @(459, 468, 469)          # Goalpost, scarecrow, and arm-circle jack series
        '465' = @(465, 445)               # Single-leg jump-rope sides, then changing-feet integration
        '491' = @(491, 501)               # Fixed-thumb gaze stabilization with nods and turns
        '500' = @(500, 505, 506)          # Jaw opening, side glide, and forward glide
        '502' = @(502, 503)               # Levator-scapulae and upper-trapezius stretches on both sides
        '566' = @(566, 581, 582)          # Parallel, toes-in, then toes-out calf raises
        '610' = @(610, 232)               # Warrior II, then extended side angle on both sides
        '612' = @(612, 530)               # Lateral-leg-lift sides, then alternating integration
        '617' = @(617, 620)               # Forward, then backward side-leg circles on both sides
        '742' = @(742, 338)               # Triceps stretch, then triceps stretch with side bend
        '784' = @(784, 969, 1000)         # Mountain, chair, and standing forward-fold holds
        '834' = @(834, 914)               # Diagonal knee-pull sides, then alternating integration
        '845' = @(845, 256)               # Overhead side-stretch repetitions, then holds
        '910' = @(910, 962)               # Pilates knee-lift-twist sides, then alternating integration
        '948' = @(948, 949)               # Wood chop, then reverse wood chop on both sides
        '996' = @(996, 997)               # Partial pistol-squat repetitions, then bottom holds on both sides
    }

    # These records were reviewed and deliberately remain complete one-member
    # scheduling units. Sequence roots and hidden members must not appear here.
    StandaloneIds = @(
        15, 16, 17, 19, 20, 21, 31, 32, 37, 41, 47, 56,
        58, 59, 60, 92, 93, 94, 95, 97, 98, 99, 100,
        101, 102, 103, 105, 107, 108, 109, 110, 111, 112, 114, 116,
        118, 119, 121, 122, 124, 125, 126, 127, 128, 129, 130, 131, 132,
        133, 136, 138, 139, 140, 141, 142, 144, 145, 146, 147,
        148, 150, 151, 152, 154, 156, 159, 161, 167, 168, 169, 170,
        171, 173, 174, 176, 182, 183, 185, 187, 188, 190, 191,
        192, 193, 194, 195, 196, 197, 198, 200, 201, 203, 212, 215,
        216, 217, 218, 219, 224, 225, 227, 228, 230, 231, 233, 234,
        236, 237, 238, 239, 240, 241, 242, 245, 246, 248, 251, 255,
        257, 258, 260, 262, 263, 265, 266, 268, 269, 270, 271, 272,
        273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284,
        287, 289, 290, 293, 295, 296, 301,
        303, 305, 308, 309, 311, 314, 315, 321, 326, 340, 341, 377, 379, 389,
        390, 391, 394, 395, 396, 397,
        398, 401, 402, 403, 404, 405, 407, 408, 409, 410, 411, 412,
        413, 417, 419, 422, 423, 424, 425, 427, 428, 429, 430, 431,
        432, 433, 434, 435, 436, 437, 438, 439, 440, 441, 442, 443,
        444, 446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
        457, 458, 460, 461, 462, 463, 464, 466, 467, 470, 471, 472,
        473, 474, 475, 476, 477, 478, 479, 480, 481, 482, 483, 484,
        485, 486, 487, 488, 489, 490, 492, 493, 494, 495, 496, 497,
        498, 499, 504, 507, 508, 509, 510, 511,
        512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522, 523,
        524, 525, 526, 527, 528, 544, 547, 548,
        549, 550, 551, 552, 554, 555, 556, 557,
        560, 561, 562, 563, 564, 565, 567, 568, 569, 570,
        571, 572, 573, 574, 575, 576, 577, 578, 583, 588, 591,
        608, 609, 611, 613, 614, 615, 616, 618, 619, 625, 632, 636,
        647, 648, 649, 654, 666, 678, 681, 683, 684, 685, 686, 687,
        712, 733, 740, 741, 743, 744, 745, 746, 747, 748, 750, 751,
        752, 804, 816, 818, 825, 831, 836, 843, 884, 885, 886, 887,
        905, 906, 915, 939, 943, 954,
        958, 960, 971, 973, 986, 987, 998, 999,
        134, 137, 149, 153, 162, 163, 165, 166, 172, 175,
        579, 580, 584, 585, 586, 587, 603, 633,
        701, 702, 703, 704, 790, 801, 835, 993
    )
}
