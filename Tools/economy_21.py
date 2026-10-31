"""21명 경제 재계산. 표준 라이브러리만 사용하며 게임·저장 파일을 바꾸지 않는다."""

from fractions import Fraction as F
from math import ceil


# 로스터: Game/Assets/Editor/SceneBuilder.Data.cs:718.
# 보석 가격: 같은 파일:158-159,175-176,194-195,206-207 및
# Game/Assets/Editor/{Boudica,Cleopatra,Tomoe,Wuzetian,Curie,Buddha,Adam}Definition.cs
# 각각:44-45,42-43,41-42,43-44,41-42,42-43,43-44.
PROGRESS_HEROES = ("칭기즈 칸", "관우", "나폴레옹", "테슬라", "이순신", "이성계")
GEM_HEROES = ("다빈치", "제갈량", "잔 다르크", "선덕여왕", "부디카", "클레오파트라",
              "토모에 고젠", "무측천", "마리 퀴리", "부처", "아담")
GODS = ("토르", "아누비스", "스사노오", "아테나")
HUMANS = len(PROGRESS_HEROES) + len(GEM_HEROES)
HEROES = HUMANS + len(GODS)
GEM_PRICE = 2000
# Game/Assets/Scripts/Heroes/HeroRoster.cs:61-75: 던전 10판, 계정당 한 명.
FREE_CHOICES = 1
FREE_CHOICE_RUNS = 10
# Game/Assets/Scripts/Meta/MetaTable.cs:46-53,70,77.
MAX_LEVEL, LEVEL_COST = 30, 200
AWAKEN_COSTS = (10, 20, 40, 80)
# Game/Assets/Scripts/Meta/HeroGrowth.cs:14-15,91-109. 5개가 현재 결정·구현값이다.
SHARD_PRICE, SHOP_LIMIT = 500, 5
PREVIOUS_LIMIT = 3
# Game/Assets/Scripts/Modes/GodTrialRules.cs:20-31,80-105,154-174.
GOD_UNLOCK, GOD_FIRST, GOD_WEEKLY = 30, 10, 5

# Game/Assets/Scripts/Economy/RunRewards.cs:19-28,32,40.
DUNGEON_BASE, DUNGEON_CHAPTER = 400, 100
CLEAR_BASE, CLEAR_CHAPTER, BOSS_GOLD, BOSS_FIRST_GEMS = 900, 100, 300, 500
# Game/Assets/Scripts/Economy/Patrol.cs:13,20: 수령당 12시간, 하루 두 번 전량 수령.
PATROL_BASE, PATROL_CHAPTER, PATROL_CAP_HOURS = 90, 12, 12
# Game/Assets/Editor/SceneBuilder.Missions.cs:64-73; Missions/Attendance.cs:16,22.
# 아래 Missions/Modes 경로의 기준 폴더는 Game/Assets/Scripts/이다.
DAILY_GOLD, DAILY_GEMS, DAILY_ANCIENT = 1000, 50, 5
WEEK_GOLD, WEEK_GEMS, WEEK_HERO, WEEK_ANCIENT = 5000, 30, 5, 20
ATTEND_GOLD, ATTEND_GEMS = 500, 50
# Modes/DailyRules.cs:18-19,29-38: 인간 17명 순환, 신은 제외.
CHALLENGE_GOLD, CHALLENGE_HERO = 1000, 1
# Survival/SurvivalData.cs:123-127; Survival/SurvivalRun.cs:404-417.
SURVIVAL_GOLD, SURVIVAL_ANCIENT_CAP = 50, 5
# Modes/RiftRules.cs:93-104: 20층 = 골드 2000/파편10, 50층 누적 = 6000/20.
RIFT_REWARDS = {20: (2000, 10), 50: (6000, 20)}
# Relics/RelicTable.cs:31; Modes/BossRun.cs:212-222. 시련 반복에는 파편이 없다.
BOSS_ANCIENT = 5
# Game/Assets/Editor/SceneBuilder.Relics.cs:18-23; RelicGachaService.cs:61-84.
RELIC_SINGLE, RELIC_TEN, RELIC_FREE = 100, 900, 1
# Docs/Design/Missions.md:244: 아직 지급 코드가 없는 무료 패스의 28일 합계.
PASS_GOLD, PASS_GEMS, PASS_ANCIENT, PASS_DAYS = 20000, 280, 60, 28
# Docs/Design/Monetization.md:63,90: 출시 계획이며 현재 구현 수입과 구분한다.
AD_GEMS, MONTHLY_DAILY, MONTHLY_INSTANT, MONTHLY_DAYS = 20, 100, 300, 30

# Docs/Design/Economy.md §4-1: 30분은 주 던전11/보스12/서바이벌10분/균열20층,
# 60분은 주 던전21/보스21/서바이벌70분/균열50층. 시간은 실측 보장이 아니다.
SCHEDULES = {"30분": (11, 12, 10, 20), "60분": (21, 21, 70, 50)}
# 비교용 가정: 5구간 보스까지 격파한 뒤 2구간을 반복, 순찰 하루 24시간.
FARM_CHAPTER, CLEARED_CHAPTERS = 2, 5
LEVEL_TOTAL = LEVEL_COST * sum(range(1, MAX_LEVEL))
SHARD_TOTAL = sum(AWAKEN_COSTS)
UNLOCK_GEMS = (len(GEM_HEROES) - FREE_CHOICES) * GEM_PRICE


def daily_income(schedule, planned=False, monthly=False):
    """주·시즌 보상을 하루 평균으로 나눈다. 첫 보상·무작위 골드는 제외한다."""
    runs, bosses, survival_minutes, rift = SCHEDULES[schedule]
    rift_gold, rift_ancient = RIFT_REWARDS[rift]
    dungeon_gold = DUNGEON_BASE + DUNGEON_CHAPTER * FARM_CHAPTER
    dungeon_gold += CLEAR_BASE + CLEAR_CHAPTER * FARM_CHAPTER
    gold = F(runs * dungeon_gold + bosses * BOSS_GOLD
             + survival_minutes * SURVIVAL_GOLD + rift_gold + WEEK_GOLD, 7)
    gold += ATTEND_GOLD + DAILY_GOLD + CHALLENGE_GOLD
    gold += 2 * PATROL_CAP_HOURS * (PATROL_BASE + PATROL_CHAPTER * CLEARED_CHAPTERS)
    gems = ATTEND_GEMS + DAILY_GEMS + F(WEEK_GEMS, 7)
    ancient = DAILY_ANCIENT + F(WEEK_ANCIENT + bosses * BOSS_ANCIENT
                               + survival_minutes // 10 * SURVIVAL_ANCIENT_CAP
                               + rift_ancient, 7)
    if planned:
        gold += F(PASS_GOLD, PASS_DAYS)
        gems += F(PASS_GEMS, PASS_DAYS) + AD_GEMS
        ancient += F(PASS_ANCIENT, PASS_DAYS)
    if monthly:
        if not planned:
            raise ValueError("월정액은 출시 계획에만 더합니다")
        gems += MONTHLY_DAILY + F(MONTHLY_INSTANT, MONTHLY_DAYS)
    return gold, gems, ancient


def resource_days(cost, income):
    """기간 보상을 평탄화한 예산 환산일. 실제 수령일을 예측하지 않는다."""
    return "적립 불가" if income <= 0 else ceil(F(cost) / income)


def shard_days(target, shop, weekly=True, daily_pool=False):
    """주간 보상은 7일째부터. 전원 분배가 낭비 없이 된다는 조건부 하한이다."""
    day = 0
    while True:
        received = shop * day
        received += WEEK_HERO * (day // 7) if weekly else 0
        received += CHALLENGE_HERO * day if daily_pool else 0
        if received >= target:
            return day
        day += 1


def main_shard_days(shop, human):
    """주력 집중: 주간 5 전부 배분, 인간 일일 순환의 시작 위치 17가지 비교."""
    results = []
    for phase in range(HUMANS if human else 1):
        day = 0
        while (shop * day + WEEK_HERO * (day // 7)
               + ((day + phase) // HUMANS if human else 0)) < SHARD_TOTAL:
            day += 1
        results.append(day)
    return f"{min(results)}~{max(results)}" if min(results) != max(results) else str(results[0])


def god_days(count, use_current_week):
    """이번 주 수령 뒤 월요일마다 즉시 받으면 고정 간격보다 한 주 일찍 끝날 수 있다."""
    claims = count * ((GOD_UNLOCK - GOD_FIRST) // GOD_WEEKLY)
    lower = (claims - (2 if use_current_week else 1)) * 7
    return lower, lower + 7


def free_choice_days(schedule):
    runs = (2, 2, 2, 2, 2, 1, 0) if schedule == "30분" else (3,) * 7
    results = []
    for start in range(7):
        day = total = 0
        while total < FREE_CHOICE_RUNS:
            total += runs[(start + day) % 7]
            day += 1
        results.append(day)
    return min(results), max(results)


def table(title, headers, rows):
    print(f"\n#### {title}\n")
    print("| " + " | ".join(headers) + " |")
    print("|" + "---|" * len(headers))
    for row in rows:
        print("| " + " | ".join(str(value) for value in row) + " |")
    print()


def main():
    # 금액·달력·상한 경계를 고정해 계산식의 실수를 바로 드러낸다.
    assert (HEROES, HUMANS, len(GEM_HEROES), UNLOCK_GEMS) == (21, 17, 11, 20000)
    assert (LEVEL_TOTAL, SHARD_TOTAL) == (87000, 150)
    assert shard_days(21, SHOP_LIMIT, weekly=False) == 5
    assert shard_days(26, SHOP_LIMIT) == 6
    assert daily_income("30분")[1] == F(730, 7)
    assert daily_income("30분", True)[1] == F(940, 7)
    assert god_days(4, True) == (98, 105)
    assert free_choice_days("30분") == (5, 7)
    print("21명 기준 경제 재계산 (2026-10-31 결정 반영)")
    print("금액은 정수, 일일 평균은 소수 둘째 자리, 기간은 올림. 1개월=30일.")
    print("모든 수입원 개방·5구간 격파 후 2구간 반복·순찰 하루 2번·미션 전부 수령 가정.")
    print("첫 보상/업적/레벨업/선택 보상방/금화주머니 제외. 시작일부터의 실제 달력 예측은 아님.")

    profiles = [("구현·무과금 가볍게 30분", "30분", False, False),
                ("구현·무과금 매일 60분", "60분", False, False),
                ("출시안·무과금 30분", "30분", True, False),
                ("출시안·무과금 60분", "60분", True, False),
                ("출시안·소액 월정액 30분", "30분", True, True)]
    incomes = [daily_income(schedule, planned, monthly)
               for _, schedule, planned, monthly in profiles]
    table("하루 반복 수입", ["유형", "골드", "보석", "고대 파편"],
          [(name, *(f"{float(value):,.2f}" for value in income))
           for (name, *_), income in zip(profiles, incomes)])
    print(f"각성 조각: 별도 구매 골드로 상점 {SHOP_LIMIT}/일 + 선택 5/주 + 인간 순환 1/일(계정 합계).")
    print("시련 2승/주가 반복 보스 2회를 대체하면 고대 파편은 위 표에서 10/7=1.43/일 감소.")

    table("해금 비용", ["구분", "인원", "보석", "해금 조각"],
          [("처음부터·진행 해금", len(PROGRESS_HEROES), 0, 0),
           ("인간 보석 전용·무료 선택 전", len(GEM_HEROES), f"{len(GEM_HEROES)*GEM_PRICE:,}", 0),
           ("10판 무료 선택 적용 후 인간", HUMANS, f"{UNLOCK_GEMS:,}", 0),
           ("신", len(GODS), 0, f"{len(GODS)*GOD_UNLOCK} (첫 40 + 주간 80)"),
           ("21명 합계·직접 해금 골드 0", HEROES, f"{UNLOCK_GEMS:,}", len(GODS)*GOD_UNLOCK)])
    print("무료 선택 10판까지: 30분 유저 {}~{}일, 60분 유저 {}~{}일 (요일별 판수 가정).".format(
        *free_choice_days("30분"), *free_choice_days("60분")))

    table("인원 증가에 따른 영웅 성장 비용", ["대상", "Lv30 골드", "각성 ★5 조각"],
          [("주력 1명" if n == 1 else f"{n}명", f"{n*LEVEL_TOTAL:,}", f"{n*SHARD_TOTAL:,}")
           for n in (1, 8, 9, 10, 19, HEROES)])
    print("8→21명 증가분: 골드 +1,131,000 / 각성 조각 +1,950. 신 해금 120은 별도 소비.")
    print("각성 조각 전량 상점 구매: 주력 75,000 / 전원 1,575,000골드 추가.")

    god_rows = []
    for n in range(1, len(GODS) + 1):
        claims = n * ((GOD_UNLOCK - GOD_FIRST) // GOD_WEEKLY)
        regular, reset = god_days(n, False), god_days(n, True)
        god_rows.append((f"신 {n}명", claims, f"{regular[0]}~{regular[1]}일", f"{reset[0]}~{reset[1]}일"))
    table("신 해금 시간·시련 조건을 갖춘 때부터", ["목표", "공유 주간 보상 횟수", "고정 7일 간격", "이번 주+리셋 즉시"], god_rows)
    print("고정 간격은 첫 수령 0~7일 뒤 가정. 리셋 즉시는 이번 주 수령 후 매주 월요일 즉시 수령 가정.")
    print("본인 기록 ID가 다르고 액티브 구성의 대칭 차이가 2개 이상인 2승, 월요일 05:00 KST 갱신.")
    print("전투에 걸리는 시간은 별도이며 주간 횟수 제한이 실제로 16회 필요하다.")

    gem_rates = [incomes[i][1] for i in (0, 2, 4)]
    table("보석 목표까지의 적립일·다른 보석 지출 0", ["목표", "보석", "구현 무과금", "출시안 무과금", "출시안 월정액"],
          [(label, f"{cost:,}", *(resource_days(cost, rate) for rate in gem_rates))
           for label, cost in (("11명 전부 구매", len(GEM_HEROES)*GEM_PRICE),
                               ("무료 1명 후 전원", UNLOCK_GEMS),
                               ("위 목표 + 유물 10회 한 번", UNLOCK_GEMS+RELIC_TEN),
                               ("위 목표 - 보스 5종 첫 보석", UNLOCK_GEMS-5*BOSS_FIRST_GEMS))])
    lower, upper = god_days(len(GODS), True)
    table("전원 해금·리셋 활용 비교 기간 (보스 첫 보석 제외)", ["구현 무과금", "출시안 무과금", "출시안 월정액"],
          [tuple(f"{max(resource_days(UNLOCK_GEMS, rate), lower)}~{max(resource_days(UNLOCK_GEMS, rate), upper)}일"
                 for rate in gem_rates)])
    print(f"시련 준비일 T=0인 비교값. 같은 평균 모델에 준비일을 넣으면 max(보석 환산일, T+{lower}~{upper}일, 인간 진행/10판 조건일).")
    table("유물 보석 지출을 계속할 때·무료/파편 뽑기는 별도", ["지출", "보석/일", "인간 전원 구현", "인간 전원 출시안", "인간 전원 월정액"],
          [(label, f"{float(spend):.2f}", *(resource_days(UNLOCK_GEMS, rate-spend) for rate in gem_rates))
           for label, spend in (("보석 사용 없음", F(0)), ("28일마다 10회", F(RELIC_TEN, 28)),
                                ("7일마다 10회", F(RELIC_TEN, 7)))])
    print(f"유물 가격 {RELIC_SINGLE}/회·{RELIC_TEN}/10회, 무료 {RELIC_FREE}/일. 21명이어도 24종·연결 첫 유물 8종 유지.")

    table("각성 병목·해금 후 공급이 모두 열린 상태", ["목표·계산 조건", "상점 3/일 이전 기준", "상점 5/일 현재 기준"],
          [("주력 인간·주간 5 집중+17명 순환", main_shard_days(PREVIOUS_LIMIT, True), main_shard_days(SHOP_LIMIT, True)),
           ("주력 신·주간 5 집중, 일일 순환 없음", main_shard_days(PREVIOUS_LIMIT, False), main_shard_days(SHOP_LIMIT, False)),
           ("21명·상점만", shard_days(HEROES*SHARD_TOTAL, PREVIOUS_LIMIT, False), shard_days(HEROES*SHARD_TOTAL, SHOP_LIMIT, False)),
           ("21명·상점+주간 5", shard_days(HEROES*SHARD_TOTAL, PREVIOUS_LIMIT), shard_days(HEROES*SHARD_TOTAL, SHOP_LIMIT)),
           ("21명·위+인간 순환 전부 유효", shard_days(HEROES*SHARD_TOTAL, PREVIOUS_LIMIT, daily_pool=True),
            shard_days(HEROES*SHARD_TOTAL, SHOP_LIMIT, daily_pool=True)),
           ("종전 75%상점 가정·나머지 공급 미보장", ceil(F(HEROES*SHARD_TOTAL*3, 4*PREVIOUS_LIMIT)),
            ceil(F(HEROES*SHARD_TOTAL*3, 4*SHOP_LIMIT)))])
    print("단위=일. 합계 하한은 개별 영웅별 배분/해금 전 제한/이미 최대인 영웅 중복에 따라 늦어진다.")
    table("골드만 모으는 데 필요한 일수·다른 지출 0", ["목표", "골드", "구현 30분", "구현 60분"],
          [(label, f"{cost:,}", resource_days(cost, incomes[0][0]), resource_days(cost, incomes[1][0]))
           for label, cost in (("주력 Lv30", LEVEL_TOTAL), ("전원 Lv30", HEROES*LEVEL_TOTAL),
                               ("주력 Lv30+각성 전량 상점", LEVEL_TOTAL+SHARD_TOTAL*SHARD_PRICE),
                               ("전원 Lv30+각성 전량 상점", HEROES*(LEVEL_TOTAL+SHARD_TOTAL*SHARD_PRICE)))])
    print("수련/장비/룬/펫/기록 용량 등 공용 성장 지출은 이 영웅 비용 표 밖이다. 21배로 곱하지 않는다.")


if __name__ == "__main__":
    main()
