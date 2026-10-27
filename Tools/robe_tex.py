"""제갈량 옷 그림 T_Zhuge_Robe_BaseColor (그래픽 손질: "제갈량 옷이 너무 어둡다").

무료 묶음의 농부 옷 그림(T_Peasant_2_BaseColor)은 갈색·올리브 계열이라 색만 곱해서는 밝아지지 않는다.
그래서 원래 그림의 밝기를 따라 색을 새로 칠한다 (무늬·주름 결은 남긴다).

- 몸통 (Male_Peasant_Body): 흰 도포, 어두운 곳은 남색 테두리
- 신 (Male_Peasant_Feet): 검은 천신
- 나머지 (팔·다리): 남색

조각마다 그림의 어느 칸을 쓰는지는 Unity에서 뽑은 UV 목록으로 안다. Unity 에디터 eval로 뽑는다:

    var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/3D/Characters/Zhuge3D.prefab");
    // 옷 재질(M_Zhuge_Outfit)을 쓰는 SkinnedMeshRenderer 조각마다 "이름|서브메시" 줄,
    // 그 아래 삼각형 꼭짓점마다 "x,y,z,u,v" 한 줄 → 텍스트 파일

사용: python Tools/robe_tex.py <uv 목록.txt> Game/Assets/Art/3D/Textures
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W = 1024
NAVY = (0.14, 0.18, 0.30)
WHITE = (0.86, 0.87, 0.89)  # 순백은 게임 조명에서 하얗게 날아가서 살짝 낮춘다
SHOE_DARK, SHOE_LIGHT = (0.05, 0.05, 0.07), (0.24, 0.25, 0.30)


def read_masks(uv_path):
    """조각 이름 → UV 칸 가림판 (0~1, 경계 번짐 막게 3픽셀 넓힘)"""
    masks, cur, pts = {}, None, []
    for line in open(uv_path, encoding='utf-8'):
        line = line.strip()
        if not line:
            continue
        if '|' in line:
            cur = line.split('|')[0]
            masks.setdefault(cur, Image.new('L', (W, W), 0))
            pts = []
            continue
        _, _, _, u, v = map(float, line.split(','))
        pts.append(((u % 1) * W, (1 - (v % 1)) * W))
        if len(pts) == 3:
            ImageDraw.Draw(masks[cur]).polygon(pts, fill=255)
            pts = []
    return {k: np.asarray(m.filter(ImageFilter.MaxFilter(7))).astype(float)[..., None] / 255 for k, m in masks.items()}


def main():
    uv_path, out_dir = sys.argv[1], sys.argv[2]
    src = np.asarray(Image.open(os.path.join(out_dir, 'T_Peasant_2_BaseColor.png')).convert('RGB')).astype(float) / 255
    lum = 0.299 * src[..., 0] + 0.587 * src[..., 1] + 0.114 * src[..., 2]

    def ramp(lo, hi, dark, light, detail):
        """밝기 lo..hi → dark..light (부드럽게) + 원래 결을 detail만큼"""
        t = np.clip((lum - lo) / (hi - lo), 0, 1)
        t = t * t * (3 - 2 * t)
        c = np.array(dark)[None, None, :] * (1 - t[..., None]) + np.array(light)[None, None, :] * t[..., None]
        return np.clip(c + (lum - 0.12)[..., None] * detail, 0, 1)

    masks = read_masks(uv_path)
    out = ramp(0.07, 0.24, NAVY, WHITE, 0.35)
    body, feet = masks.get('Male_Peasant_Body'), masks.get('Male_Peasant_Feet')
    if body is not None:
        out = out * (1 - body) + ramp(0.105, 0.155, NAVY, WHITE, 0.45) * body
    if feet is not None:
        out = out * (1 - feet) + ramp(0.06, 0.25, SHOE_DARK, SHOE_LIGHT, 0.2) * feet
    Image.fromarray((out * 255).astype(np.uint8)).save(os.path.join(out_dir, 'T_Zhuge_Robe_BaseColor.png'))
    print('ok')


if __name__ == '__main__':
    main()
