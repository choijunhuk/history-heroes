"""History Heroes UI 부품 그림 만들기 (그래픽 평가 3번 묶음, Docs/Reviews/2026-10-27-graphics-eval.md).

- 대부분 흰색·회색으로 그린다 → Unity Image.color가 곱해져서 색이 된다 (버튼 하나로 노랑·파랑·초록 버튼).
- 검은 테두리·그림자처럼 색이 정해진 부분은 그대로 남는다 (곱해도 검정).
- 4배로 크게 그린 뒤 줄여서 가장자리를 부드럽게 한다.

사용: python Tools/ui_kit.py Game/Assets/Art/UI/Kit
9칸 자르기 테두리(왼쪽, 아래, 오른쪽, 위)는 BORDERS에 있고, 장면 빌더(SceneBuilder.UiKit)가 같은 값을 쓴다.
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

SS = 4
OUTLINE = (24, 22, 34)

# 9칸 자르기 테두리 (픽셀, 왼쪽·아래·오른쪽·위)
BORDERS = {
    "ui_button": (34, 30, 34, 30),
    "ui_panel": (44, 44, 44, 44),
    "ui_row": (28, 28, 28, 28),
    "ui_frame": (40, 40, 40, 40),
    "ui_frame_gold": (44, 44, 44, 44),
    "ui_shadow": (60, 60, 60, 60),
    "ui_badge": (22, 22, 22, 22),
    "ui_ribbon": (96, 0, 96, 0),
    "ui_bar_bg": (24, 22, 24, 22),
    "ui_slot": (30, 30, 30, 30),
    "ui_tab": (30, 20, 30, 20),
}


def big(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def mask_rrect(w, h, box, r):
    """box = (x0, y0, x1, y1) 실제 픽셀, r = 반지름"""
    m = Image.new("L", (w * SS, h * SS), 0)
    x0, y0, x1, y1 = (v * SS for v in box)
    ImageDraw.Draw(m).rounded_rectangle([x0, y0, x1 - 1, y1 - 1], radius=max(0, r) * SS, fill=255)
    return m


def mask_ellipse(w, h, box):
    m = Image.new("L", (w * SS, h * SS), 0)
    x0, y0, x1, y1 = (v * SS for v in box)
    ImageDraw.Draw(m).ellipse([x0, y0, x1 - 1, y1 - 1], fill=255)
    return m


def vgrad(w, h, top, bottom, y0=0, y1=None):
    """세로 그라데이션 RGB (top·bottom = (r,g,b)), y0~y1 사이에서 변한다"""
    y1 = h if y1 is None else y1
    col = Image.new("RGB", (1, h * SS))
    for y in range(h * SS):
        t = min(1.0, max(0.0, (y / SS - y0) / max(1e-6, (y1 - y0))))
        col.putpixel((0, y), tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    return col.resize((w * SS, h * SS))


def alpha_vgrad(w, h, a_top, a_bottom, y0, y1):
    col = Image.new("L", (1, h * SS))
    for y in range(h * SS):
        t = min(1.0, max(0.0, (y / SS - y0) / max(1e-6, (y1 - y0))))
        col.putpixel((0, y), int(a_top + (a_bottom - a_top) * t))
    return col.resize((w * SS, h * SS))


def paint(img, fill, mask, alpha=None):
    """fill: (r,g,b) 또는 RGB 이미지. alpha: 0~255 상수나 L 이미지 (mask에 곱한다)"""
    w, h = img.size
    src = Image.new("RGB", (w, h), fill) if isinstance(fill, tuple) else fill
    m = mask
    if alpha is not None:
        a = Image.new("L", (w, h), alpha) if isinstance(alpha, int) else alpha
        m = ImageChops.multiply(mask, a)
    layer = Image.new("RGBA", (w, h))
    layer.paste(src, (0, 0))
    layer.putalpha(m)
    img.alpha_composite(layer)


def sub(m1, m2):
    return ImageChops.subtract(m1, m2)


def finish(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


# ---------------------------------------------------------------- 부품

def button(w=256, h=128, r=30, lip=11):
    """두툼한 버튼: 검은 테두리 · 아래 그늘(두께) · 위가 밝은 면 · 위쪽 광택"""
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    paint(img, (118, 118, 118), mask_rrect(w, h, (4, 4, w - 4, h - 4), r - 4))  # 아래 두께
    face = mask_rrect(w, h, (4, 4, w - 4, h - 4 - lip), r - 4)
    paint(img, vgrad(w, h, (255, 255, 255), (198, 198, 198), 4, h - lip), face)
    # 위쪽 광택 (면 위 절반이 하얗게 흐려진다)
    gloss = mask_rrect(w, h, (9, 8, w - 9, int(h * 0.5)), r - 9)
    paint(img, (255, 255, 255), gloss, alpha_vgrad(w, h, 120, 0, 8, h * 0.5))
    # 면 위 가장자리 밝은 선
    ring = sub(face, mask_rrect(w, h, (6, 7, w - 6, h - 4 - lip), r - 6))
    paint(img, (255, 255, 255), ring, 160)
    return finish(img, w, h)


def panel(w=256, h=256, r=38):
    """창·카드 바탕: 검은 테두리 · 밝은 테두리 고리 · 위가 조금 밝은 면 · 가장자리 안쪽 그늘"""
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    paint(img, (255, 255, 255), mask_rrect(w, h, (4, 4, w - 4, h - 4), r - 4))
    face = mask_rrect(w, h, (8, 8, w - 8, h - 8), r - 8)
    paint(img, vgrad(w, h, (214, 214, 214), (168, 168, 168), 8, h - 8), face)
    # 안쪽 그늘 (가장자리를 따라 어둡게)
    inner = mask_rrect(w, h, (8, 8, w - 8, h - 8), r - 8).filter(ImageFilter.GaussianBlur(10 * SS))
    paint(img, (60, 60, 70), sub(face, inner), 110)
    return finish(img, w, h)


def row(w=128, h=128, r=20):
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    face = mask_rrect(w, h, (3, 3, w - 3, h - 3), r - 3)
    paint(img, vgrad(w, h, (236, 236, 236), (196, 196, 196), 3, h - 3), face)
    ring = sub(face, mask_rrect(w, h, (5, 5, w - 5, h - 5), r - 5))
    paint(img, (255, 255, 255), ring, 150)
    return finish(img, w, h)


def frame(w=256, h=256, r=38, gold=False):
    """가운데가 빈 테두리 (카드 등급 · 중요한 창). gold=True면 금색으로 칠해서 그린다"""
    img = big(w, h)
    dark = (40, 26, 8) if gold else OUTLINE
    paint(img, dark, sub(mask_rrect(w, h, (0, 0, w, h), r), mask_rrect(w, h, (12, 12, w - 12, h - 12), r - 12)))
    ringm = sub(mask_rrect(w, h, (2, 2, w - 2, h - 2), r - 2), mask_rrect(w, h, (10, 10, w - 10, h - 10), r - 10))
    top, bottom = ((255, 236, 160), (196, 128, 34)) if gold else ((255, 255, 255), (170, 170, 170))
    paint(img, vgrad(w, h, top, bottom), ringm)
    # 고리 위쪽 반짝임
    shine = sub(mask_rrect(w, h, (3, 3, w - 3, h - 3), r - 3), mask_rrect(w, h, (5, 5, w - 5, h - 5), r - 5))
    paint(img, (255, 255, 255), shine, alpha_vgrad(w, h, 200, 0, 0, h * 0.6))
    # 네 모서리 보석 못
    for cx, cy in ((r * 0.55, r * 0.55), (w - r * 0.55, r * 0.55), (r * 0.55, h - r * 0.55), (w - r * 0.55, h - r * 0.55)):
        rr = 9
        paint(img, dark, mask_ellipse(w, h, (cx - rr - 2, cy - rr - 2, cx + rr + 2, cy + rr + 2)))
        paint(img, vgrad(w, h, top, bottom, cy - rr, cy + rr), mask_ellipse(w, h, (cx - rr, cy - rr, cx + rr, cy + rr)))
        paint(img, (255, 255, 255), mask_ellipse(w, h, (cx - rr * 0.5, cy - rr * 0.65, cx + rr * 0.1, cy - rr * 0.1)), 200)
    return finish(img, w, h)


def shadow(w=160, h=160, r=44):
    """창·버튼 밑 그림자 (부드러운 검정)"""
    img = big(w, h)
    m = mask_rrect(w, h, (30, 30, w - 30, h - 30), r).filter(ImageFilter.GaussianBlur(12 * SS))
    paint(img, (0, 0, 0), m, 190)
    return finish(img, w, h)


def radial(size, fn):
    """가운데에서의 거리(0~1.4)로 알파를 정하는 흰 그림"""
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            px[x, y] = (255, 255, 255, int(255 * max(0.0, min(1.0, fn(math.hypot(x - c, y - c) / c)))))
    return img


def ring(size=256):
    """퍼지는 고리 (폭발·내려찍기): 가장자리 0.8 근처만 밝다"""
    return radial(size, lambda d: max(0.0, 1.0 - abs(d - 0.78) / 0.16) ** 1.5 * (1.0 if d < 1.0 else 0.0))


def glow(size=256):
    return radial(size, lambda d: max(0.0, 1.0 - d) ** 2.2)


def ray_burst(size=512, rays=14):
    """레벨 업·보상 뒤에 도는 빛살"""
    img = big(size, size)
    m = Image.new("L", (size * SS, size * SS), 0)
    d = ImageDraw.Draw(m)
    c = size * SS / 2
    for i in range(rays):
        a0 = (i / rays) * math.tau
        a1 = a0 + math.tau / rays * 0.42
        d.polygon([(c, c), (c + math.cos(a0) * c * 1.5, c + math.sin(a0) * c * 1.5), (c + math.cos(a1) * c * 1.5, c + math.sin(a1) * c * 1.5)], fill=255)
    fade = radial(size, lambda dd: max(0.0, 1.0 - dd) ** 1.4).getchannel("A").resize((size * SS, size * SS))
    paint(img, (255, 255, 255), m, fade)
    return finish(img, size, size)


def ribbon(w=512, h=128):
    """제목 띠 (양 끝이 접힌 리본)"""
    img = big(w, h)
    s = SS
    tails = Image.new("L", (w * s, h * s), 0)
    d = ImageDraw.Draw(tails)
    for flip in (False, True):
        pts = [(4, 34), (92, 34), (92, 112), (4, 112), (34, 73)]
        if flip:
            pts = [(w - x, y) for x, y in pts]
        d.polygon([(x * s, y * s) for x, y in pts], fill=255)
    paint(img, OUTLINE, tails.filter(ImageFilter.MaxFilter(9)))
    paint(img, (150, 150, 150), tails)
    paint(img, OUTLINE, mask_rrect(w, h, (50, 8, w - 50, 102), 13))
    paint(img, vgrad(w, h, (255, 255, 255), (196, 196, 196), 12, 98), mask_rrect(w, h, (54, 12, w - 54, 98), 10))
    paint(img, (255, 255, 255), mask_rrect(w, h, (60, 16, w - 60, 52), 8), alpha_vgrad(w, h, 130, 0, 16, 52))
    fold = Image.new("L", (w * s, h * s), 0)
    fd = ImageDraw.Draw(fold)
    fd.polygon([(54 * s, 98 * s), (92 * s, 98 * s), (92 * s, 112 * s)], fill=255)
    fd.polygon([((w - 54) * s, 98 * s), ((w - 92) * s, 98 * s), ((w - 92) * s, 112 * s)], fill=255)
    paint(img, (70, 70, 80), fold)
    return finish(img, w, h)


def bar_bg(w=128, h=48, r=22):
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    paint(img, vgrad(w, h, (20, 22, 30), (54, 58, 72), 3, h - 3), mask_rrect(w, h, (3, 3, w - 3, h - 3), r - 3))
    return finish(img, w, h)


def bar_fill(w=512, h=48, r=20):
    """채우기 막대 (Filled 이미지라 9칸 자르기 없이 늘린다 → 넓게 그려 양 끝이 덜 늘어나게)"""
    img = big(w, h)
    paint(img, vgrad(w, h, (255, 255, 255), (178, 178, 178), 4, h - 4), mask_rrect(w, h, (4, 4, w - 4, h - 4), r - 2))
    paint(img, (255, 255, 255), mask_rrect(w, h, (10, 7, w - 10, int(h * 0.42)), 8), alpha_vgrad(w, h, 170, 30, 7, h * 0.42))
    return finish(img, w, h)


def slot(w=128, h=128, r=30):
    """아이콘 칸 (스킬·메뉴): 안쪽이 오목하게 어둡다"""
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    paint(img, (255, 255, 255), mask_rrect(w, h, (3, 3, w - 3, h - 3), r - 3))
    inner = mask_rrect(w, h, (8, 8, w - 8, h - 8), r - 8)
    paint(img, vgrad(w, h, (120, 120, 128), (196, 196, 204), 8, h - 8), inner)
    paint(img, (0, 0, 0), sub(inner, mask_rrect(w, h, (8, 14, w - 8, h - 8), r - 8)), 90)
    return finish(img, w, h)


def badge(w=96, h=48, r=22):
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h, (0, 0, w, h), r))
    paint(img, vgrad(w, h, (255, 255, 255), (200, 200, 200), 3, h - 3), mask_rrect(w, h, (3, 3, w - 3, h - 3), r - 3))
    paint(img, (255, 255, 255), mask_rrect(w, h, (7, 6, w - 7, h // 2), r - 7), alpha_vgrad(w, h, 140, 0, 6, h / 2))
    return finish(img, w, h)


def tab(w=128, h=96, r=26):
    """위만 둥근 탭"""
    img = big(w, h)
    paint(img, OUTLINE, mask_rrect(w, h + 40, (0, 0, w, h + 40), r).crop((0, 0, w * SS, h * SS)))
    face = mask_rrect(w, h + 40, (3, 3, w - 3, h + 40), r - 3).crop((0, 0, w * SS, h * SS))
    paint(img, vgrad(w, h, (255, 255, 255), (200, 200, 200), 3, h), face)
    return finish(img, w, h)


def screen_gradient(w=64, h=512):
    img = Image.new("RGBA", (w, h))
    for y in range(h):
        v = int(255 - 120 * y / (h - 1))
        for x in range(w):
            img.putpixel((x, y), (v, v, v, 255))
    return img


def pattern(size=256):
    """화면 바탕 무늬 (마름모 격자, 아주 옅게). 이어 붙여 깐다"""
    img = big(size, size)
    m = Image.new("L", (size * SS, size * SS), 0)
    d = ImageDraw.Draw(m)
    s = size * SS
    wdt = 2 * SS
    for a, b in (((0, 0), (s, s)), ((0, s), (s, 0)), ((s / 2, 0), (s, s / 2)), ((0, s / 2), (s / 2, s)), ((s / 2, 0), (0, s / 2)), ((s, s / 2), (s / 2, s))):
        d.line([a, b], fill=255, width=wdt)
    paint(img, (255, 255, 255), m, 26)
    dots = Image.new("L", (s, s), 0)
    dd = ImageDraw.Draw(dots)
    rr = 3 * SS
    for cx, cy in ((s / 2, s / 4), (s / 4, s / 2), (3 * s / 4, s / 2), (s / 2, 3 * s / 4)):
        dd.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=255)
    paint(img, (255, 255, 255), dots, 30)
    return finish(img, size, size)


def vignette(size=512):
    img = Image.new("RGBA", (size, size))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            d = math.hypot((x - c) / c, (y - c) / c) / math.sqrt(2)
            a = min(1.0, max(0.0, (d - 0.45) / 0.55)) ** 1.6
            px[x, y] = (0, 0, 0, int(220 * a))
    return img


def star(size=96, points=5, inner=0.45):
    img = big(size, size)
    m = Image.new("L", (size * SS, size * SS), 0)
    c = size * SS / 2
    pts = []
    for i in range(points * 2):
        a = -math.pi / 2 + i * math.pi / points
        rr = c * 0.86 * (1 if i % 2 == 0 else inner)
        pts.append((c + math.cos(a) * rr, c + math.sin(a) * rr))
    ImageDraw.Draw(m).polygon(pts, fill=255)
    paint(img, OUTLINE, m.filter(ImageFilter.MaxFilter(13)))
    paint(img, vgrad(size, size, (255, 255, 255), (190, 190, 190)), m)
    return finish(img, size, size)


def sparkle(size=96):
    """네 갈래 반짝임 (효과·보상)"""
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    px = img.load()
    c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            dx, dy = abs(x - c) / c, abs(y - c) / c
            a = max(0.0, 1 - (dx * 6 + dy)) + max(0.0, 1 - (dy * 6 + dx)) + max(0.0, 1 - math.hypot(dx, dy) * 2.2) * 0.8
            px[x, y] = (255, 255, 255, int(255 * min(1.0, a)))
    return img


def divider(w=512, h=16):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for x in range(w):
        t = 1 - abs((x / (w - 1)) * 2 - 1)
        for y in range(h):
            dy = abs(y - (h - 1) / 2) / ((h - 1) / 2)
            px[x, y] = (255, 255, 255, int(255 * t ** 0.8 * max(0.0, 1 - dy * 1.6)))
    return img


def main(out):
    os.makedirs(out, exist_ok=True)
    parts = {
        "ui_button": button(),
        "ui_panel": panel(),
        "ui_row": row(),
        "ui_frame": frame(),
        "ui_frame_gold": frame(gold=True),
        "ui_shadow": shadow(),
        "ui_glow": glow(),
        "ui_ring": ring(),
        "ui_rays": ray_burst(),
        "ui_ribbon": ribbon(),
        "ui_bar_bg": bar_bg(),
        "ui_bar_fill": bar_fill(),
        "ui_slot": slot(),
        "ui_badge": badge(),
        "ui_tab": tab(),
        "ui_screen": screen_gradient(),
        "ui_pattern": pattern(),
        "ui_vignette": vignette(),
        "ui_star": star(),
        "ui_sparkle": sparkle(),
        "ui_divider": divider(),
    }
    for name, img in parts.items():
        img.save(os.path.join(out, name + ".png"))
        print(name, img.size, BORDERS.get(name, ""))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "ui_kit_out")
