#!/usr/bin/env python
"""Cut separated sprites from transparent PNG sheets.

The tool treats non-transparent alpha pixels as foreground, finds connected
components, and exports each component as an individual PNG.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFont


IMAGE_EXTENSIONS = {".png"}


@dataclass
class Component:
    label: int
    area: int
    bbox: tuple[int, int, int, int]  # left, top, right_exclusive, bottom_exclusive
    pixels: list[int]

    @property
    def width(self) -> int:
        return self.bbox[2] - self.bbox[0]

    @property
    def height(self) -> int:
        return self.bbox[3] - self.bbox[1]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Split transparent PNG sprite sheets into individual images.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument(
        "inputs",
        nargs="+",
        help="PNG files or folders. Folders process PNG files inside them.",
    )
    parser.add_argument("-o", "--output", default="cut_output", help="Output folder.")
    parser.add_argument("--recursive", action="store_true", help="Scan folders recursively.")
    parser.add_argument("--alpha", type=int, default=8, help="Foreground alpha threshold, 0-255.")
    parser.add_argument("--min-area", type=int, default=32, help="Ignore components smaller than this pixel area.")
    parser.add_argument("--padding", type=int, default=0, help="Extra transparent pixels around each exported sprite.")
    parser.add_argument(
        "--connectivity",
        type=int,
        choices=(4, 8),
        default=8,
        help="Pixel connectivity used to decide whether pixels belong to the same sprite.",
    )
    parser.add_argument(
        "--open",
        dest="open_radius",
        type=int,
        default=0,
        help="Morphological open radius. Useful for breaking thin alpha bridges; 0 disables it.",
    )
    parser.add_argument(
        "--merge-small",
        type=int,
        default=0,
        help="Merge components smaller than this area into the nearest larger component before export; 0 disables it.",
    )
    parser.add_argument("--prefix", default="", help="Optional filename prefix for exported sprites.")
    parser.add_argument("--json", action="store_true", help="Write a manifest JSON next to exported sprites.")
    parser.add_argument("--debug", action="store_true", help="Write a numbered preview image with component boxes.")
    parser.add_argument("--dry-run", action="store_true", help="Analyze and print results without writing sprite PNGs.")
    parser.add_argument("--overwrite", action="store_true", help="Allow overwriting existing output PNG files.")
    return parser.parse_args()


def collect_inputs(paths: Iterable[str], recursive: bool) -> list[Path]:
    images: list[Path] = []
    for raw_path in paths:
        path = Path(raw_path).expanduser()
        if path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS:
            images.append(path)
        elif path.is_dir():
            pattern = "**/*.png" if recursive else "*.png"
            images.extend(sorted(p for p in path.glob(pattern) if p.is_file()))
        else:
            print(f"[WARN] Skip unsupported input: {path}", file=sys.stderr)
    return sorted(dict.fromkeys(images))


def safe_name(text: str) -> str:
    text = re.sub(r"[^\w.-]+", "_", text, flags=re.UNICODE).strip("._")
    return text or "image"


def make_mask(alpha: Image.Image, threshold: int) -> bytearray:
    threshold = max(0, min(255, threshold))
    data = alpha.tobytes()
    return bytearray(1 if value > threshold else 0 for value in data)


def erode(mask: bytearray, width: int, height: int, radius: int) -> bytearray:
    if radius <= 0:
        return mask
    out = bytearray(width * height)
    for y in range(height):
        y0 = max(0, y - radius)
        y1 = min(height - 1, y + radius)
        for x in range(width):
            x0 = max(0, x - radius)
            x1 = min(width - 1, x + radius)
            keep = True
            for ny in range(y0, y1 + 1):
                row = ny * width
                for nx in range(x0, x1 + 1):
                    if mask[row + nx] == 0:
                        keep = False
                        break
                if not keep:
                    break
            out[y * width + x] = 1 if keep else 0
    return out


def dilate(mask: bytearray, width: int, height: int, radius: int) -> bytearray:
    if radius <= 0:
        return mask
    out = bytearray(width * height)
    for y in range(height):
        y0 = max(0, y - radius)
        y1 = min(height - 1, y + radius)
        for x in range(width):
            x0 = max(0, x - radius)
            x1 = min(width - 1, x + radius)
            hit = False
            for ny in range(y0, y1 + 1):
                row = ny * width
                for nx in range(x0, x1 + 1):
                    if mask[row + nx]:
                        hit = True
                        break
                if hit:
                    break
            out[y * width + x] = 1 if hit else 0
    return out


def morphological_open(mask: bytearray, width: int, height: int, radius: int) -> bytearray:
    if radius <= 0:
        return mask
    return dilate(erode(mask, width, height, radius), width, height, radius)


def find_components(mask: bytearray, width: int, height: int, connectivity: int) -> list[Component]:
    visited = bytearray(width * height)
    components: list[Component] = []
    if connectivity == 4:
        neighbors = ((1, 0), (-1, 0), (0, 1), (0, -1))
    else:
        neighbors = (
            (1, 0),
            (-1, 0),
            (0, 1),
            (0, -1),
            (1, 1),
            (1, -1),
            (-1, 1),
            (-1, -1),
        )

    label = 0
    for start in range(width * height):
        if mask[start] == 0 or visited[start]:
            continue

        label += 1
        queue: deque[int] = deque([start])
        visited[start] = 1
        pixels: list[int] = []
        min_x = width
        min_y = height
        max_x = -1
        max_y = -1

        while queue:
            idx = queue.pop()
            pixels.append(idx)
            x = idx % width
            y = idx // width
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)

            for dx, dy in neighbors:
                nx = x + dx
                ny = y + dy
                if nx < 0 or nx >= width or ny < 0 or ny >= height:
                    continue
                nidx = ny * width + nx
                if mask[nidx] and not visited[nidx]:
                    visited[nidx] = 1
                    queue.append(nidx)

        components.append(Component(label, len(pixels), (min_x, min_y, max_x + 1, max_y + 1), pixels))

    return components


def bbox_center(component: Component) -> tuple[float, float]:
    left, top, right, bottom = component.bbox
    return ((left + right) / 2, (top + bottom) / 2)


def merge_small_components(components: list[Component], merge_area: int) -> list[Component]:
    if merge_area <= 0:
        return components

    large = [c for c in components if c.area >= merge_area]
    small = [c for c in components if c.area < merge_area]
    if not large:
        return components

    for component in small:
        cx, cy = bbox_center(component)
        nearest = min(
            large,
            key=lambda target: math.dist((cx, cy), bbox_center(target)),
        )
        nearest.pixels.extend(component.pixels)
        nearest.area += component.area
        l1, t1, r1, b1 = nearest.bbox
        l2, t2, r2, b2 = component.bbox
        nearest.bbox = (min(l1, l2), min(t1, t2), max(r1, r2), max(b1, b2))

    return large


def filter_and_sort_components(components: list[Component], min_area: int) -> list[Component]:
    filtered = [c for c in components if c.area >= min_area]
    filtered.sort(key=lambda c: (c.bbox[1], c.bbox[0], -c.area))
    for idx, component in enumerate(filtered, start=1):
        component.label = idx
    return filtered


def component_pixel_set(component: Component, width: int, crop_box: tuple[int, int, int, int]) -> set[int]:
    left, top, right, bottom = crop_box
    crop_width = right - left
    pixels: set[int] = set()
    for idx in component.pixels:
        x = idx % width
        y = idx // width
        if left <= x < right and top <= y < bottom:
            pixels.add((y - top) * crop_width + (x - left))
    return pixels


def padded_box(
    bbox: tuple[int, int, int, int],
    image_width: int,
    image_height: int,
    padding: int,
) -> tuple[int, int, int, int]:
    left, top, right, bottom = bbox
    return (
        max(0, left - padding),
        max(0, top - padding),
        min(image_width, right + padding),
        min(image_height, bottom + padding),
    )


def export_component(
    image: Image.Image,
    component: Component,
    source_width: int,
    crop_box: tuple[int, int, int, int],
    output_path: Path,
    overwrite: bool,
) -> None:
    if output_path.exists() and not overwrite:
        raise FileExistsError(f"Output exists, use --overwrite: {output_path}")

    crop = image.crop(crop_box).convert("RGBA")
    data = bytearray(crop.tobytes())
    keep_pixels = component_pixel_set(component, source_width, crop_box)
    pixel_count = crop.width * crop.height
    for pixel_index in range(pixel_count):
        if pixel_index not in keep_pixels:
            data[pixel_index * 4 + 3] = 0
    crop = Image.frombytes("RGBA", crop.size, bytes(data))
    crop.save(output_path)


def draw_debug(
    image: Image.Image,
    components: list[Component],
    output_path: Path,
    padding: int,
) -> None:
    preview = image.convert("RGBA")
    overlay = Image.new("RGBA", preview.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    font = ImageFont.load_default()
    palette = [
        (255, 68, 68, 210),
        (69, 170, 242, 210),
        (38, 222, 129, 210),
        (254, 211, 48, 210),
        (165, 94, 234, 210),
        (250, 130, 49, 210),
    ]
    for component in components:
        color = palette[(component.label - 1) % len(palette)]
        left, top, right, bottom = padded_box(component.bbox, image.width, image.height, padding)
        draw.rectangle((left, top, right - 1, bottom - 1), outline=color, width=2)
        text = str(component.label)
        text_box = draw.textbbox((left + 3, top + 3), text, font=font)
        draw.rectangle(text_box, fill=(0, 0, 0, 180))
        draw.text((left + 3, top + 3), text, fill=color, font=font)
    Image.alpha_composite(preview, overlay).save(output_path)


def process_image(path: Path, output_root: Path, args: argparse.Namespace) -> dict:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    mask = make_mask(alpha, args.alpha)
    mask = morphological_open(mask, image.width, image.height, args.open_radius)
    components = find_components(mask, image.width, image.height, args.connectivity)
    components = merge_small_components(components, args.merge_small)
    components = filter_and_sort_components(components, args.min_area)

    image_output_dir = output_root / safe_name(path.stem)
    manifest_items = []
    if not args.dry_run:
        image_output_dir.mkdir(parents=True, exist_ok=True)

    for component in components:
        crop_box = padded_box(component.bbox, image.width, image.height, args.padding)
        filename = f"{safe_name(args.prefix + path.stem)}_{component.label:03d}.png"
        output_path = image_output_dir / filename
        if not args.dry_run:
            export_component(image, component, image.width, crop_box, output_path, args.overwrite)
        manifest_items.append(
            {
                "label": component.label,
                "file": str(output_path),
                "bbox": {
                    "x": crop_box[0],
                    "y": crop_box[1],
                    "width": crop_box[2] - crop_box[0],
                    "height": crop_box[3] - crop_box[1],
                },
                "raw_bbox": {
                    "x": component.bbox[0],
                    "y": component.bbox[1],
                    "width": component.width,
                    "height": component.height,
                },
                "area": component.area,
            }
        )

    if not args.dry_run and args.debug:
        draw_debug(image, components, image_output_dir / f"{safe_name(path.stem)}_debug.png", args.padding)

    result = {
        "source": str(path),
        "size": {"width": image.width, "height": image.height},
        "count": len(components),
        "output_dir": str(image_output_dir),
        "sprites": manifest_items,
    }
    if not args.dry_run and args.json:
        with (image_output_dir / f"{safe_name(path.stem)}_manifest.json").open("w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
    return result


def main() -> int:
    args = parse_args()
    if args.alpha < 0 or args.alpha > 255:
        print("[ERROR] --alpha must be between 0 and 255.", file=sys.stderr)
        return 2
    if args.min_area < 1:
        print("[ERROR] --min-area must be >= 1.", file=sys.stderr)
        return 2
    if args.padding < 0 or args.open_radius < 0:
        print("[ERROR] --padding and --open must be >= 0.", file=sys.stderr)
        return 2

    images = collect_inputs(args.inputs, args.recursive)
    if not images:
        print("[ERROR] No PNG files found.", file=sys.stderr)
        return 1

    output_root = Path(args.output).expanduser()
    if not args.dry_run:
        output_root.mkdir(parents=True, exist_ok=True)

    all_results = []
    for image_path in images:
        try:
            result = process_image(image_path, output_root, args)
            all_results.append(result)
            print(f"[OK] {image_path} -> {result['count']} sprites")
        except Exception as exc:  # noqa: BLE001 - CLI should continue with remaining batch files.
            print(f"[ERROR] {image_path}: {exc}", file=sys.stderr)

    if args.json and not args.dry_run:
        with (output_root / "manifest_all.json").open("w", encoding="utf-8") as f:
            json.dump(all_results, f, ensure_ascii=False, indent=2)

    return 0 if all_results else 1


if __name__ == "__main__":
    raise SystemExit(main())
