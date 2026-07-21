@{
    # Every retained direct source is reviewed footage of an actual person.
    ReviewedExternal = @(
        55, 61, 62, 63, 65, 66, 92, 93, 104, 105, 107, 113, 114, 115, 116, 117,
        118, 119, 120, 121, 122, 123, 129, 130, 131, 132, 133, 135, 136, 138, 139, 140,
        141, 142, 145, 148, 150, 151, 154, 159, 160, 161, 168, 169, 170, 171, 173, 176,
        177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 190, 191, 192, 193,
        194, 195, 196, 197, 198, 199, 202, 204, 205, 206, 207, 208, 209, 210, 221, 226,
        237, 238, 318, 329, 348, 350, 367, 377, 379, 409, 422, 423, 425, 467, 474, 475,
        477, 481, 482, 483, 485, 488, 489, 490, 491, 492, 493, 494, 495, 497, 498, 499,
        500, 512, 513, 516, 572, 573, 575, 576, 577, 581, 582, 583, 588, 589, 590, 591,
        593, 608, 626, 666, 677, 678, 681, 683, 684, 685, 686, 687, 712, 745, 784, 816,
        818, 843, 845, 884, 885, 948, 969, 971, 985, 986, 987
    )

    # Synthetic, schematic, 3D, and anatomical demonstrations are excluded.
    ReviewedPosecode = @()
    PurposeBuiltSvg = @()

    # Reuse is allowed only when the reviewed source is human footage and the
    # target movement has identical mechanics.
    ReviewedExactCopies = @(
        153, 175, 247, 390, 510, 522, 523, 568, 569, 574, 578, 579, 580,
        602, 612, 614, 640, 643, 646, 698, 744, 774, 832, 970, 992, 994
    )

    # Direction transforms likewise retain an actual person in every frame.
    ReviewedExactTransforms = @(222, 949)
}
