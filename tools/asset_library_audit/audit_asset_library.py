"""Warn about suspicious authoring assets without deleting or modifying them."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "assets/art"
IGNORED = ("/sheets/", "/characters/", "/ui/", "/zombies/")


def inspect(path: Path) -> dict:
    image = np.asarray(Image.open(path).convert("RGBA"))
    alpha = image[:, :, 3]
    labels, count = ndimage.label(alpha >= 32)
    sizes = sorted((int((labels == label).sum()) for label in range(1, count + 1)), reverse=True)
    occupied = int((alpha > 0).sum())
    border = int((alpha[0] > 0).sum() + (alpha[-1] > 0).sum() + (alpha[:, 0] > 0).sum() + (alpha[:, -1] > 0).sum())
    warnings = []
    if count > 12:
        warnings.append("many_alpha_islands")
    if len(sizes) > 1 and sum(size < max(8, sizes[0] * .01) for size in sizes[1:]) > 6:
        warnings.append("many_tiny_islands")
    if border:
        warnings.append("touches_crop_boundary")
    if max(image.shape[:2]) / max(1, min(image.shape[:2])) > 5:
        warnings.append("extreme_aspect_ratio")
    if occupied and occupied / (image.shape[0] * image.shape[1]) < .025:
        warnings.append("excessive_empty_canvas")
    return {"path": path.relative_to(ROOT).as_posix(), "width": int(image.shape[1]), "height": int(image.shape[0]), "alpha_islands": count, "warnings": warnings}


def main() -> None:
    assets = []
    for path in ART.rglob("*.png"):
        normalized = "/" + path.relative_to(ROOT).as_posix()
        if any(part in normalized for part in IGNORED) or "sheet" in path.name.lower() or path.name in {"county_ground.png", "ashwood_outskirts_ground.png"}:
            continue
        assets.append(inspect(path))
    report = {"asset_count": len(assets), "warning_count": sum(bool(asset["warnings"]) for asset in assets), "assets": assets}
    destination = ROOT / "docs/asset_library_sanity_report.json"
    destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Audited {len(assets)} assets; {report['warning_count']} have heuristic warnings. Report: {destination}")


if __name__ == "__main__":
    main()
