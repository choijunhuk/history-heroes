using System;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Stats;
using UnityEditor;

namespace HistoryHeroes.EditorTools
{
    /// <summary>v0.4 판 밖 성장 데이터: 장비 42종 (Equipment 7장) + 장비 표 + 판 밖 성장 표</summary>
    public static partial class SceneBuilder
    {
        private static MetaTable CreateMetaData(HeroRoster roster)
        {
            var table = AssetUtil.LoadOrCreate<EquipmentTable>($"{DataDir}/Equipment/EquipmentTable.asset");
            ConfigureEquipment(table, id => AssetUtil.LoadOrCreate<EquipmentData>($"{DataDir}/Equipment/Equip_{id}.asset"));
            foreach (EquipmentData item in table.items) EditorUtility.SetDirty(item);
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

            // 기존 에셋에도 새 24개 목록과 예산을 적용한다. 테스트는 같은 목록을 메모리에서 만든다.
            var relics = AssetUtil.LoadOrCreate<HistoryHeroes.Relics.RelicTable>($"{DataDir}/Relics/RelicTable.asset");
            ImportUiSprites("Assets/Art/UI/Relics", false); // 유물 그림 24장 → 스프라이트
            ConfigureRelics(relics, id => AssetUtil.LoadOrCreate<HistoryHeroes.Relics.RelicData>($"{DataDir}/Relics/Relic_{id}.asset"),
                key => key.StartsWith("relic_") ? AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>($"Assets/Art/UI/Relics/{key}.png") : Icon(key));
            foreach (HistoryHeroes.Relics.RelicData relic in relics.relics) EditorUtility.SetDirty(relic);
            EditorUtility.SetDirty(relics);

            var meta = AssetUtil.LoadOrCreate<MetaTable>($"{DataDir}/Meta/MetaTable.asset");
            meta.equipment = table;
            meta.runes = runes;
            meta.relics = relics;
            meta.pets = CreatePets();
            meta.missions = CreateMissions(roster);
            EditorUtility.SetDirty(meta);

            // 상자 표 (Gacha 2장): 화면 확률과 추첨이 같이 읽는다. 숫자는 GachaTable 기본값
            var gacha = AssetUtil.LoadOrCreate<GachaTable>($"{DataDir}/Equipment/GachaTable.asset");
            gacha.equipment = table;
            EditorUtility.SetDirty(gacha);
            return meta;
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
            p.model3D = Art3DPets.Model(id, out float petHeight); // 3D 모델 (없으면 임시 도형)
            p.modelHeight = petHeight;
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

        /// <summary>42종 정의를 빌더와 테스트가 함께 쓴다. 생성 함수를 바꿔 테스트에서는 에셋에 쓰지 않는다.</summary>
        public static void ConfigureEquipment(EquipmentTable table, Func<string, EquipmentData> createEquipment)
        {
            // 기존 에셋을 다시 만들 때도 3칸 때의 비용·배분이 남지 않게 덮어쓴다.
            table.weaponBase = new[] { 0.03f, 0.06f, 0.10f, 0.15f };
            table.armorBase = new[] { 0.03f, 0.06f, 0.10f, 0.15f };
            table.accessoryBase = new[] { 0.015f, 0.03f, 0.045f, 0.06f };
            table.mainShares = new[] { 0.6f, 0.5f, 1f, 0.4f, 0.3f, 0.2f };
            table.enhanceCap = new[] { 10, 20, 30, 40 };
            table.weaponPerLevel = table.armorPerLevel = 0.0025f;
            table.accessoryPerLevel = 0.0005f;
            table.enhanceCostPerLevel = 50;
            table.items.Clear();

            // 🗡️ 무기: 공격력 % (부가: 치명타 피해 / 쿨다운 감소 / 치명타 확률)
            table.items.Add(Equip(createEquipment, "weapon_recruit_c", "수습병의 무기", EquipSlot.Weapon, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "weapon_elite_u", "정예병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CritDamage, 0.03f));
            table.items.Add(Equip(createEquipment, "weapon_scout_u", "척후병의 무기", EquipSlot.Weapon, EquipRarity.Uncommon, StatType.CooldownReduction, 0.006f));
            table.items.Add(Equip(createEquipment, "weapon_general_r", "장군의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CritDamage, 0.06f));
            table.items.Add(Equip(createEquipment, "weapon_strategist_r", "책사의 무기", EquipSlot.Weapon, EquipRarity.Rare, StatType.CooldownReduction, 0.012f));
            table.items.Add(Equip(createEquipment, "weapon_timeguard_l", "시간 수호자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritRate, 0.018f));
            table.items.Add(Equip(createEquipment, "weapon_conqueror_l", "정복자의 무기", EquipSlot.Weapon, EquipRarity.Legendary, StatType.CritDamage, 0.12f));

            // 🛡️ 갑옷: 최대체력 % (부가: 피해 감소 / 이동속도)
            table.items.Add(Equip(createEquipment, "armor_recruit_c", "수습병의 갑옷", EquipSlot.Armor, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "armor_elite_u", "정예병의 갑옷", EquipSlot.Armor, EquipRarity.Uncommon, StatType.DamageReduction, 0.005f));
            table.items.Add(Equip(createEquipment, "armor_pilgrim_u", "순례자의 외투", EquipSlot.Armor, EquipRarity.Uncommon, StatType.MoveSpeed, 0.005f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "armor_general_r", "장군의 갑옷", EquipSlot.Armor, EquipRarity.Rare, StatType.DamageReduction, 0.015f));
            table.items.Add(Equip(createEquipment, "armor_explorer_r", "탐험가의 외투", EquipSlot.Armor, EquipRarity.Rare, StatType.MoveSpeed, 0.0075f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "armor_timeguard_l", "시간 수호자의 갑옷", EquipSlot.Armor, EquipRarity.Legendary, StatType.DamageReduction, 0.03f));
            table.items.Add(Equip(createEquipment, "armor_unyielding_l", "불굴의 망토", EquipSlot.Armor, EquipRarity.Legendary, StatType.MoveSpeed, 0.01f, ModifierKind.Percent));

            // 💍 목걸이: 공격속도 % (부가: 치명타 확률 / 이동속도 / 쿨다운 감소). "부적"은 행운 장식으로 그린다
            table.items.Add(Equip(createEquipment, "acc_charm_c", "낡은 부적", EquipSlot.Necklace, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "acc_feather_u", "매의 깃털 장식", EquipSlot.Necklace, EquipRarity.Uncommon, StatType.CritRate, 0.01f));
            table.items.Add(Equip(createEquipment, "acc_compass_u", "여행자의 나침반", EquipSlot.Necklace, EquipRarity.Uncommon, StatType.MoveSpeed, 0.02f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "acc_signet_r", "황금 인장 반지", EquipSlot.Necklace, EquipRarity.Rare, StatType.CritRate, 0.015f));
            table.items.Add(Equip(createEquipment, "acc_watch_r", "학자의 회중시계", EquipSlot.Necklace, EquipRarity.Rare, StatType.CooldownReduction, 0.02f));
            table.items.Add(Equip(createEquipment, "acc_hourglass_l", "시간의 모래시계", EquipSlot.Necklace, EquipRarity.Legendary, StatType.CritRate, 0.02f));
            table.items.Add(Equip(createEquipment, "acc_stirrup_l", "바람의 등자", EquipSlot.Necklace, EquipRarity.Legendary, StatType.MoveSpeed, 0.03f, ModifierKind.Percent));

            table.items.Add(Equip(createEquipment, "gloves_recruit_c", "수습병의 장갑", EquipSlot.Gloves, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "gloves_elite_u", "정예병의 장갑", EquipSlot.Gloves, EquipRarity.Uncommon, StatType.CritDamage, 0.02f));
            table.items.Add(Equip(createEquipment, "gloves_scout_u", "척후병의 장갑", EquipSlot.Gloves, EquipRarity.Uncommon, StatType.CooldownReduction, 0.004f));
            table.items.Add(Equip(createEquipment, "gloves_general_r", "장군의 장갑", EquipSlot.Gloves, EquipRarity.Rare, StatType.CritDamage, 0.04f));
            table.items.Add(Equip(createEquipment, "gloves_strategist_r", "책사의 장갑", EquipSlot.Gloves, EquipRarity.Rare, StatType.CooldownReduction, 0.008f));
            table.items.Add(Equip(createEquipment, "gloves_timeguard_l", "시간 수호자의 장갑", EquipSlot.Gloves, EquipRarity.Legendary, StatType.CritRate, 0.012f));
            table.items.Add(Equip(createEquipment, "gloves_conqueror_l", "정복자의 장갑", EquipSlot.Gloves, EquipRarity.Legendary, StatType.CritDamage, 0.08f));

            table.items.Add(Equip(createEquipment, "belt_recruit_c", "수습병의 허리띠", EquipSlot.Belt, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "belt_elite_u", "정예병의 허리띠", EquipSlot.Belt, EquipRarity.Uncommon, StatType.DamageReduction, 0.003f));
            table.items.Add(Equip(createEquipment, "belt_pilgrim_u", "순례자의 허리띠", EquipSlot.Belt, EquipRarity.Uncommon, StatType.MoveSpeed, 0.003f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "belt_general_r", "장군의 허리띠", EquipSlot.Belt, EquipRarity.Rare, StatType.DamageReduction, 0.009f));
            table.items.Add(Equip(createEquipment, "belt_explorer_r", "탐험가의 허리띠", EquipSlot.Belt, EquipRarity.Rare, StatType.MoveSpeed, 0.0045f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "belt_timeguard_l", "시간 수호자의 허리띠", EquipSlot.Belt, EquipRarity.Legendary, StatType.DamageReduction, 0.018f));
            table.items.Add(Equip(createEquipment, "belt_unyielding_l", "불굴의 허리띠", EquipSlot.Belt, EquipRarity.Legendary, StatType.MoveSpeed, 0.006f, ModifierKind.Percent));

            table.items.Add(Equip(createEquipment, "boots_recruit_c", "수습병의 신발", EquipSlot.Boots, EquipRarity.Common));
            table.items.Add(Equip(createEquipment, "boots_elite_u", "정예병의 군화", EquipSlot.Boots, EquipRarity.Uncommon, StatType.DamageReduction, 0.002f));
            table.items.Add(Equip(createEquipment, "boots_pilgrim_u", "순례자의 신발", EquipSlot.Boots, EquipRarity.Uncommon, StatType.MoveSpeed, 0.002f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "boots_general_r", "장군의 군화", EquipSlot.Boots, EquipRarity.Rare, StatType.DamageReduction, 0.006f));
            table.items.Add(Equip(createEquipment, "boots_explorer_r", "탐험가의 신발", EquipSlot.Boots, EquipRarity.Rare, StatType.MoveSpeed, 0.003f, ModifierKind.Percent));
            table.items.Add(Equip(createEquipment, "boots_timeguard_l", "시간 수호자의 군화", EquipSlot.Boots, EquipRarity.Legendary, StatType.DamageReduction, 0.012f));
            table.items.Add(Equip(createEquipment, "boots_wind_l", "바람의 신발", EquipSlot.Boots, EquipRarity.Legendary, StatType.MoveSpeed, 0.004f, ModifierKind.Percent));
        }

        private static EquipmentData Equip(Func<string, EquipmentData> createEquipment, string id, string name, EquipSlot slot, EquipRarity rarity,
            StatType? secondary = null, float value = 0f, ModifierKind kind = ModifierKind.Flat)
        {
            EquipmentData e = createEquipment(id);
            e.id = id;
            e.displayName = name;
            e.slot = slot;
            e.rarity = rarity;
            e.hasSecondary = secondary.HasValue;
            e.secondaryStat = secondary ?? StatType.Attack;
            e.secondaryKind = kind;
            e.secondaryValue = value;
            return e;
        }
    }
}
