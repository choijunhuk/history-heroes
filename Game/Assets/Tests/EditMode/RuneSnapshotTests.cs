using HistoryHeroes.Meta;
using HistoryHeroes.Modes;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.Runes;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class RuneSnapshotTests
    {
        [Test]
        public void SavedRunKeepsEquippedRuneAndLevelAfterAccountChanges()
        {
            var table = ScriptableObject.CreateInstance<MetaTable>();
            var runes = ScriptableObject.CreateInstance<RuneTable>();
            var fire = ScriptableObject.CreateInstance<RuneData>();
            var lightning = ScriptableObject.CreateInstance<RuneData>();
            var skill = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                table.runes = runes;
                runes.runes.Add(fire);
                runes.runes.Add(lightning);
                fire.id = "fire";
                fire.tag = SkillTag.Fire;
                lightning.id = "lightning";
                lightning.tag = SkillTag.Lightning;
                skill.tags = SkillTag.Fire;
                var save = new SaveData { unlockedChapter = 5 };
                save.runes.owned.Add(new RuneLevel { id = fire.id, level = 3 });
                save.runes.owned.Add(new RuneLevel { id = lightning.id, level = 1 });
                Assert.IsTrue(RuneService.Equip(save, runes, "khan", 0, fire.id));

                MetaSnapshot atStart = MetaStats.FromAccount(save, table, "khan");
                save.run = new RunSnapshot { valid = true, hasMeta = true, meta = atStart, heroId = "khan" };
                SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
                Assert.IsTrue(RuneService.Equip(save, runes, "khan", 0, lightning.id));
                save.runes.owned[0].level = 1;

                RuneWeightModifier resumed = RuneService.ModifierFor(loaded.run.meta, runes, "khan", null);
                Assert.AreEqual(2.5f, resumed.GetMultiplier(skill, new SkillLoadout()), 0.0001f);
                Assert.AreEqual(1f, RuneService.ModifierFor(save, runes, "khan", null).GetMultiplier(skill, new SkillLoadout()), 0.0001f);
                Assert.IsTrue(loaded.run.meta.hasRunes);
                Assert.AreEqual("fire", loaded.run.meta.runes[0].id);
                Assert.AreEqual(3, loaded.run.meta.runes[0].level);
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(lightning);
                Object.DestroyImmediate(fire);
                Object.DestroyImmediate(runes);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void EmptyRuneSnapshotDoesNotAcquireLaterRune()
        {
            var table = ScriptableObject.CreateInstance<MetaTable>();
            var runes = ScriptableObject.CreateInstance<RuneTable>();
            var fire = ScriptableObject.CreateInstance<RuneData>();
            try
            {
                table.runes = runes;
                fire.id = "fire";
                runes.runes.Add(fire);
                var save = new SaveData { unlockedChapter = 5 };
                MetaSnapshot atStart = MetaStats.FromAccount(save, table, "khan");
                Assert.IsTrue(atStart.hasRunes);
                Assert.IsEmpty(atStart.runes);

                save.runes.owned.Add(new RuneLevel { id = "fire", level = 3 });
                Assert.IsTrue(RuneService.Equip(save, runes, "khan", 0, "fire"));
                Assert.IsNull(RuneService.ModifierFor(atStart, runes, "khan", null));
            }
            finally
            {
                Object.DestroyImmediate(fire);
                Object.DestroyImmediate(runes);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void RiftCheckpointUsesStoredMetaAfterAccountChanges()
        {
            GameSession.PrepareDungeon(1);
            var save = new SaveData();
            var old = new MetaSnapshot { heroLevel = 8, hasRunes = true };
            old.runes.Add(new RuneLevel { id = "fire", level = 2 });
            save.rift.checkpoint = new RiftCheckpoint { valid = true, hasMeta = true, meta = old };
            save.Hero("khan").level = 20;
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            typeof(GameSession).GetField("<Mode>k__BackingField", flags).SetValue(null, BattleMode.Rift);
            typeof(GameSession).GetField("<RiftContinue>k__BackingField", flags).SetValue(null, true);
            try
            {
                MetaSnapshot resumed = BattleBootstrap.MetaForThisBattle(null, "khan", save);
                Assert.AreSame(old, resumed);
                Assert.AreEqual(8, resumed.heroLevel);
                Assert.AreEqual("fire", resumed.runes[0].id);
            }
            finally
            {
                typeof(GameSession).GetField("<RiftContinue>k__BackingField", flags).SetValue(null, false);
                GameSession.PrepareDungeon(1);
            }
        }
    }
}
