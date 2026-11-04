using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class HeroSkillArtTests
    {
        [Test]
        public void SharedPulseHeroesHaveDifferentCachedGlyphsWithoutChangingTheirAsset()
        {
            var skills = new ActiveSkillData[3];
            var glyphs = new Sprite[3];
            string[] heroes = { "seondeok", "wuzetian", "curie" };
            try
            {
                for (int i = 0; i < skills.Length; i++)
                {
                    skills[i] = ScriptableObject.CreateInstance<ActiveSkillData>();
                    skills[i].heroOnlyId = heroes[i];
                    glyphs[i] = HeroSkillArt.IconFor(skills[i]);
                    Assert.IsNotNull(glyphs[i]);
                    Assert.AreSame(glyphs[i], HeroSkillArt.IconFor(skills[i]), "카드를 그릴 때마다 그림을 다시 만들지 않는다");
                    Assert.IsNull(skills[i].icon, "런타임 표시는 원본 에셋을 고치지 않는다");
                    Color32[] pixels = glyphs[i].texture.GetPixels32();
                    Assert.IsTrue(System.Array.Exists(pixels, p => p.a == 0));
                    Assert.IsTrue(System.Array.Exists(pixels, p => p.a > 200));
                    for (int j = 0; j < i; j++)
                        CollectionAssert.AreNotEqual(glyphs[j].texture.GetPixels32(), pixels, "이름뿐 아니라 문양이 달라야 한다");
                }
            }
            finally
            {
                foreach (ActiveSkillData skill in skills) if (skill != null) Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void CommonSkillsKeepTheirOriginalIcon()
        {
            var skill = ScriptableObject.CreateInstance<ActiveSkillData>();
            var texture = new Texture2D(2, 2);
            var icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            try
            {
                skill.icon = icon;
                Assert.AreSame(icon, HeroSkillArt.IconFor(skill));
                Assert.IsNull(HeroSkillArt.IconFor(null));
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
