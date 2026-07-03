import os
import traceback
from pathlib import Path
from PIL import Image
import subprocess
import shutil
import tempfile


SUPPORTED_FORMATS = {".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif", ".gif", ".tga"}


def get_file_size_str(size_bytes: int) -> str:
    if size_bytes < 1024:
        return f"{size_bytes} B"
    elif size_bytes < 1024 * 1024:
        return f"{size_bytes / 1024:.1f} KB"
    else:
        return f"{size_bytes / 1024 / 1024:.2f} MB"


def _to_saveable_mode(img: Image.Image, fmt: str) -> Image.Image:
    """Convert image to a mode that can be saved in the target format."""
    mode = img.mode

    if fmt in ("JPEG", "JPG"):
        # JPEG only supports L, RGB
        if mode in ("RGBA", "LA", "PA"):
            bg = Image.new("RGB", img.size, (255, 255, 255))
            if mode == "PA":
                img = img.convert("RGBA")
            bg.paste(img, mask=img.split()[-1])
            return bg
        elif mode == "P":
            return img.convert("RGB")
        elif mode in ("I", "I;16", "I;16B", "F"):
            # 16/32-bit grayscale -> 8-bit gray -> RGB
            img = img.convert("L")
            return img.convert("RGB")
        elif mode not in ("L", "RGB"):
            return img.convert("RGB")
        return img

    elif fmt == "PNG":
        # PNG supports L, LA, P, RGB, RGBA — no 32-bit float/int
        if mode in ("I", "I;16", "I;16B", "F"):
            return img.convert("L")
        elif mode == "CMYK":
            return img.convert("RGB")
        return img

    elif fmt == "WEBP":
        if mode in ("I", "I;16", "I;16B", "F"):
            return img.convert("RGB")
        elif mode == "P":
            return img.convert("RGBA")
        elif mode == "CMYK":
            return img.convert("RGB")
        return img

    elif fmt == "TGA":
        # TGA supports L, LA, RGB, RGBA
        if mode in ("I", "I;16", "I;16B", "F"):
            return img.convert("RGB")
        elif mode == "P":
            return img.convert("RGBA")
        elif mode == "CMYK":
            return img.convert("RGB")
        return img

    else:
        # BMP, TIFF, GIF etc — just avoid float/int modes
        if mode in ("I", "I;16", "I;16B", "F"):
            return img.convert("L")
        return img


def compress_image(
    input_path: str,
    output_path: str,
    quality: int = 80,
    max_width: int = 0,
    max_height: int = 0,
    keep_exif: bool = False,
    lossless: bool = False,
    png_lossless: bool = False,
    output_format: str = "same",
    png_quantize: bool = False,
    png_colors: int = 256,
) -> dict:
    result = {
        "success": False,
        "input_path": input_path,
        "output_path": output_path,
        "input_size": 0,
        "output_size": 0,
        "ratio": 0.0,
        "error": "",
        "log": "",
    }

    log_lines = []

    try:
        input_size = os.path.getsize(input_path)
        result["input_size"] = input_size
        log_lines.append(f"[打开] {input_path}  ({get_file_size_str(input_size)})")

        img = Image.open(input_path)
        img.load()  # force decode so errors surface here
        original_format = img.format or "JPEG"
        suffix = Path(input_path).suffix.lower()
        log_lines.append(f"[格式] {original_format}  模式={img.mode}  尺寸={img.size[0]}x{img.size[1]}")

        # Determine output format
        if output_format == "same":
            if suffix in (".jpg", ".jpeg"):
                fmt = "JPEG"
            elif suffix == ".png":
                fmt = "PNG"
            elif suffix == ".webp":
                fmt = "WEBP"
            elif suffix == ".bmp":
                fmt = "BMP"
            elif suffix in (".tiff", ".tif"):
                fmt = "TIFF"
            elif suffix == ".gif":
                fmt = "GIF"
            elif suffix == ".tga":
                fmt = "TGA"
            else:
                fmt = original_format or "JPEG"
        else:
            fmt = output_format.upper()

        log_lines.append(f"[目标格式] {fmt}")

        # Convert to saveable mode
        img = _to_saveable_mode(img, fmt)
        log_lines.append(f"[转换后模式] {img.mode}")

        # Resize if needed
        orig_w, orig_h = img.size
        if (max_width > 0 and orig_w > max_width) or (max_height > 0 and orig_h > max_height):
            ratio_w = max_width / orig_w if max_width > 0 else float("inf")
            ratio_h = max_height / orig_h if max_height > 0 else float("inf")
            scale = min(ratio_w, ratio_h)
            new_w = int(orig_w * scale)
            new_h = int(orig_h * scale)
            img = img.resize((new_w, new_h), Image.LANCZOS)
            log_lines.append(f"[缩放] {orig_w}x{orig_h} → {new_w}x{new_h}")

        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

        # PNG lossy quantize
        if fmt == "PNG" and png_quantize and not png_lossless and not lossless:
            log_lines.append("[PNG] 有损量化模式")
            _save_png_quantized(img, output_path, png_colors, log_lines)
        else:
            save_kwargs = {}
            if fmt == "JPEG":
                save_kwargs["quality"] = quality
                save_kwargs["optimize"] = True
                if keep_exif:
                    try:
                        exif = img.info.get("exif")
                        if exif:
                            save_kwargs["exif"] = exif
                            log_lines.append("[EXIF] 已保留")
                    except Exception:
                        pass
            elif fmt == "PNG":
                if lossless or png_lossless:
                    save_kwargs["optimize"] = True
                    save_kwargs["compress_level"] = 9
                    log_lines.append("[PNG] 无损最高压缩")
                else:
                    compress = max(1, min(9, int((100 - quality) / 11)))
                    save_kwargs["optimize"] = True
                    save_kwargs["compress_level"] = compress
                    log_lines.append(f"[PNG] compress_level={compress}")
            elif fmt == "WEBP":
                if lossless or png_lossless:
                    save_kwargs["lossless"] = True
                    log_lines.append("[WEBP] 无损模式")
                else:
                    save_kwargs["quality"] = quality
                    save_kwargs["method"] = 6
            elif fmt == "TGA":
                save_kwargs["compression"] = "tga_rle"
                log_lines.append("[TGA] RLE压缩")

            img.save(output_path, format=fmt, **save_kwargs)

        output_size = os.path.getsize(output_path)
        result["output_size"] = output_size
        result["ratio"] = (1 - output_size / input_size) * 100 if input_size > 0 else 0
        result["success"] = True
        log_lines.append(
            f"[完成] {get_file_size_str(input_size)} → {get_file_size_str(output_size)}"
            f"  节省 {result['ratio']:.1f}%"
        )

    except Exception as e:
        err = traceback.format_exc()
        result["error"] = str(e)
        log_lines.append(f"[错误] {err}")

    result["log"] = "\n".join(log_lines)
    return result


def _save_png_quantized(img: Image.Image, output_path: str, colors: int, log_lines: list):
    pngquant_exe = shutil.which("pngquant")

    with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
        tmp_in = f.name
    with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as f:
        tmp_out = f.name

    try:
        img.save(tmp_in, format="PNG")

        if pngquant_exe:
            log_lines.append(f"[PNG量化] 使用 pngquant，颜色数={colors}")
            cmd = [
                pngquant_exe, "--force",
                "--quality=60-90",
                f"--colors={colors}",
                "--output", tmp_out,
                tmp_in,
            ]
            subprocess.run(cmd, check=True, capture_output=True)
            shutil.copy2(tmp_out, output_path)
        else:
            log_lines.append(f"[PNG量化] pngquant 未安装，使用 Pillow 量化，颜色数={colors}")
            if img.mode in ("RGBA", "LA"):
                q = img.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
                q = q.convert("RGBA")
            else:
                q = img.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
                q = q.convert("RGB")
            q.save(output_path, format="PNG", optimize=True, compress_level=9)
    finally:
        os.unlink(tmp_in)
        try:
            os.unlink(tmp_out)
        except Exception:
            pass


def batch_compress(
    input_paths: list,
    output_dir: str,
    options: dict,
    progress_callback=None,
    cancel_flag=None,
) -> list:
    results = []
    total = len(input_paths)

    for i, input_path in enumerate(input_paths):
        if cancel_flag and cancel_flag[0]:
            break

        p = Path(input_path)
        suffix = p.suffix.lower()
        out_fmt = options.get("output_format", "same")
        suffix_tag = options.get("suffix_tag", "")  # e.g. "_compressed"

        if out_fmt != "same":
            new_suffix = "." + out_fmt.lower()
            if new_suffix == ".jpeg":
                new_suffix = ".jpg"
        else:
            new_suffix = suffix

        output_path = os.path.join(output_dir, p.stem + suffix_tag + new_suffix)

        # Avoid overwriting source when outdir == source dir and no suffix_tag
        if not suffix_tag and os.path.abspath(input_path) == os.path.abspath(output_path):
            output_path = os.path.join(output_dir, p.stem + "_compressed" + new_suffix)

        res = compress_image(
            input_path=input_path,
            output_path=output_path,
            quality=options.get("quality", 80),
            max_width=options.get("max_width", 0),
            max_height=options.get("max_height", 0),
            keep_exif=options.get("keep_exif", False),
            lossless=options.get("lossless", False),
            png_lossless=options.get("png_lossless", False),
            output_format=out_fmt,
            png_quantize=options.get("png_quantize", False),
            png_colors=options.get("png_colors", 256),
        )
        results.append(res)

        if progress_callback:
            progress_callback(i + 1, total, res)

    return results
