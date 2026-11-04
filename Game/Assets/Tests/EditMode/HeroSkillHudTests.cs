using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Heroes;
using HistoryHeroes.Player;
using HistoryHeroes.Skills;
using HistoryHeroes.Stats;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class HeroSkillHudTests
    {
        [Test]
        public void SuccessfulCastReportsEffectiveCooldownOnce()
        {
            var owner = new GameObject("HeroSkillOwner");
            owner.SetActive(false);
            var effect = new GameObject("HeroSkillEffect");
            effect.SetActive(false);
            var skill = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                GamePause.ResetAll();
                var player = owner.AddComponent<PlayerController>();
                player.Stats.SetBase(StatType.CooldownReduction, 0.25f);
                skill.cooldown = 5f;
                var behaviour = effect.AddComponent<HeroSkillProbeBehaviour>();
                behaviour.Bind(player, skill, 1, 0);
                int signals = 0;
                float reported = -1f;
                behaviour.Activated += seconds => { signals++; reported = seconds; };

                typeof(ActiveSkillBehaviour).GetField("_cooldownLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(behaviour, -1f);
                behaviour.CanActivate = false;
                behaviour.Tick();
                Assert.AreEqual(0, signals, "표적이 없어 실패한 시도에는 발동 표시가 나오면 안 됩니다");

                typeof(ActiveSkillBehaviour).GetField("_cooldownLeft", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(behaviour, -1f);
                behaviour.CanActivate = true;
                behaviour.Tick();
                Assert.AreEqual(1, signals);
                Assert.AreEqual(3.75f, reported, 0.001f, "실제로 적용된 쿨다운 감소를 표시해야 합니다");
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(owner);
                GamePause.ResetAll();
            }
        }

        [Test]
        public void SelectionShowsOnlyCurrentHeroAndPrefersHisEvolution()
        {
            var common = ScriptableObject.CreateInstance<ActiveSkillData>();
            var other = ScriptableObject.CreateInstance<ActiveSkillData>();
            var own = ScriptableObject.CreateInstance<ActiveSkillData>();
            var evolved = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                other.heroOnlyId = "guan";
                own.heroOnlyId = evolved.heroOnlyId = "khan";
                var loadout = new SkillLoadout();
                loadout.AddNew(common);
                loadout.AddNew(other);
                SkillInstance first = loadout.AddNew(own);
                Assert.AreSame(first, HeroSkillHud.SelectExclusive(loadout.Actives, "khan"));
                Assert.IsNull(HeroSkillHud.SelectExclusive(loadout.Actives, "tesla"));
                SkillInstance fusion = loadout.AddNew(evolved);
                fusion.IsFusionResult = true;
                Assert.AreSame(fusion, HeroSkillHud.SelectExclusive(loadout.Actives, "khan"));
            }
            finally
            {
                Object.DestroyImmediate(evolved);
                Object.DestroyImmediate(own);
                Object.DestroyImmediate(other);
                Object.DestroyImmediate(common);
            }
        }

        [TestCase(BattleMode.Dungeon, "카드에서 고유 기술 선택")]
        [TestCase(BattleMode.Boss, "이 기록에 고유 기술 없음")]
        public void FirstRenderAndReenableKeepTheCurrentCooldown(BattleMode mode, string emptyHint)
        {
            BattleMode previousMode = GameSession.Mode;
            var modeProperty = typeof(GameSession).GetProperty("Mode", BindingFlags.Static | BindingFlags.Public);
            modeProperty.SetValue(null, mode);
            var parent = new GameObject("HeroHudParent", typeof(RectTransform));
            var source = new GameObject("FontSource", typeof(RectTransform), typeof(TextMeshProUGUI));
            var owner = new GameObject("HeroHudPlayer");
            owner.SetActive(false);
            var systems = new GameObject("HeroHudSystems");
            systems.SetActive(false);
            var hero = ScriptableObject.CreateInstance<HeroData>();
            var skill = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                hero.id = skill.heroOnlyId = "khan";
                skill.displayName = "만궁 연사";
                hero.exclusiveActive = skill;
                var player = owner.AddComponent<PlayerController>();
                player.SetHero(hero);
                var skills = systems.AddComponent<SkillManager>();
                var hud = HeroSkillHud.Create((RectTransform)parent.transform, player, skills, source.GetComponent<TMP_Text>());
                var tick = typeof(HeroSkillHud).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                tick.Invoke(hud, null);
                Assert.AreEqual("만궁 연사", hud.transform.Find("SkillName").GetComponent<TMP_Text>().text);
                Assert.AreEqual(emptyHint, hud.transform.Find("SkillState").GetComponent<TMP_Text>().text);

                skills.Loadout.AddNew(skill);
                tick.Invoke(hud, null);
                var readyAt = typeof(HeroSkillHud).GetField("_readyAt", BindingFlags.Instance | BindingFlags.NonPublic);
                var cooldown = typeof(HeroSkillHud).GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic);
                readyAt.SetValue(hud, Time.time + 4f);
                cooldown.SetValue(hud, 5f);
                hud.enabled = false;
                hud.enabled = true;
                tick.Invoke(hud, null);
                StringAssert.Contains("4초", hud.transform.Find("SkillState").GetComponent<TMP_Text>().text);
                Assert.Greater(hud.transform.Find("CooldownTrack/CooldownFill").GetComponent<Image>().fillAmount, 0f);
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(systems);
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(parent);
                modeProperty.SetValue(null, previousMode);
            }
        }
    }
}
