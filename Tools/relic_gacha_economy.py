"""유물 설계의 30분 무과금 기대값을 같은 난수로 다시 계산한다. 외부 패키지는 쓰지 않는다."""

import argparse
import random
import statistics


def simulate(rng, daily_shards, hero_grants):
    copies = [1] * hero_grants + [0] * (24 - hero_grants)
    pity = pulls = shards = overflow = 0
    capacity_left = 300

    def draw():
        nonlocal pity, pulls, shards, overflow
        missing = [i for i, count in enumerate(copies) if count == 0]
        index = rng.choice(missing) if missing and pity >= 19 else rng.randrange(24)
        is_new = copies[index] == 0
        if copies[index] >= 4:
            shards += 5
            overflow += 1
        else:
            copies[index] += 1
        pity = 0 if is_new or not missing else pity + 1
        pulls += 1

    for day in range(1, 2000):
        shards += daily_shards
        spent = min(shards, capacity_left)
        capacity_left -= spent
        shards -= spent
        if day < 7:
            continue
        draw()
        while shards >= 900 and min(copies) < 4:
            shards -= 900
            for _ in range(10):
                draw()
        if min(copies) >= 4:
            return day, pulls, overflow
    raise RuntimeError("계산 기간 안에 끝나지 않았습니다")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--samples", type=int, default=20000)
    parser.add_argument("--seed", type=int, default=729)
    parser.add_argument("--daily-shards", type=int, default=17)
    parser.add_argument("--hero-grants", type=int, choices=range(9), default=8)
    args = parser.parse_args()
    if args.samples < 1 or args.daily_shards < 0:
        parser.error("계정 수는 1 이상, 하루 파편은 0 이상이어야 합니다")
    rng = random.Random(args.seed)
    results = [simulate(rng, args.daily_shards, args.hero_grants) for _ in range(args.samples)]
    days, pulls, overflow = (statistics.mean(values) for values in zip(*results))
    print(f"계정 {args.samples:,} · 난수 {args.seed} · 하루 파편 {args.daily_shards} · 첫 클리어 지급 {args.hero_grants}")
    print(f"평균 뽑기 {pulls:.2f}회 · {days:.2f}일 · {days / 30:.2f}개월 · 초과 중복 {overflow:.2f}개")


if __name__ == "__main__":
    main()
