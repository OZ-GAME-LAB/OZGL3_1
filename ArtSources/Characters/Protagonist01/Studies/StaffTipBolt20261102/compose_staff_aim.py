"""Assemble fixed-camera Unity takes at their captured speed."""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path.home() / "Desktop/OZGL3_ArtReview/Characters/Protagonist01/Studies/ChibiStaffAimCast20261102/idle-sway"
FONTS = Path("C:/Windows/Fonts")
INK, MUTED, TEAL, BG = "#1b3540", "#506872", "#187766", "#f4f6f3"
PHASES = {"idle": "대기", "blendIn": "시전 준비", "cast": "시전", "blendOut": "대기 복귀", "idleAfter": "대기 복귀"}


def read_frames(root, folder, samples, size):
    images = []
    for sample in samples:
        path = root / "frames" / folder / f"{sample['frame']:03d}.png"
        with Image.open(path) as image:
            images.append(image.convert("RGB").resize((size, size), Image.Resampling.LANCZOS))
    return images


def write_gif(path, frames, fps, accent_frame=None):
    # A shared palette prevents the navy silhouette and warm skin from flickering.
    tiles = [frame.resize((156, 128), Image.Resampling.BOX) for frame in frames]
    sheet = Image.new("RGB", (156 * 7, 128 * ((len(tiles) + 6) // 7)), BG)
    for index, tile in enumerate(tiles):
        sheet.paste(tile, ((index % 7) * 156, (index // 7) * 128))
    accents = []
    if accent_frame is not None:
        img = frames[accent_frame]
        colors = img.getcolors(maxcolors=img.width * img.height) or []
        # Keep the tiny turquoise projectile visible alongside the much larger body.
        accents = [color for count, color in sorted(colors, reverse=True)
                   if color[1] - color[0] > 70 and color[2] - color[0] > 70][:8]
    n = 256 - len(accents)
    palette = sheet.quantize(colors=n, method=Image.Quantize.MEDIANCUT)
    palette.putpalette(palette.getpalette()[:3 * n] + [ch for color in accents for ch in color])
    sequence = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    ticks = [round(i * 100 / fps) for i in range(len(frames) + 1)]
    durations = [(b - a) * 10 for a, b in zip(ticks, ticks[1:])]
    sequence[0].save(path, save_all=True, append_images=sequence[1:], duration=durations, loop=0, optimize=False)


def pair_frames(left_panel, right_panel, samples, size, heading, labels, caption):
    title = ImageFont.truetype(str(FONTS / "malgunbd.ttf"), 25)
    label = ImageFont.truetype(str(FONTS / "malgunbd.ttf"), 16)
    meta = ImageFont.truetype(str(FONTS / "segoeuib.ttf"), 13)
    text = ImageFont.truetype(str(FONTS / "malgun.ttf"), 15)
    canvas = Image.new("RGB", (size * 2 + 48, size + 113), BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((16, 9), heading, font=title, fill=INK)
    draw.text((canvas.width - 16, 21), "PLAYER_CHIBI  /  UNITY  /  1×", anchor="ra", font=meta, fill=TEAL)
    for x, name in ((16, labels[0]), (size + 32, labels[1])):
        draw.text((x, 52), name, font=label, fill=MUTED)
    draw.text((16, size + 82), caption, font=text, fill=MUTED)
    result = []
    for first, second, sample in zip(left_panel, right_panel, samples):
        frame = canvas.copy()
        for im, x in ((first, 16), (second, size + 32)):
            if im.size != (size, size):
                im = im.resize((size, size), Image.Resampling.LANCZOS)
            frame.paste(im, (x, 77))
        ImageDraw.Draw(frame).text((canvas.width - 16, size + 82), PHASES[sample["phase"]],
                                    font=label, fill=TEAL, anchor="ra")
        result.append(frame)
    return result


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("destination", type=Path, nargs="?", default=ROOT)
    root = ap.parse_args().destination
    reports = [json.loads((root / "frames" / facing / "capture.json").read_text(encoding="utf-8-sig"))
               for facing in ("right", "left")]
    info = reports[0]
    samples, fps = info["samples"], info["fps"]
    assert len(samples) == len(reports[1]["samples"]) and fps == reports[1]["fps"]
    assert info["releaseFrame"] == reports[1]["releaseFrame"]
    accent = info["releaseFrame"] + 1
    right = read_frames(root, "right", samples, 600)
    left = read_frames(root, "left", samples, 600)
    for facing, sequence in (("right", right), ("left", left)):
        write_gif(root / f"staff-aim-{facing}.gif", sequence, fps, accent)
    del sequence
    labels = ("오른쪽을 볼 때", "왼쪽을 볼 때")
    # Use just the first complete idle cycle. Crossing its seam must not add an extra still frame.
    count = sum(s["seconds"] < info["triggerSeconds"] - 1e-5 for s in samples)
    assert count and all(s["phase"] == "idle" for s in samples[:count])
    idle = pair_frames(right[:count], left[:count], samples[:count], 464, "대기 중 오른팔 움직임", labels,
                       f"대기 한 바퀴  {info['idleSeconds']:.2f}초")
    write_gif(root / "idle-both.gif", idle, fps)
    del idle
    body = pair_frames(right, left, samples, 464, "대기에서 시전, 다시 대기로", labels,
                       f"시전 입력 후 {info['releaseSeconds'] - info['triggerSeconds']:.2f}초에 발사")
    write_gif(root / "staff-aim-both.gif", body, fps, accent)
    del body, right, left

    side = read_frames(root, "right-hand-detail", samples, 420)
    front = read_frames(root, "right-hand-front", samples, 420)
    detail = pair_frames(side, front, samples, 420, "몸통과 오른팔 움직임", ("오른쪽 옆에서", "오른쪽 앞에서"),
                         "고정 카메라  ·  어깨, 팔꿈치, 주먹")
    write_gif(root / "right-arm-sway-detail.gif", detail, fps)
    print(f"Composed {len(samples)} frames per facing, {count} idle-only frames, {fps} fps. Output: {root}")


if __name__ == "__main__":
    main()
