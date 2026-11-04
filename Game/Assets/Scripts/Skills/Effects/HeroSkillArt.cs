using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>같은 파동 판정을 쓰는 세 영웅도 카드와 전투에서는 서로 다른 문양으로 읽히게 한다.</summary>
    public static class HeroSkillArt
    {
        private static Sprite _star, _seal, _prism;

        public static Sprite IconFor(SkillData skill)
        {
            if (skill == null) return null;
            if (skill.heroOnlyId == "seondeok") return _star != null ? _star : _star = Star();
            if (skill.heroOnlyId == "wuzetian") return _seal != null ? _seal : _seal = Make("HeroSeal", new[]
            {
                new Vector2(-0.7f, -0.7f), new Vector2(-0.7f, 0.7f),
                new Vector2(-0.7f, 0.7f), new Vector2(0.7f, 0.7f),
                new Vector2(0.7f, 0.7f), new Vector2(0.7f, -0.7f),
                new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f),
                new Vector2(-0.35f, 0f), new Vector2(0.35f, 0f),
                new Vector2(0f, -0.35f), new Vector2(0f, 0.35f)
            });
            if (skill.heroOnlyId == "curie") return _prism != null ? _prism : _prism = Make("HeroPrism", new[]
            {
                new Vector2(-0.75f, -0.6f), new Vector2(-0.25f, 0.7f),
                new Vector2(-0.25f, 0.7f), new Vector2(0.25f, -0.6f),
                new Vector2(0.25f, -0.6f), new Vector2(-0.75f, -0.6f),
                new Vector2(-0.05f, 0.25f), new Vector2(0.85f, 0.5f),
                new Vector2(0.05f, 0f), new Vector2(0.9f, 0f),
                new Vector2(0.15f, -0.25f), new Vector2(0.85f, -0.5f)
            });
            return skill.icon;
        }

        private static Sprite Star()
        {
            var lines = new Vector2[10];
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 144f) * Mathf.Deg2Rad;
                float b = (90f + (i + 1) * 144f) * Mathf.Deg2Rad;
                lines[i * 2] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.82f;
                lines[i * 2 + 1] = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 0.82f;
            }
            return Make("HeroStar", lines);
        }

        private static Sprite Make(string name, Vector2[] lines)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float distance = 2f;
                    for (int i = 0; i < lines.Length; i += 2)
                    {
                        Vector2 ab = lines[i + 1] - lines[i];
                        float t = Mathf.Clamp01(Vector2.Dot(p - lines[i], ab) / ab.sqrMagnitude);
                        distance = Mathf.Min(distance, Vector2.Distance(p, lines[i] + ab * t));
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((0.065f - distance) * size * 0.5f) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
            sprite.name = name;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _star = _seal = _prism = null;
    }
}
