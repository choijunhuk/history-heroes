using HistoryHeroes.Equipment;
using HistoryHeroes.Meta;
using HistoryHeroes.Stats;
using UnityEditor;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.4 판 밖 성장 데이터: 장비 21종 (Equipment 7장) + 장비 표 + 판 밖 성장 표</summary>
    public static partial class SceneBuilder
    {
        private static MetaTable CreateMetaData()
        {
            var table = AssetUtil.LoadOrCreate<EquipmentTable>($"{DataDir}/Equipment/EquipmentTable.asset");
            table.items.Clear();

            // 🗡️ 무기: 공격력 % (부가: 치명타 피해 / 쿨다운 감소 / 치명타 확률)
            table.items.Add(Equip("weapon_recruit_c", "수습병의 무기", EquipSlot.Weapon, EquipRarity.Common));
            table.items.Add(Equip("weapon_elite_u", "정예병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CritDamage, 0.05f));
            table.items.Add(Equip("weapon_scout_u", "척후병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CooldownReduction, 0.01f));
            table.items.Add(Equip("weapon_general_r", "장군의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CritDamage, 0.10f));
            table.items.Add(Equip("weapon_strategist_r", "책사의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CooldownReduction, 0.02f));
            table.items.Add(Equip("weapon_timeguard_l", "시간 수호자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritRate, 0.03f));
            table.items.Add(Equip("weapon_conqueror_l", "정복자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritDamage, 0.20f));

            // 🛡️ 갑옷: 최대체력 % (부가: 피해 감소 / 이동속도)
            table.items.Add(Equip("armor_recruit_c", "수습병의 갑옷", EquipSlot.Armor, EquipRarity.Common));
            table.items.Add(Equip("armor_elite_u", "정예병의 갑옷", EquipSlot.Armor, EquipRarity.Uncommon, StatType.DamageReduction, 0.01f));
            table.items.Add(Equip("armor_pilgrim_u", "순례자의 외투", EquipSlot.Armor, EquipRarity.Uncommon, StatType.MoveSpeed, 0.01f, ModifierKind.Percent));
            table.items.Add(Equip("armor_general_r", "장군의 갑옷", EquipSlot.Armor, EquipRarity.Rare, StatType.DamageReduction, 0.03f));
            table.items.Add(Equip("armor_explorer_r", "탐험가의 외투", EquipSlot.Armor, EquipRarity.Rare, StatType.MoveSpeed, 0.015f, ModifierKind.Percent));
            table.items.Add(Equip("armor_timeguard_l", "시간 수호자의 갑옷", EquipSlot.Armor, EquipRarity.Legendary, StatType.DamageReduction, 0.06f));
            table.items.Add(Equip("armor_unyielding_l", "불굴의 망토", EquipSlot.Armor, EquipRarity.Legendary, StatType.MoveSpeed, 0.02f, ModifierKind.Percent));

            // 💍 장신구: 공격속도 % (부가: 치명타 확률 / 이동속도 / 쿨다운 감소). "부적"은 행운 장식으로 그린다
            table.items.Add(Equip("acc_charm_c", "낡은 부적", EquipSlot.Accessory, EquipRarity.Common));
            table.items.Add(Equip("acc_feather_u", "매의 깃털 장식", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.CritRate, 0.01f));
            table.items.Add(Equip("acc_compass_u", "여행자의 나침반", EquipSlot.Accessory, EquipRarity.Uncommon, StatType.MoveSpeed, 0.02f, ModifierKind.Percent));
            table.items.Add(Equip("acc_signet_r", "황금 인장 반지", EquipSlot.Accessory, EquipRarity.Rare, StatType.CritRate, 0.015f));
            table.items.Add(Equip("acc_watch_r", "학자의 회중시계", EquipSlot.Accessory, EquipRarity.Rare, StatType.CooldownReduction, 0.02f));
            table.items.Add(Equip("acc_hourglass_l", "시간의 모래시계", EquipSlot.Accessory, EquipRarity.Legendary, StatType.CritRate, 0.02f));
            table.items.Add(Equip("acc_stirrup_l", "바람의 등자", EquipSlot.Accessory, EquipRarity.Legendary, StatType.MoveSpeed, 0.03f, ModifierKind.Percent));
            EditorUtility.SetDirty(table);

            // 룬 7종 (Runes 3장)
            var runes = AssetUtil.LoadOrCreate<HistoryHeroes.Runes.RuneTable>($"{DataDir}/Runes/RuneTable.asset");
            runes.runes.Clear();
            runes.runes.Add(Rune("rune_fire", "불의 룬", Skills.SkillTag.Fire, "지속 피해형 (불꽃 장판, 불꽃 발자국, 유성)", HistoryHeroes.Runes.RuneSource.BossChoice));
            runes.runes.Add(Rune("rune_lightning", "번개의 룬", Skills.SkillTag.Lightning, "번개 광역형 (낙뢰, 테슬라 코일)", HistoryHeroes.Runes.RuneSource.BossChoice));
            runes.runes.Add(Rune("rune_projectile", "투사체의 룬", Skills.SkillTag.Projectile, "연사형 (멀티샷, 관통, 벽 튕김)", HistoryHeroes.Runes.RuneSource.BossChoice));
            runes.runes.Add(Rune("rune_melee", "근접의 룬", Skills.SkillTag.Melee, "칼날 탱커형, 관우", HistoryHeroes.Runes.RuneSource.Shop));
            runes.runes.Add(Rune("rune_survival", "생존의 룬", Skills.SkillTag.Survival, "흡혈, 방어력, 최대체력", HistoryHeroes.Runes.RuneSource.Shop));
            runes.runes.Add(Rune("rune_training", "수련의 룬", Skills.SkillTag.Stat, "공격력, 공격속도 같은 기본기", HistoryHeroes.Runes.RuneSource.Shop));
            HistoryHeroes.Runes.RuneData bond = Rune("rune_bond", "인연의 룬", Skills.SkillTag.None, "조합 노리기 (발견한 조합의 재료)", HistoryHeroes.Runes.RuneSource.Survival);
            bond.bond = true;
            runes.runes.Add(bond);
            EditorUtility.SetDirty(runes);

            // 유물 20개 = 세트 5 × 4 (Relics 3·4장). 획득처는 유물마다 하나
            var relics = AssetUtil.LoadOrCreate<HistoryHeroes.Relics.RelicTable>($"{DataDir}/Relics/RelicTable.asset");
            relics.relics.Clear();
            const HistoryHeroes.Relics.RelicSource Hero = HistoryHeroes.Relics.RelicSource.HeroFirstClear;
            const HistoryHeroes.Relics.RelicSource Boss = HistoryHeroes.Relics.RelicSource.BossFirstClear;
            const HistoryHeroes.Relics.RelicSource StarsSource = HistoryHeroes.Relics.RelicSource.ChapterStars;
            const HistoryHeroes.Relics.RelicSource Survival = HistoryHeroes.Relics.RelicSource.Survival;
            const HistoryHeroes.Relics.RelicSource Achievement = HistoryHeroes.Relics.RelicSource.Achievement;
            // 1. 발명의 시대
            relics.relics.Add(Relic("tesla_notes", "테슬라의 연구 노트", 0, "테슬라는 교류 전기와 무선 송전을 연구했다", Hero, 0, "tesla", "니콜라 테슬라로 던전 첫 클리어"));
            relics.relics.Add(Relic("davinci_sketch", "다빈치의 비행기계 스케치", 0, "레오나르도 다빈치는 새의 날개를 본뜬 비행 장치를 스케치로 남겼다", Achievement, 0, "recipes5", "조합 5종 발견"));
            relics.relics.Add(Relic("galileo_telescope", "갈릴레이의 망원경", 0, "갈릴레이는 망원경으로 목성의 위성을 관찰했다", Survival, 0, "two_heroes", "서바이벌을 영웅 2명으로 클리어"));
            relics.relics.Add(Relic("antikythera", "안티키테라 기계", 0, "고대 그리스의 천체 계산 장치. 톱니바퀴로 이루어졌다", Boss, 3, null, "3구간 보스 최초 격파"));
            // 2. 기록과 문자
            relics.relics.Add(Relic("rosetta_fragment", "로제타석 조각", 1, "같은 내용이 세 가지 문자로 새겨져 이집트 문자 해독의 열쇠가 되었다", Boss, 2, null, "2구간 보스 최초 격파"));
            relics.relics.Add(Relic("hunminjeongeum", "훈민정음 해례본", 1, "한글을 만든 원리와 쓰임을 설명한 책", Achievement, 0, "recipes10", "조합 10종 발견"));
            relics.relics.Add(Relic("gutenberg_type", "구텐베르크의 활자", 1, "유럽 활판 인쇄술을 퍼뜨린 금속 활자", Achievement, 0, "records10", "기록 10개 남기기"));
            relics.relics.Add(Relic("magna_carta", "마그나 카르타 사본", 1, "왕의 권력을 법으로 제한한 영국의 문서", Achievement, 0, "heroes4", "영웅 4명 해금"));
            // 3. 바다와 항해
            relics.relics.Add(Relic("turtle_ship_plan", "거북선 설계도", 2, "조선 수군의 철갑 지붕을 얹은 전선 (설계도는 게임 각색)", StarsSource, 1, null, "1구간 별 6개 (던전 3 + 보스 3)"));
            relics.relics.Add(Relic("zhenghe_compass", "정화 함대의 나침반", 2, "명나라 정화의 대규모 원정 항해", StarsSource, 2, null, "2구간 별 6개"));
            relics.relics.Add(Relic("viking_sunstone", "바이킹의 태양석", 2, "흐린 날 해의 위치를 찾았다고 전해지는 광물 (전승)", Boss, 4, null, "4구간 보스 최초 격파"));
            relics.relics.Add(Relic("magellan_log", "마젤란의 항해 일지", 2, "세계 일주 항해의 기록", StarsSource, 3, null, "3구간 별 6개"));
            // 4. 장수의 유품
            relics.relics.Add(Relic("guan_blade", "청룡언월도 조각", 3, "『삼국지연의』 속 관우의 무기 (전승)", Hero, 0, "guan", "관우로 던전 첫 클리어"));
            relics.relics.Add(Relic("napoleon_bicorne", "나폴레옹의 이각모", 3, "나폴레옹의 상징이 된 모자", Hero, 0, "napoleon", "나폴레옹으로 던전 첫 클리어"));
            relics.relics.Add(Relic("khan_paiza", "칭기즈 칸의 파이자", 3, "몽골 제국의 통행·권한 증표", Hero, 0, "khan", "칭기즈 칸으로 던전 첫 클리어"));
            relics.relics.Add(Relic("caesar_laurel", "카이사르의 월계관", 3, "로마에서 승리와 영예를 상징한 관", Boss, 5, null, "5구간 보스 최초 격파"));
            // 5. 하늘과 시간
            relics.relics.Add(Relic("rain_gauge", "측우기", 4, "조선 시대에 비의 양을 재던 기구", Boss, 1, null, "1구간 보스 최초 격파"));
            relics.relics.Add(Relic("armillary_sphere", "혼천의", 4, "하늘의 움직임을 나타낸 천문 기구", StarsSource, 4, null, "4구간 별 6개"));
            relics.relics.Add(Relic("sundial", "앙부일구", 4, "그림자로 시각을 알려준 조선의 해시계", Survival, 0, "survive5", "서바이벌 5분 생존"));
            relics.relics.Add(Relic("astrolabe", "아스트롤라베", 4, "별의 높이를 재던 천문·항해 도구", Survival, 0, "clear10", "서바이벌 10분 클리어"));
            EditorUtility.SetDirty(relics);

            var meta = AssetUtil.LoadOrCreate<MetaTable>($"{DataDir}/Meta/MetaTable.asset");
            meta.equipment = table;
            meta.runes = runes;
            meta.relics = relics;
            meta.pets = CreatePets();
            meta.missions = CreateMissions();
            EditorUtility.SetDirty(meta);

            // 상자 표 (Gacha 2장): 화면 확률과 추첨이 같이 읽는다. 숫자는 GachaTable 기본값
            var gacha = AssetUtil.LoadOrCreate<GachaTable>($"{DataDir}/Equipment/GachaTable.asset");
            gacha.equipment = table;
            EditorUtility.SetDirty(gacha);
            return meta;
        }

        private static HistoryHeroes.Relics.RelicData Relic(string id, string name, int set, string fact, HistoryHeroes.Relics.RelicSource source,
            int chapter, string value, string howToGet)
        {
            var r = AssetUtil.LoadOrCreate<HistoryHeroes.Relics.RelicData>($"{DataDir}/Relics/Relic_{id}.asset");
            r.id = id;
            r.displayName = name;
            r.set = set;
            r.fact = fact;
            r.source = source;
            r.sourceChapter = chapter;
            r.sourceValue = value;
            r.howToGet = howToGet;
            EditorUtility.SetDirty(r);
            return r;
        }

        /// <summary>펫 4마리 (Pets 3장) + 성장 표 (4장: Lv1~10, +0.3%/Lv, 인연 +2%, 1,500×n 골드)</summary>
        private static HistoryHeroes.Pets.PetTable CreatePets()
        {
            var table = AssetUtil.LoadOrCreate<HistoryHeroes.Pets.PetTable>($"{DataDir}/Pets/PetTable.asset");
            table.pets.Clear();
            table.pets.Add(Pet("hawk", "사냥매", HistoryHeroes.Pets.PetKind.Hawk, "khan", "칭기즈 칸", HistoryHeroes.Pets.PetSource.FirstDungeonClear, 0,
                new UnityEngine.Color(0.72f, 0.52f, 0.28f), "멈춰서 버티기",
                "2초 멈추면 하늘로 솟구쳤다가 화면에서 체력이 가장 높은 적에게 급강하 (공격력 30%) + 사냥 표식 5초: 그 적에게 기본 공격 치명타 확률 +25%. 재사용 6초",
                "표식 대상이 죽으면 표식이 가장 가까운 적에게 옮겨 간다", "4초 넘게 멈추면 한 번 더 급강하",
                "몽골인들은 매사냥을 즐겼다.", "칭기즈 칸의 매가 독이 든 물그릇을 쳐서 그를 구했다는 이야기가 전해진다."));
            table.pets.Add(Pet("red_hare", "적토마", HistoryHeroes.Pets.PetKind.RedHare, "guan", "관우", HistoryHeroes.Pets.PetSource.BossFirstClear, 1,
                new UnityEngine.Color(0.85f, 0.28f, 0.18f), "계속 움직이기",
                "1.5초 넘게 움직이면 옆에서 질주 (이동속도 +15%, 앞의 적을 들이받아 공격력 10% + 밀치기). 질주하다 멈추면 기세: 2초 동안 기본 공격 피해 +20% (4초에 한 번)",
                "질주한 길에 먼지 구름 (적 이동속도 -30%, 2초)", "기세 3초 + 기세 중 받는 피해 -15%",
                "", "『삼국지연의』에서 여포에 이어 관우가 탄 명마. \"사람 중엔 여포, 말 중엔 적토\"라는 말이 전해진다."));
            table.pets.Add(Pet("marengo", "마렝고", HistoryHeroes.Pets.PetKind.Marengo, "napoleon", "나폴레옹", HistoryHeroes.Pets.PetSource.BossFirstClear, 3,
                new UnityEngine.Color(0.78f, 0.78f, 0.82f), "위험한 순간",
                "적 투사체가 닿기 0.3초 전에 영웅 앞을 막아서서 1발을 몸으로 받는다. 재사용 7초 (체력 30% 아래면 4초). 돌진 같은 근접 공격은 못 막는다",
                "막아낸 투사체를 쏜 적에게 되돌려 보낸다 (그 피해 100%)", "한 번에 2발까지 막는다",
                "나폴레옹이 탔던 것으로 유명한 아랍 종 회색 말. 여러 전투에 함께했다 (이름은 마렝고 전투에서 따왔다).", ""));
            table.pets.Add(Pet("dove", "흰 비둘기", HistoryHeroes.Pets.PetKind.Dove, "tesla", "니콜라 테슬라", HistoryHeroes.Pets.PetSource.SurvivalFirstClear, 0,
                new UnityEngine.Color(0.96f, 0.97f, 1f), "돌아다니기",
                "멀리 떨어진 경험치 보석을 하나씩 물어 온다 (경험치 양은 같다). 던전 방마다 숨은 보물 1개를 찾아 표시 (골드, 10%는 회복 열매). 멈추면 가까운 적에게 약한 전기 (공격력 15%, 2초마다)",
                "보물 골드 5% 확률로 2배", "회복 열매 확률 25% + 서바이벌에서 30초마다 화면 보석을 전부 끌어온다",
                "테슬라는 말년에 비둘기들을 돌봤고, 특히 흰 비둘기 한 마리를 아꼈다고 전해진다.", ""));
            EditorUtility.SetDirty(table);
            return table;
        }

        private static HistoryHeroes.Pets.PetData Pet(string id, string name, HistoryHeroes.Pets.PetKind kind, string bondHero, string bondName,
            HistoryHeroes.Pets.PetSource source, int chapter, UnityEngine.Color color, string trigger, string effect, string awaken5, string awaken10, string fact, string legend)
        {
            var p = AssetUtil.LoadOrCreate<HistoryHeroes.Pets.PetData>($"{DataDir}/Pets/Pet_{id}.asset");
            p.id = id;
            p.displayName = name;
            p.kind = kind;
            p.bondHeroId = bondHero;
            p.bondHeroName = bondName;
            p.source = source;
            p.sourceChapter = chapter;
            p.color = color;
            p.trigger = trigger;
            p.effect = effect;
            p.awaken5 = awaken5;
            p.awaken10 = awaken10;
            p.fact = fact;
            p.legend = legend;
            EditorUtility.SetDirty(p);
            return p;
        }

        private static HistoryHeroes.Runes.RuneData Rune(string id, string name, Skills.SkillTag tag, string aim, HistoryHeroes.Runes.RuneSource source)
        {
            var r = AssetUtil.LoadOrCreate<HistoryHeroes.Runes.RuneData>($"{DataDir}/Runes/Rune_{id}.asset");
            r.id = id;
            r.displayName = name;
            r.tag = tag;
            r.bond = false;
            r.aim = aim;
            r.source = source;
            EditorUtility.SetDirty(r);
            return r;
        }

        private static EquipmentData Equip(string id, string name, EquipSlot slot, EquipRarity rarity,
            StatType? secondary = null, float value = 0f, ModifierKind kind = ModifierKind.Flat)
        {
            var e = AssetUtil.LoadOrCreate<EquipmentData>($"{DataDir}/Equipment/Equip_{id}.asset");
            e.id = id;
            e.displayName = name;
            e.slot = slot;
            e.rarity = rarity;
            e.hasSecondary = secondary.HasValue;
            e.secondaryStat = secondary ?? StatType.Attack;
            e.secondaryKind = kind;
            e.secondaryValue = value;
            EditorUtility.SetDirty(e);
            return e;
        }
    }
}
