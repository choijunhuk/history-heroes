using System;
using HistoryHeroes.Relics;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    public static partial class SceneBuilder
    {
        /// <summary>유물 24개 = 세트 6 × 4. 생성·그림 읽기를 분리해 에셋을 쓰지 않는 테스트도 같은 정의를 쓴다.</summary>
        public static void ConfigureRelics(RelicTable relics, Func<string, RelicData> createRelic, Func<string, Sprite> loadIcon)
        {
            relics.relics.Clear();
            relics.setNames = new[] { "발명의 시대", "기록과 문자", "바다와 항해", "장수의 유품", "하늘과 시간", "준비의 흔적" };
            // 24개로 늘어도 별 +6%, 세트 +4%, 피해 감소 +4%라는 유물 몫을 지킨다.
            relics.perStar = 0.01f / 12f;
            relics.setBonus = 0.02f / 3f;
            relics.setDamageReduction = 0.02f / 3f;
            relics.pullShardCost = relics.pullGemCost = 100;
            relics.tenPullShardCost = relics.tenPullGemCost = 900;
            relics.newRelicPity = 20;
            relics.duplicateCosts = new[] { 1, 2 };
            relics.overflowShards = 5;
            relics.sourceShards = 20;
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
            // 6. 준비의 흔적: 새 영웅으로 던전을 직접 깬 보상. 뽑기에서도 같은 유물을 얻을 수 있다.
            relics.relics.Add(Relic("yisunsin_diary", "난중일기", 5,
                "사실: 이순신이 전쟁 중 직접 쓴 일기다. 각색: 게임 속 일기 한 권으로 표현한다", Hero, 0, "yisunsin", "이순신으로 던전 첫 클리어",
                "오늘 지킨 길을 적어, 내일의 길을 준비한다", "archive"));
            relics.relics.Add(Relic("yiseonggye_bow", "이성계의 활", 5,
                "사실: 이성계의 뛰어난 활솜씨가 기록에 남아 있다. 각색: 이 활의 모습은 게임에서 만들었다", Hero, 0, "yiseonggye", "이성계로 던전 첫 클리어",
                "활시위를 고르며 다음 한 발을 준비한다", "heaven_bow"));
            relics.relics.Add(Relic("davinci_gears", "다빈치의 톱니바퀴 도면", 5,
                "사실: 다빈치의 필사본에는 톱니바퀴와 기계 장치 그림이 남아 있다. 각색: 여러 그림을 한 장으로 묶었다", Hero, 0, "davinci", "다빈치로 던전 첫 클리어",
                "작은 톱니 하나도 제자리가 있어야 기계가 움직인다", "dav_tank"));
            relics.relics.Add(Relic("zhuge_memorial", "전출사표 사본", 5,
                "사실: 제갈량의 전출사표는 후대에도 베끼고 새겨 전해졌다. 각색: 게임 속 사본이며 제갈량의 친필은 아니다", Hero, 0, "zhuge", "제갈량으로 던전 첫 클리어",
                "떠나기 전 남긴 뜻이 망각 속에서도 길을 잡아 준다", "codex"));

            RelicData Relic(string id, string name, int set, string fact, RelicSource source,
                int chapter, string value, string howToGet, string story = "", string iconKey = "relic")
            {
                RelicData relic = createRelic(id);
                relic.id = id;
                relic.displayName = name;
                relic.set = set;
                relic.fact = fact;
                relic.story = story;
                relic.icon = loadIcon(iconKey);
                relic.source = source;
                relic.sourceChapter = chapter;
                relic.sourceValue = value;
                relic.howToGet = source == RelicSource.HeroFirstClear ? "유물 뽑기 또는 " + howToGet : "유물 뽑기";
                return relic;
            }
        }
    }
}
