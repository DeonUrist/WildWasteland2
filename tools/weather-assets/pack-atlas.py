"""Pack an offline RGBA PNG sequence for the WW DustShell prototype shader."""
import argparse
import json
import math
from pathlib import Path
import re
from PIL import Image


def natural(path):
    return [int(part) if part.isdigit() else part.lower() for part in re.split(r"(\d+)", path.name)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("frames", type=Path, help="Folder containing only this effect's rendered PNG frames")
    parser.add_argument("output", type=Path, help="New atlas PNG; also writes a JSON manifest")
    parser.add_argument("--frames", dest="count", type=int, default=32)
    parser.add_argument("--columns", type=int, default=8)
    parser.add_argument("--cell", type=int, default=256, help="Cell pixels including a 2-pixel border on each side")
    parser.add_argument("--fps", type=float, default=12)
    parser.add_argument("--loop", action="store_true", help="Source was deliberately authored as a seamless loop")
    parser.add_argument("--variants", action="store_true", help="Static alternatives; keep atlas animation speed at zero")
    parser.add_argument("--trim", action="store_true", help="Crop static stamps to their alpha bounds; prohibited for animations")
    args = parser.parse_args()
    if args.count < 1 or args.columns < 1 or args.cell < 16 or args.fps <= 0:
        parser.error("Use positive frame count, columns and fps; cell must be at least 16")
    if args.trim and not args.variants:
        parser.error("--trim requires --variants; trimming each animation frame causes visible scale changes")
    if args.output.exists() or args.output.with_suffix(".json").exists():
        parser.error("Output exists; choose a new name to preserve previous bakes")
    files = sorted(args.frames.glob("*.png"), key=natural)
    if len(files) < args.count:
        parser.error(f"Need at least {args.count} source frames; found {len(files)}")
    # Loop sampling excludes the repeated endpoint; one-shot sampling includes it.
    denominator = args.count if args.loop else max(1, args.count - 1)
    extent = len(files) if args.loop else len(files) - 1
    indices = [min(len(files) - 1, round(index * extent / denominator)) for index in range(args.count)]
    rows = math.ceil(args.count / args.columns)
    width, height = args.columns * args.cell, rows * args.cell
    if width > 4096 or height > 4096:
        parser.error("Atlas exceeds 4096; reduce cell size or frames")
    if width & (width - 1) or height & (height - 1):
        parser.error("Choose a grid producing power-of-two dimensions (e.g. 32 frames, 8 columns)")
    atlas = Image.new("RGBA", (width, height), (255, 255, 255, 0))
    original_size = None
    for index, source_index in enumerate(indices):
        with Image.open(files[source_index]) as image:
            if image.mode != "RGBA":
                parser.error(f"Render transparent RGBA PNGs: {files[source_index].name} is {image.mode}")
            if original_size is None:
                original_size = image.size
            if image.size != original_size:
                parser.error("Frames have different dimensions; preserve camera and framing through the bake")
            if image.getchannel("A").getextrema()[0] == 255:
                parser.error(f"Frame has no transparent pixels: {files[source_index].name}")
            if args.trim:
                bounds = image.getchannel("A").point(lambda value: 255 if value > 3 else 0).getbbox()
                if bounds is None:
                    parser.error(f"Empty stamp: {files[source_index].name}")
                image = image.crop(bounds)
            image.thumbnail((args.cell - 4, args.cell - 4), Image.Resampling.LANCZOS)
            tile = image
            atlas.paste(tile, ((index % args.columns) * args.cell + (args.cell - tile.width) // 2,
                               (index // args.columns) * args.cell + (args.cell - tile.height) // 2))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(args.output)
    metadata = {
        "columns": args.columns, "rows": rows, "frames": args.count, "fps": args.fps,
        "loop": args.loop, "order": "top-left row-major", "cell_pixels": args.cell,
        "padding_per_edge": 2, "width": width, "height": height,
        "rgba8_bytes_without_mips": width * height * 4,
        "shader_sheet": [args.columns, rows, args.count, 0 if args.variants else args.fps],
        "source_frames": [files[index].name for index in indices],
        "note": "Static variants select _FrameOffset with fps=0. For animated persistent storms use loop=true sources; crossfade does not make a nonlooping source seamless.",
    }
    args.output.with_suffix(".json").write_text(json.dumps(metadata, indent=2) + "\n")
    print(f"Packed {args.count} frames -> {args.output} ({width} x {height})")


if __name__ == "__main__":
    main()
