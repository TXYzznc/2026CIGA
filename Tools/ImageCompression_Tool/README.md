# ImageCompression Tool

Local image compressor for the game art pipeline. Two entry points:

- **GUI** — `app.py` (Tkinter, drag-and-drop, used by humans).
- **CLI** — `cli.py` (argparse, used by AI agents and scripts).

Backed by [Pillow](https://pypi.org/project/Pillow/); optional [pngquant](https://pngquant.org/)
for lossy PNG quantize (auto-detected on `PATH`, falls back to Pillow's median-cut).

---

## Install

Python 3.10+ recommended.

```bash
# from repo root, after activating .venv
.venv/Scripts/python -m pip install -r tools/ImageCompression_Tool/requirements.txt
```

Optional (better PNG quantize quality):

```bash
# Windows: choco install pngquant   |   macOS: brew install pngquant
```

---

## CLI Usage

All examples assume repo root as cwd. Use bash (`.venv/Scripts/python` not `python`).

### Basic — single file, default output dir

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/img.png
# → writes path/to/compressed/img.png
```

### Folder, recursive

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py Assets/Resources/Sprite/Items --recursive
```

### Apply a preset from `.claude/美术资源规范.md`

Presets bundle max size + format + quality for each asset class. See full list with `--help`.

```bash
# Compress all Items as 256×256 PNG with lossy quantize
.venv/Scripts/python tools/ImageCompression_Tool/cli.py Assets/Resources/Sprite/Items \
    --recursive --preset item-icon

# Compress all Environments to 1920×1080 JPEG q85
.venv/Scripts/python tools/ImageCompression_Tool/cli.py Assets/Resources/Sprite/Environments \
    --recursive --preset environment-bg
```

### In-place overwrite (DANGER — back up first)

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py Assets/Resources/Sprite/UI \
    --recursive --in-place --preset ui-panel
```

### JSON output (for AI agents to parse)

```bash
.venv/Scripts/python tools/ImageCompression_Tool/cli.py path/to/img.png --json --quiet
```

Emits:

```json
{
  "total": 1, "ok": 1, "fail": 0,
  "input_bytes": 1524220, "output_bytes": 184391, "saved_pct": 87.9,
  "preset": null,
  "files": [{"input": "...", "output": "...", "ratio": 87.9, "success": true, "error": ""}]
}
```

Exit code: `0` if every file succeeded, `1` if any failed, `2` on bad arguments.

---

## Presets (sync with `.claude/美术资源规范.md`)

| Preset name          | Max size   | Format | Notes                          |
|----------------------|------------|--------|--------------------------------|
| `ui-icon`            | 256×256    | PNG    | Quantized, transparent allowed |
| `ui-button`          | 512×256    | PNG    | Quantized                      |
| `ui-panel`           | 1024×1024  | PNG    | Quantized, supports 9-slice    |
| `ui-bg`              | 1920×1080  | PNG    | Quantized                      |
| `character-portrait` | 1024×2048  | PNG    | High quality, no quantize      |
| `character-avatar`   | 512×512    | PNG    | Quantized                      |
| `item-icon`          | 256×256    | PNG    | Quantized                      |
| `skill-icon`         | 256×256    | PNG    | Quantized                      |
| `decal`              | 1024×1024  | PNG    | Decal / overlay art, no quantize (alias: `tattoo`) |
| `environment-bg`     | 1920×1080  | JPEG   | q85, no alpha                  |
| `effect-texture`     | 512×512    | PNG    | Quantized                      |
| `screenshot`         | 1920×1080  | JPEG   | q80                            |

If you need a preset that's not listed, edit `cli.py::PRESETS` and the规范文档 in sync.

---

## Manual options (override preset)

```text
--quality N           JPEG/WEBP quality 1-100. (default 80)
--max-width N         Resize width <= N. 0 = no limit.
--max-height N        Resize height <= N. 0 = no limit.
--format FMT          same | JPEG | PNG | WEBP | TGA | BMP
--png-lossless        PNG: optimize + max compress level (lossless).
--png-quantize        PNG: lossy palette quantize.
--png-colors N        PNG quantize colors 2-256. (default 256)
--keep-exif           JPEG: keep EXIF metadata.
--suffix STR          Append suffix to filename (e.g. _compressed).
```

---

## How it works

1. Open image via Pillow → force decode (`img.load()`).
2. Convert to a mode the target format can save (RGBA→RGB for JPEG, etc.).
3. Resize with LANCZOS if `--max-width` / `--max-height` clip.
4. Save with format-specific kwargs (JPEG quality + optimize, PNG compress_level, WEBP method=6).
5. PNG `--png-quantize` path: prefer `pngquant` binary if on `PATH`, else Pillow MEDIANCUT.

Core logic lives in `compressor.py` — `cli.py` is just an argparse front-end. The Tkinter
`app.py` calls the exact same functions, so behaviour is identical between GUI and CLI.

---

## Files

```
tools/ImageCompression_Tool/
├── compressor.py     ← Pure functions: compress_image, batch_compress
├── cli.py            ← AI/script entry point (this README's subject)
├── app.py            ← Tkinter GUI (human entry point)
├── requirements.txt  ← Pillow + tkinterdnd2 (GUI only)
└── README.md
```
