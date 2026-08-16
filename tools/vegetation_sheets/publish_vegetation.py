"""Name and publish the curated vegetation library.

The extractor produces numbered raw crops. This step selects the ones worth
keeping, gives them meaningful names and files them under the directories the
world generator reads. Selection is keyed on the sprite's pixel dimensions
rather than its index, because index ordering shifts whenever the extractor is
retuned, while a sprite's size does not.

Curation is deliberate. The sheets contain more usable art than the world needs,
and scattering all of it produces noise; a smaller set used with good spatial
rules reads better than a larger set used randomly.
"""

from __future__ import annotations

import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]

# Rejected outright: these two crops each contain two overlapping trees with no
# separating gap, so they cannot be cut apart cleanly. There is ample variety
# without them.
REJECTED_TREES = {(248, 526), (198, 311)}

TREES: dict[tuple[int, int], str] = {
    (348, 436): "oak_large_01",
    (307, 422): "maple_large_01",
    (277, 434): "oak_autumn_large_01",
    (224, 440): "spruce_large_01",
    (201, 432): "birch_large_01",
    (193, 423): "pine_large_01",
    (244, 268): "blossom_medium_01",
    (224, 265): "maple_medium_01",
    (170, 316): "spruce_medium_01",
    (159, 285): "spruce_medium_02",
    (166, 283): "pine_medium_01",
    (162, 268): "birch_medium_01",
    (166, 231): "birch_autumn_medium_01",
    (159, 208): "maple_autumn_small_01",
    (108, 215): "spruce_small_01",
    (244, 330): "dead_oak_large_01",
    (173, 328): "dead_tree_medium_01",
    (188, 263): "snag_large_01",
    (163, 218): "snag_medium_01",
    (205, 96): "fallen_log_branches_01",
    (165, 73): "fallen_log_01",
    (170, 92): "deadfall_roots_01",
}

UNDERGROWTH: dict[tuple[int, int], str] = {
    # Flowering and berry bushes.
    (218, 173): "bush_white_flower_01",
    (180, 169): "bush_white_flower_02",
    (228, 227): "bush_white_flower_tall_01",
    (187, 165): "bush_berry_red_01",
    (195, 176): "bush_berry_red_02",
    (209, 208): "bush_berry_tall_01",
    (181, 168): "bush_blue_flower_01",
    (164, 153): "bush_berry_blue_01",
    (157, 136): "bush_autumn_01",
    (150, 129): "bush_autumn_02",
    (198, 145): "bush_green_01",
    (171, 134): "bush_green_02",
    (171, 139): "shrub_bare_red_01",
    (144, 123): "shrub_bare_01",
    # Ferns.
    (261, 193): "fern_large_01",
    (238, 173): "fern_large_02",
    (190, 141): "fern_01",
    (186, 152): "fern_02",
    # Grasses.
    (179, 193): "grass_pampas_01",
    (148, 173): "grass_pampas_02",
    (160, 151): "grass_tuft_01",
    (161, 231): "grass_seedheads_01",
    # Flower clusters.
    (114, 91): "flowers_yellow_01",
    (137, 103): "flowers_yellow_02",
    (136, 109): "flowers_yellow_03",
    (131, 110): "flowers_daisy_01",
    (160, 73): "flowers_daisy_02",
    (91, 69): "flowers_white_01",
    (128, 144): "flowers_lavender_01",
    (110, 146): "flowers_lavender_02",
    (93, 88): "flowers_pink_01",
    (117, 99): "flowers_blue_01",
    (150, 100): "flowers_mixed_01",
    (196, 87): "plant_creeper_01",
    (146, 116): "plant_autumn_01",
    (134, 94): "plant_leafy_06",
    (95, 93): "flowers_orange_01",
    # Low leafy plants.
    (154, 103): "plant_broadleaf_01",
    (136, 101): "plant_leafy_01",
    (102, 92): "plant_leafy_02",
    (100, 74): "plant_leafy_03",
    (78, 95): "plant_leafy_04",
    (65, 83): "plant_small_01",
    (102, 74): "plant_small_02",
    (63, 83): "plant_small_03",
    (65, 68): "plant_small_04",
    (123, 93): "plant_leafy_05",
    # Woodland floor.
    (261, 154): "stump_mossy_01",
    (205, 178): "log_mushroom_01",
    (296, 149): "log_mossy_01",
    (101, 104): "mushroom_cluster_01",
    (99, 106): "mushroom_cluster_02",
    (155, 114): "mushroom_cluster_03",
    (127, 72): "mushroom_red_01",
    (128, 150): "mushroom_red_02",
    (83, 72): "mushroom_brown_01",
    (109, 87): "mushroom_brown_02",
    (176, 97): "leaf_litter_01",
    (210, 135): "leaf_litter_02",
    (141, 70): "leaf_litter_03",
    (86, 92): "pinecones_01",
    # Rocks and stones.
    (98, 65): "rock_mossy_01",
    (171, 126): "rock_mossy_02",
    (192, 121): "rock_mossy_03",
    (121, 74): "rock_mossy_04",
    (134, 120): "rock_mossy_05",
    (120, 73): "rock_mossy_06",
    (101, 61): "rock_small_01",
    # Fallen branches and sticks.
    (292, 96): "branch_01",
    (224, 113): "branch_02",
    (226, 93): "branch_03",
    (179, 93): "branch_04",
    (222, 81): "branch_05",
    (191, 58): "branch_bundle_01",
    (156, 54): "branch_bundle_02",
    (193, 103): "branch_pile_01",
}


def publish(raw_dir: Path, out_dir: Path, names: dict[tuple[int, int], str],
            rejected: set[tuple[int, int]]) -> tuple[int, list[str]]:
    by_size: dict[tuple[int, int], list[Path]] = {}
    for path in sorted(raw_dir.glob("*.png")):
        by_size.setdefault(Image.open(path).size, []).append(path)

    out_dir.mkdir(parents=True, exist_ok=True)
    published = 0
    problems: list[str] = []
    for size, name in names.items():
        matches = by_size.get(size, [])
        if len(matches) != 1:
            problems.append(f"{name}: expected one {size[0]}x{size[1]} sprite, found {len(matches)}")
            continue
        shutil.copyfile(matches[0], out_dir / f"{name}.png")
        published += 1

    leftover = [f"{p.name} {Image.open(p).size}" for size, paths in by_size.items()
                for p in paths if size not in names and size not in rejected]
    return published, problems + ([f"unused: {len(leftover)}"] if leftover else [])


def main() -> None:
    trees, tree_problems = publish(
        ROOT / "assets/art/trees/_raw", ROOT / "assets/art/trees", TREES, REJECTED_TREES)
    under, under_problems = publish(
        ROOT / "assets/art/undergrowth/_raw", ROOT / "assets/art/undergrowth", UNDERGROWTH, set())
    print(f"trees published: {trees}/{len(TREES)}")
    for problem in tree_problems:
        print("  !", problem)
    print(f"undergrowth published: {under}/{len(UNDERGROWTH)}")
    for problem in under_problems:
        print("  !", problem)


if __name__ == "__main__":
    main()
