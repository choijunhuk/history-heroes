"""Unity 확률 에셋에서 한국어 공개 표를 만든다. 표준 라이브러리만 사용한다."""

from __future__ import annotations

import argparse
from collections import Counter
from datetime import date
from decimal import Decimal
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "Game/Assets/Data"
OUTPUT = ROOT / "Docs/Public/probabilities.md"
RARITIES = ("일반", "고급", "희귀", "전설")
SLOTS = ("무기", "갑옷", "목걸이", "장갑", "허리띠", "신발")
GUID = re.compile(r"guid: ([0-9a-f]{32})")


def field(text: str, key: str) -> str:
    match = re.search(rf"(?m)^  {re.escape(key)}: (.*)$", text)
    if not match:
        raise ValueError(f"필수 에셋 값 없음: {key}")
    value = match.group(1).strip()
    if value.startswith('"'):
        import json
        return json.loads(value)
    return value


def block(text: str, key: str, next_key: str) -> str:
    match = re.search(rf"(?ms)^  {re.escape(key)}:\n(.*?)^  {re.escape(next_key)}:", text)
    if not match:
        raise ValueError(f"필수 에셋 블록 없음: {key}")
    return match.group(1)


def rates(text: str, key: str, next_key: str) -> list[Decimal]:
    values = [Decimal(value) for value in re.findall(r"(?m)^    - ([0-9.]+)$", block(text, key, next_key))]
    if len(values) != 4 or sum(values) != 1:
        raise ValueError(f"{key} 등급 확률은 네 개이고 합계가 1이어야 합니다")
    return values


def nested_int(text: str, parent: str, next_key: str, key: str) -> int:
    match = re.search(rf"(?m)^    {re.escape(key)}: (\d+)$", block(text, parent, next_key))
    if not match:
        raise ValueError(f"{parent}의 {key} 값이 없습니다")
    return int(match.group(1))


def referenced_items(folder: Path, table: Path, list_key: str, stop_key: str) -> list[dict[str, str]]:
    table_text = table.read_text(encoding="utf-8")
    refs = GUID.findall(block(table_text, list_key, stop_key))
    if not refs or len(set(refs)) != len(refs):
        raise ValueError(f"{table.name}의 목록이 비었거나 중복되었습니다")
    files = {}
    for meta in folder.glob("*.asset.meta"):
        match = GUID.search(meta.read_text(encoding="utf-8"))
        if match:
            files[match.group(1)] = meta.with_suffix("")
    items = []
    for guid in refs:
        if guid not in files or not files[guid].exists():
            raise ValueError(f"{table.name}의 항목 GUID {guid}를 찾을 수 없습니다")
        source = files[guid].read_text(encoding="utf-8")
        items.append({"id": field(source, "id"), "name": field(source, "displayName"),
                      "source": source})
    ids = [item["id"] for item in items]
    if len(set(ids)) != len(ids):
        raise ValueError(f"{table.name}에 중복 ID가 있습니다")
    return items


def percent(value: Decimal) -> str:
    shown = f"{value * 100:.6f}".rstrip("0").rstrip(".")
    return f"{shown}%"


def guaranteed_final_rates(base: list[Decimal], redraw: list[Decimal]) -> list[Decimal]:
    """보장 회차에 자연 희귀 이상은 유지하고 낮은 등급만 다시 뽑는다."""
    if len(base) != 4 or len(redraw) != 4 or sum(base) != 1 or sum(redraw) != 1 or redraw[:2] != [0, 0]:
        raise ValueError("보장 확률표가 올바르지 않습니다")
    low = base[0] + base[1]
    result = [Decimal(0), Decimal(0), base[2] + low * redraw[2], base[3] + low * redraw[3]]
    if sum(result) != 1:
        raise ValueError("보장 회차의 최종 확률 합계가 100%가 아닙니다")
    return result


def assert_rules() -> None:
    equipment = (ROOT / "Game/Assets/Scripts/Equipment/GachaService.cs").read_text(encoding="utf-8")
    relic = (ROOT / "Game/Assets/Scripts/Relics/RelicGachaService.cs").read_text(encoding="utf-8")
    inventory = (ROOT / "Game/Assets/Scripts/Equipment/EquipmentInventory.cs").read_text(encoding="utf-8")
    checks = (("장비 전설 보장", "premiumPity + 1 >= box.pity", equipment),
              ("장비 누적 희귀 이상 보장", "PullsUntilRareGuarantee(save, table) == 1", equipment),
              ("장비 보장 재추첨", "RollRarity(table.tenPullGuarantee", equipment),
              ("유물 새 종류 보장", "pityBefore >= table.newRelicPity - 1", relic),
              ("유물 중복 별 성장", "RelicService.Add", relic),
              ("장비 중복 분해", "autoDismantle && duplicate", inventory),
              ("장비 확정 강화", "save.equipment.slotLevels[(int)slot]++", inventory),
              ("선택한 장비로 합성", "table.Find(resultDataId)", inventory))
    for description, needle, source in checks:
        if needle not in source:
            raise ValueError(f"{description} 구현을 확인할 수 없어 공개 문서를 생성하지 않습니다")


def render(today: date) -> str:
    assert_rules()
    gacha = (DATA / "Equipment/GachaTable.asset").read_text(encoding="utf-8")
    normal = rates(gacha, "normal", "premium")
    premium = rates(gacha, "premium", "tenPullGuarantee")
    ten_match = re.search(r"(?m)^  tenPullGuarantee:\n((?:  - [0-9.]+\n){4})", gacha)
    if not ten_match:
        raise ValueError("고급 상자 희귀 이상 보장 확률표가 없습니다")
    ten = [Decimal(value) for value in re.findall(r"  - ([0-9.]+)", ten_match.group(1))]
    if sum(ten) != 1 or ten[:2] != [0, 0]:
        raise ValueError("희귀 이상 보장 표가 올바르지 않습니다")
    guaranteed = guaranteed_final_rates(premium, ten)
    premium_pity = nested_int(gacha, "premium", "tenPullGuarantee", "pity")
    if premium_pity <= 0:
        raise ValueError("고급 상자 전설 보장 횟수가 없습니다")
    equipment = referenced_items(DATA / "Equipment", DATA / "Equipment/EquipmentTable.asset", "items", "weaponBase")
    for item in equipment:
        item["rarity"] = int(field(item["source"], "rarity"))
        item["slot"] = int(field(item["source"], "slot"))
        if item["rarity"] not in range(4) or item["slot"] not in range(6):
            raise ValueError(f"잘못된 장비 등급/슬롯: {item['id']}")
    counts = Counter(item["rarity"] for item in equipment)
    if len(equipment) != 42 or any(counts[r] == 0 for r in range(4)):
        raise ValueError("장비 목록은 42종이고 모든 등급이 있어야 합니다")

    relic_table = (DATA / "Relics/RelicTable.asset").read_text(encoding="utf-8")
    relics = referenced_items(DATA / "Relics", DATA / "Relics/RelicTable.asset", "relics", "setNames")
    relic_pity = int(field(relic_table, "newRelicPity"))
    if len(relics) != 24 or relic_pity <= 0:
        raise ValueError("유물 목록/보장 횟수가 예상과 다릅니다")
    duplicate_costs = field(relic_table, "duplicateCosts")
    if duplicate_costs != "0100000002000000":
        raise ValueError("유물 별 성장 중복 필요량이 바뀌었습니다. 설명을 확인하세요")
    equipment_table = (DATA / "Equipment/EquipmentTable.asset").read_text(encoding="utf-8")
    synthesis_count = int(field(equipment_table, "synthesisCount"))
    if synthesis_count != 3:
        raise ValueError("장비 합성 필요량이 바뀌었습니다. 설명을 확인하세요")

    lines = ["# 역사영웅 확률형 아이템 확률", "", f"마지막 갱신: {today.isoformat()}", "",
             "아래 수치는 게임의 확률 에셋에서 생성했습니다. 보장 회차는 보통 회차와 확률이 다릅니다.", "",
             "## 장비 상자", "",
             f"일반 상자는 골드 {nested_int(gacha, 'normal', 'premium', 'goldCost'):,}개 또는 하루 한 번 보상형 광고로 엽니다. 고급 상자는 보석 {nested_int(gacha, 'premium', 'tenPullGuarantee', 'gemCost'):,}개 또는 소환권 1장으로 한 번, 보석 {nested_int(gacha, 'premium', 'tenPullGuarantee', 'gem10Cost'):,}개로 열 번 엽니다. 소환권과 보석은 같은 표와 보장 횟수를 씁니다.",
             "등급을 먼저 뽑고, 해당 등급의 장비를 같은 확률로 한 개 뽑습니다.", "",
             "| 장비 | 칸 | 등급 | 일반 상자 | 고급 상자 보통 | 고급 상자 희귀 이상 보장 | 고급 상자 전설 보장 |",
             "|---|---|---|---:|---:|---:|---:|"]
    for item in equipment:
        r = item["rarity"]
        base = (normal[r] / counts[r], premium[r] / counts[r])
        lines.append(f"| {item['name']} | {SLOTS[item['slot']]} | {RARITIES[r]} | "
                     f"{percent(base[0])} | {percent(base[1])} | {percent(guaranteed[r] / counts[r])} | "
                     f"{percent(Decimal(1) / counts[r] if r == 3 else Decimal(0))} |")
    lines += ["", f"일반 상자 등급 확률: {', '.join(f'{RARITIES[i]} {percent(value)}' for i, value in enumerate(normal))}.",
              f"고급 상자 보통 등급 확률: {', '.join(f'{RARITIES[i]} {percent(value)}' for i, value in enumerate(premium))}.",
              f"희귀 이상 보장 회차의 최종 확률: 희귀 {percent(guaranteed[2])}, 전설 {percent(guaranteed[3])}. 자연스럽게 희귀 이상이 나오면 유지하고 낮은 등급만 희귀 {percent(ten[2])}·전설 {percent(ten[3])} 표로 다시 뽑습니다. 전설 보장 회차는 전설 100%입니다.",
              f"고급 상자는 전설이 나오면 횟수를 다시 셉니다. 전설 없이 {premium_pity - 1}회 열면 {premium_pity}번째는 전설 확정입니다. 보석·소환권은 횟수를 공유하며 일반 상자는 제외합니다.",
              "고급 상자에서 희귀 이상 없이 9회 열면 다음 10번째는 희귀 이상 확정입니다. 이 횟수는 낱개·10회 열기를 함께 셉니다. 전설 보장이 겹치면 전설 보장을 먼저 적용합니다.",
              "일반·고급 중복 장비는 합성 재료로 보관할 수 있습니다. 희귀·전설 중복은 설정에 따라 자동 분해하며, 가방이 가득 차면 등급별 장비 조각으로 바뀝니다.",
              "", "## 유물 뽑기", "",
              f"하루 한 번 무료, 고대 파편 {field(relic_table, 'pullShardCost')}개 또는 보석 {field(relic_table, 'pullGemCost')}개로 한 번 엽니다. 열 번은 고대 파편 {field(relic_table, 'tenPullShardCost')}개 또는 보석 {field(relic_table, 'tenPullGemCost')}개입니다. 보통 회차에는 24종을 같은 확률로 뽑으며 모든 방식은 보장 횟수를 공유합니다.", "",
              "| 유물 | 보통 회차 | 새 유물 보장 회차 |", "|---|---:|---:|"]
    for item in relics:
        lines.append(f"| {item['name']} | {percent(Decimal(1) / len(relics))} | 미보유일 때 1 ÷ 현재 미보유 종수, 보유 시 0% |")
    lines += ["", f"새 유물을 {relic_pity - 1}회 연속 얻지 못하면 {relic_pity}번째는 미보유 유물 중 균등 추첨합니다. 새 유물이 나오면 횟수는 초기화됩니다. 전종 보유 시 보장은 쉬고 모든 유물이 각각 {percent(Decimal(1) / len(relics))}입니다.",
              f"★1에서 ★2는 중복 1개, ★2에서 ★3은 추가 중복 2개가 필요합니다. ★3 중복은 고대 파편 {field(relic_table, 'overflowShards')}개로 바뀝니다.",
              "", "## 강화와 합성", "", "장비 강화는 항상 성공합니다. 같은 칸·같은 등급 장비 3개 합성의 결과는 직접 선택합니다. 유물 별 성장은 필요한 중복을 모으면 확정으로 오릅니다. 확률형 강화는 없습니다.", ""]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="공개 문서가 현재 에셋과 같은지 검사")
    parser.add_argument("--date", type=date.fromisoformat, help="갱신 날짜 (기본: 생성 시 오늘, 검사 시 문서 날짜)")
    args = parser.parse_args()
    if args.check:
        if not OUTPUT.exists():
            raise SystemExit("공개 확률 문서가 없습니다. python3 Tools/export_probabilities.py 를 실행하세요")
        previous = OUTPUT.read_text(encoding="utf-8")
        match = re.search(r"(?m)^마지막 갱신: (\d{4}-\d{2}-\d{2})$", previous)
        if not match:
            raise SystemExit("공개 확률 문서에 마지막 갱신 날짜가 없습니다")
        content = render(args.date or date.fromisoformat(match.group(1)))
        if previous != content:
            raise SystemExit("공개 확률 문서가 현재 에셋과 다릅니다. python3 Tools/export_probabilities.py 를 실행하세요")
    else:
        content = render(args.date or date.today())
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(content, encoding="utf-8")
        print(f"{OUTPUT.relative_to(ROOT)} 생성 완료")


if __name__ == "__main__":
    main()
