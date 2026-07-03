# ImageCut Tool

ImageCut Tool is a local command-line sprite cutter for transparent PNG sheets.
It finds separated foreground regions from the alpha channel and exports each
region as an individual PNG.

The tool is designed for AI/batch workflows:

- Process one PNG, many PNG files, or a folder.
- Cut by alpha connected components instead of overlapping rectangles.
- Export each sprite with its own mask, so nearby sprites do not leak into each other.
- Optional debug preview with numbered bounding boxes.
- Optional JSON manifests for downstream automation.

## Install

Python 3.10+ is recommended.

```powershell
cd D:\Sourcetree\AI_Friendly_Project\Tools\ImageCut_Tool
python -m pip install -r requirements.txt
```

## Basic Commands

Cut one PNG:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut
```

Cut one PNG with transparent padding around each sprite:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --padding 2
```

Cut all PNG files in a folder:

```powershell
python image_cut.py D:\Art\sheets -o D:\Art\cut
```

Cut all PNG files in a folder recursively:

```powershell
python image_cut.py D:\Art\sheets -o D:\Art\cut --recursive
```

Cut multiple files:

```powershell
python image_cut.py D:\Art\a.png D:\Art\b.png D:\Art\c.png -o D:\Art\cut
```

Generate debug preview images and JSON manifests:

```powershell
python image_cut.py D:\Art\sheets -o D:\Art\cut --debug --json
```

Analyze without writing sprite PNGs:

```powershell
python image_cut.py D:\Art\sheet.png --dry-run
```

## Common Quality Parameters

Use a higher alpha threshold to ignore faint transparent shadows or tiny bridges:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --alpha 24
```

Ignore small noise:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --min-area 80
```

Try to break very thin alpha connections between nearby assets:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --alpha 16 --open 1
```

Merge tiny fragments back into the nearest larger sprite:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --merge-small 120
```

Use 4-neighbor connectivity for stricter separation of diagonal-touching pixels:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --connectivity 4
```

Overwrite existing output files:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --overwrite
```

## Recommended Presets

Clean transparent sprite sheets:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --alpha 8 --min-area 32 --padding 2 --debug --json
```

Sheets with faint shadows or soft alpha edges:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --alpha 24 --min-area 80 --padding 2 --debug --json
```

Sheets where nearby assets are connected by a few stray pixels:

```powershell
python image_cut.py D:\Art\sheet.png -o D:\Art\cut --alpha 16 --open 1 --min-area 80 --padding 2 --debug --json
```

## How It Works

1. Load the PNG as RGBA.
2. Build a foreground mask from the alpha channel: `alpha > threshold`.
3. Find connected components in that mask.
4. Filter very small regions.
5. Crop each component with optional padding.
6. Export each crop using the component mask, clearing unrelated pixels inside the crop.

This means two sprites can have overlapping bounding rectangles and still export
cleanly, as long as their visible pixels are not connected.

## Output

For each input image, the tool creates a subfolder named after the source file:

```text
cut_output/
  sheet/
    sheet_001.png
    sheet_002.png
    sheet_003.png
    sheet_debug.png
    sheet_manifest.json
  manifest_all.json
```

## Important Options

```text
--alpha N          Foreground alpha threshold, 0-255. Higher values ignore faint pixels.
--min-area N       Ignore components smaller than N pixels.
--padding N        Add transparent padding around each exported sprite.
--connectivity 4|8 Decide whether diagonal-touching pixels are connected.
--open N           Morphological open radius. Use 1 to break tiny bridges.
--merge-small N    Merge components smaller than N into nearest larger component.
--debug            Export a preview image with numbered boxes.
--json             Export per-image and batch JSON manifests.
--recursive        Process folders recursively.
--dry-run          Analyze only.
--overwrite        Replace existing output PNGs.
```

