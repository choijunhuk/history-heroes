"""던전 돌바닥 무늬 (그래픽 재평가 4번 "바닥 줄눈이 같은 간격으로 끝까지 반복된다", 개발자: "맵도 너무 단조로운거 같은데").

- 크기가 다른 판석: 2x2 큰 판 · 2x1 · 1x2 · 1x1 을 섞는다 (줄마다 같은 네모가 아니게)
- 줄눈은 옅고 가늘게, 판석마다 밝기·색 차이, 모서리 깨짐 몇 개
- 큰 얼룩(먼지 · 이끼)으로 넓게 명암을 흔들어 반복이 덜 보이게
- 위아래·좌우가 이어 붙는다 (무늬 한 장 = 게임 안 6 유닛, 판석 한 칸 = 0.75 유닛)

사용: python Tools/floor_tex.py Game/Assets/Art/3D/Textures
"""
import math
import os
import random
import sys

from PIL import Image, ImageFilter

N = 1024
CELLS = 8
CELL = N // CELLS


class Noise:
    """돌아 감기는 값 노이즈 (fBm): period 칸 수 · 가중치"""

    def __init__(self, seed, octaves):
        rng = random.Random(seed)
        self.layers = [([[rng.random() for _ in range(p)] for _ in range(p)], p, w) for p, w in octaves]

    @staticmethod
    def _one(grid, period, x, y):
        gx, gy = x * period / N, y * period / N
        x0, y0 = int(gx) % period, int(gy) % period
        x1, y1 = (x0 + 1) % period, (y0 + 1) % period
        fx, fy = gx - int(gx), gy - int(gy)
        sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        a = grid[y0][x0] + (grid[y0][x1] - grid[y0][x0]) * sx
        b = grid[y1][x0] + (grid[y1][x1] - grid[y1][x0]) * sx
        return a + (b - a) * sy

    def __call__(self, x, y):
        return sum(self._one(g, p, x, y) * w for g, p, w in self.layers)


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def clamp(v):
    return max(0.0, min(1.0, v))


def layout(rng):
    """칸 → 판석 번호. 판석 = (x, y, w, h) (칸 단위, 감아 돈다)"""
    owner = [[-1] * CELLS for _ in range(CELLS)]
    slabs = []
    for cy in range(CELLS):
        for cx in range(CELLS):
            if owner[cy][cx] >= 0:
                continue
            options = [(1, 1)]
            r = rng.random()
            free = lambda dx, dy: owner[(cy + dy) % CELLS][(cx + dx) % CELLS] < 0
            if r < 0.22 and free(1, 0) and free(0, 1) and free(1, 1):
                options = [(2, 2)]
            elif r < 0.45 and free(1, 0):
                options = [(2, 1)]
            elif r < 0.62 and free(0, 1):
                options = [(1, 2)]
            w, h = options[0]
            idx = len(slabs)
            slabs.append((cx, cy, w, h))
            for dy in range(h):
                for dx in range(w):
                    owner[(cy + dy) % CELLS][(cx + dx) % CELLS] = idx
    return owner, slabs


def floor(out):
    rng = random.Random(77)
    owner, slabs = layout(rng)
    tone = [rng.random() for _ in slabs]
    tint = [rng.random() for _ in slabs]
    chipped = {i for i in range(len(slabs)) if rng.random() < 0.18}
    grain = Noise(78, [(16, 0.4), (64, 0.35), (256, 0.25)])
    big = Noise(79, [(2, 0.6), (4, 0.4)])           # 넓은 명암 (반복 감추기)
    moss = Noise(80, [(3, 0.55), (6, 0.3), (24, 0.15)])
    crack = Noise(81, [(32, 0.6), (128, 0.4)])
    warp = Noise(82, [(16, 0.6), (64, 0.4)])      # 판석 가장자리를 살짝 삐뚤게 (손으로 깐 돌)
    warp2 = Noise(83, [(16, 0.6), (64, 0.4)])

    base_a, base_b = (112, 106, 98), (138, 130, 118)
    grout = (78, 72, 66)                              # 옅은 줄눈 (예전보다 훨씬 밝다)
    moss_c, dirt_c = (92, 108, 70), (96, 84, 70)
    img = Image.new("RGB", (N, N))
    px = img.load()
    for y in range(N):
        for x in range(N):
            wx = int(x + (warp(x, y) - 0.5) * 14) % N
            wy = int(y + (warp2(x, y) - 0.5) * 14) % N
            cx, cy = wx // CELL, wy // CELL
            i = owner[cy][cx]
            sx, sy, w, h = slabs[i]
            # 판석 안에서의 자리 (감아 돈다)
            lx = ((cx - sx) % CELLS) * CELL + wx % CELL
            ly = ((cy - sy) % CELLS) * CELL + wy % CELL
            W, H = w * CELL, h * CELL
            edge = min(lx, ly, W - 1 - lx, H - 1 - ly)
            g = grain(x, y)
            c = lerp(base_a, base_b, clamp(tone[i] * 0.55 + (g - 0.5) * 0.7 + 0.15))
            c = lerp(c, (c[0] * 0.96, c[1] * 0.98, c[2] * 1.03), tint[i] * 0.6)  # 판석마다 살짝 다른 색
            # 넓은 얼룩: 먼지 어두움 + 이끼 (벽 쪽·구석이 아니라 무작위로 넓게)
            b = big(x, y)
            c = lerp(c, (c[0] * 0.72, c[1] * 0.7, c[2] * 0.68), clamp((b - 0.5) * 2.4))
            m = moss(x, y)
            if m > 0.58:
                c = lerp(c, moss_c, clamp((m - 0.58) * 3.0) * 0.6)
            # 줄눈 (가늘고 옅게) + 모서리 살짝 어둡게
            if edge < 2:
                c = lerp(grout, c, 0.25 + edge * 0.2)
            elif edge < 7:
                # 위쪽 가장자리는 밝게, 아래쪽은 어둡게 (빛이 위에서 → 판석이 도톰해 보인다)
                top = ly < 7 or lx < 7
                k = (7 - edge) / 5 * 0.35
                c = lerp(c, (c[0] * 1.12, c[1] * 1.12, c[2] * 1.1) if top else (c[0] * 0.82, c[1] * 0.82, c[2] * 0.82), k)
            # 낡은 판석: 가운데부터 둥글게 흙 얼룩
            if i in chipped:
                d = math.hypot(lx - W * 0.5, ly - H * 0.5) / (min(W, H) * 0.5)
                c = lerp(c, dirt_c, clamp(1.0 - d) * 0.35)
            # 가는 금 (드물게)
            k = crack(x, y)
            if abs(k - 0.5) < 0.004 and edge > 8:
                c = lerp(c, grout, 0.6)
            px[x, y] = tuple(int(max(0, min(255, v))) for v in c)
    img = img.filter(ImageFilter.SMOOTH)
    os.makedirs(out, exist_ok=True)
    img.save(os.path.join(out, "T_Floor_Stone.png"))
    print("T_Floor_Stone", img.size, len(slabs), "slabs")


if __name__ == "__main__":
    floor(sys.argv[1] if len(sys.argv) > 1 else ".")
