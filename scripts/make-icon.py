"""生成 KeyStar 的 NASA 朋克风格图标。

设计：深色仪器面板 + 蓝图网格 + 蠕虫红顶部仪表条 + 中央蓝色键帽（内含四角星）
      + 底部一排小键帽（琥珀/深灰），即 "KeyStar" = 键 + 星。

输出：
  src/KeyStar.App/Assets/KeyStar-icon-final.png   1024x1024 预览图
  src/KeyStar.App/Assets/KeyStar.ico              多尺寸图标（16/20/24/32/48/64/128/256）
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "KeyStar.App" / "Assets"

# ---- NASA 朋克配色（与 App.xaml 令牌一致）----
BG_DEEP = (11, 13, 17)          # #0B0D11
PANEL_ALT = (23, 28, 35)        # #171C23
GRID = (21, 27, 35)             # #151B23
BORDER_STRONG = (57, 67, 78)    # #39434E
BLUE = (78, 156, 255)           # #4E9CFF
BLUE_EDGE = (14, 48, 84)        # 蓝色键帽的深色描边
RED = (252, 61, 33)             # #FC3D21
AMBER = (255, 180, 84)          # #FFB454
INK = (7, 20, 34)               # #071422 星形

SS = 4          # 超采样倍数
BASE = 1024     # 设计基准尺寸
RADIUS = 176    # 圆角半径（基准单位）


def draw_star(draw: ImageDraw.ImageDraw, cx: float, cy: float, outer: float, inner: float, color) -> None:
    """四角星：NASA 蠕虫时代的几何感。"""
    points = []
    for index in range(8):
        angle = math.pi / 2 * (index / 2) - math.pi / 2
        radius = outer if index % 2 == 0 else inner
        points.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle)))
    draw.polygon(points, fill=color)


def make_icon(size: int) -> Image.Image:
    big = size * SS
    scale = big / BASE

    def px(value: float) -> float:
        return value * scale

    def box(x0: float, y0: float, x1: float, y1: float) -> tuple[float, float, float, float]:
        return (px(x0), px(y0), px(x1), px(y1))

    simple = size <= 32  # 小尺寸省略细节，保证辨识度
    line = lambda weight: max(1, int(round(px(weight))))

    layer = Image.new("RGB", (big, big), BG_DEEP)
    draw = ImageDraw.Draw(layer)

    # 蓝图网格
    spacing = max(3, int(px(74)))
    for offset in range(0, big + spacing, spacing):
        draw.line([(offset, 0), (offset, big)], fill=GRID, width=1)
        draw.line([(0, offset), (big, offset)], fill=GRID, width=1)

    if not simple:
        # HUD 四角括号
        arm, inset, weight = px(118), px(74), line(9)
        for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
            x = inset if sx > 0 else big - inset
            y = inset if sy > 0 else big - inset
            draw.line([(x, y), (x + sx * arm, y)], fill=BORDER_STRONG, width=weight)
            draw.line([(x, y), (x, y + sy * arm)], fill=BORDER_STRONG, width=weight)

    # 顶部蠕虫红仪表条
    draw.rectangle((0, 0, big, px(74)), fill=RED)

    # ---- 中央键帽（键 + 星）----
    key = box(252, 186, 772, 706)
    draw.rounded_rectangle(key, radius=px(92), fill=BLUE, outline=BLUE_EDGE, width=line(12))
    draw_star(
        draw,
        (key[0] + key[2]) / 2,
        (key[1] + key[3]) / 2 - px(6),
        px(158),
        px(62),
        INK,
    )

    # ---- 底部三个小键帽（中间琥珀色）----
    if not simple:
        for index in range(3):
            x0 = 252 + index * (152 + 34)
            fill = AMBER if index == 1 else PANEL_ALT
            draw.rounded_rectangle(
                box(x0, 760, x0 + 152, 912),
                radius=px(26),
                fill=fill,
                outline=BORDER_STRONG,
                width=line(7),
            )

    # ---- 统一裁成圆角 ----
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, big - 1, big - 1), radius=px(RADIUS), fill=255)

    out = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    out.paste(layer, (0, 0), mask)
    return out.resize((size, size), Image.LANCZOS)


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)

    preview = make_icon(BASE)
    preview_path = ASSETS / "KeyStar-icon-final.png"
    preview.save(preview_path, "PNG")
    print(f"WROTE {preview_path}")

    sizes = [16, 20, 24, 32, 48, 64, 128, 256]
    frames = [make_icon(s) for s in sizes]
    ico_path = ASSETS / "KeyStar.ico"
    frames[-1].save(
        ico_path,
        format="ICO",
        sizes=[(s, s) for s in sizes],
        append_images=frames[:-1],
    )
    with Image.open(ico_path) as check:
        print(f"WROTE {ico_path} sizes={sorted(check.ico.sizes())}")


if __name__ == "__main__":
    main()
