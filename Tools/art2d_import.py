"""코덱스가 만든 2D 그림을 게임에 넣을 크기로 다듬는다 (2D 시험판, 개발자: "코덱스 이미지 2D 시험판").

- 캐릭터·소품: 투명한 가장자리를 잘라 내고 (몸 둘레 8px 여백), 키를 정한 픽셀로 줄인다
- 바닥: 이음새 없는 무늬라 자르지 않고 크기만 줄인다
- UI: 투명 가장자리만 자른다 (9칸 늘리기는 Unity 쪽에서 테두리 값)

사용: python Tools/art2d_import.py <코덱스 그림 폴더> Game/Assets/Art/2D
"""
import os
import sys

from PIL import Image

# 이름 → (폴더, 다듬기, 목표 키 px). 다듬기: trim = 가장자리 자르기 + 키 맞춤, tile = 크기만, ui = 가장자리만
CHARACTER_PX = 512
RULES = {
    'guan': ('Characters', 'trim', CHARACTER_PX),
    'yisunsin': ('Characters', 'trim', CHARACTER_PX),
    'enemy_charger': ('Characters', 'trim', CHARACTER_PX),
    'enemy_archer': ('Characters', 'trim', CHARACTER_PX),
    'enemy_bomber': ('Characters', 'trim', 400),
    'floor': ('Stage', 'tile', 512),
    'wall': ('Stage', 'ui', 0),
    'door': ('Stage', 'trim', 384),
    'pillar': ('Stage', 'trim', 384),
    'crate': ('Stage', 'trim', 256),
    'brazier': ('Stage', 'trim', 320),
    'banner': ('Stage', 'trim', 384),
    'water': ('Stage', 'tile', 512),
    'berry': ('Stage', 'trim', 256),
    'heal': ('Stage', 'trim', 128),
    'moss': ('Stage', 'ui', 0),
    'skull': ('Stage', 'trim', 256),
    'thorns': ('Stage', 'ui', 0),
    'pet_': ('Characters', 'trim', 256),
    'card': ('UI', 'ui', 0),
    'panel': ('UI', 'ui', 0),
    'button': ('UI', 'ui', 0),
    'ribbon': ('UI', 'ui', 0),
}
MARGIN = 8


def rule_for(name):
    for prefix, rule in RULES.items():
        if name.startswith(prefix):
            return rule
    return None


def trim(im):
    bbox = im.getchannel('A').point(lambda a: 255 if a > 8 else 0).getbbox()
    if not bbox:
        return im
    l, t, r, b = bbox
    return im.crop((max(0, l - MARGIN), max(0, t - MARGIN), min(im.width, r + MARGIN), min(im.height, b + MARGIN)))


def main():
    src, dst = sys.argv[1], sys.argv[2]
    done = []
    for f in sorted(os.listdir(src)):
        if not f.endswith('.png'):
            continue
        name = f[:-4]
        rule = rule_for(name)
        if rule is None:
            print('건너뜀', f)
            continue
        folder, mode, px = rule
        im = Image.open(os.path.join(src, f)).convert('RGBA')
        if mode == 'trim':
            im = trim(im)
            if px and im.height > px:
                im = im.resize((max(1, round(im.width * px / im.height)), px), Image.LANCZOS)
        elif mode == 'tile':
            im = im.resize((px, px), Image.LANCZOS)
        else:
            im = trim(im)
        os.makedirs(os.path.join(dst, folder), exist_ok=True)
        im.save(os.path.join(dst, folder, f), optimize=True)
        done.append(f'{folder}/{f} {im.size[0]}x{im.size[1]}')

    with open(os.path.join(dst, 'CREDITS.txt'), 'w', encoding='utf-8', newline='\n') as c:
        c.write('2D 그림 (시험판): OpenAI Codex 이미지 생성으로 만든 그림 (개발자 소유, 상업 사용 가능).\n')
        c.write('다듬기: Tools/art2d_import.py (자르기 · 크기 줄이기)\n')
    print('\n'.join(done))


if __name__ == '__main__':
    main()
