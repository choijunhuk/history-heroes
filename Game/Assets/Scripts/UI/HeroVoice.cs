using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Modes;
using HistoryHeroes.Player;
using HistoryHeroes.Progress;
using HistoryHeroes.Skills;
using HistoryHeroes.Story;
using TMPro;
using UnityEngine;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 영웅 대사 말풍선 (Story 3장, 화면 아래쪽). 같은 상황 대사는 **판마다 1번** (사망·보스 격파는 매번).
    /// 보스맵은 시작할 때 보스 앞 대사 (Story 2장). 튜토리얼에서는 클리오가 말하므로 영웅 대사는 조합 완성만.
    /// 화면을 가리지 않게 2.6초만, 터치를 막지 않는다.
    /// </summary>
    public sealed class HeroVoice : MonoBehaviour
    {
        [SerializeField] private GameObject bubble;
        [SerializeField] private TMP_Text label;
        [SerializeField] private PlayerController player;
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private SkillManager skills;
        [SerializeField] private BossRun boss;

        private const float LineSeconds = 2.6f;
        private const float LowHpRatio = 0.3f;

        private readonly HashSet<LineMoment> _said = new HashSet<LineMoment>();
        private readonly Queue<string> _queue = new Queue<string>();
        private float _hideAt;
        private bool _bossDown;

        private string HeroId => player != null && player.Hero != null ? player.Hero.id : null;
        private string HeroName => player != null && player.Hero != null ? player.Hero.displayName : string.Empty;

        private void Start()
        {
            bubble.SetActive(false);
            if (experience != null) experience.LeveledUp += _ => Once(LineMoment.FirstLevelUp);
            if (skills != null)
            {
                skills.FusionCompleted += _ => Say(LineMoment.Fusion, always: true);
                skills.LegendaryChosen += () => Once(LineMoment.Legendary);
            }

            if (player != null) player.Health.Died += _ => Say(LineMoment.Death, always: true);
            StartCoroutine(Opening());
        }

        private IEnumerator Opening()
        {
            yield return new WaitForSeconds(0.7f);
            if (boss != null && boss.isActiveAndEnabled && !GameSession.Tutorial)
            {
                foreach ((string speaker, string line) in StoryLines.BossIntro(GameSession.Chapter)) Enqueue($"<color=#FFD966>{speaker}</color>  {line}");
                yield break;
            }

            Once(LineMoment.RunStart);
        }

        private void Update()
        {
            if (player != null && !player.Health.IsDead && player.Health.Current < player.Health.Max * LowHpRatio) Once(LineMoment.LowHp);
            if (boss != null && boss.isActiveAndEnabled && boss.Boss != null && boss.Boss.Health.IsDead && !_bossDown)
            {
                _bossDown = true;
                Say(LineMoment.BossDefeat, always: true);
            }

            if (bubble.activeSelf && Time.unscaledTime > _hideAt)
            {
                if (_queue.Count > 0) Show(_queue.Dequeue());
                else bubble.SetActive(false);
            }
        }

        private void Once(LineMoment moment) => Say(moment, always: false);

        private void Say(LineMoment moment, bool always)
        {
            if (GameSession.Tutorial && moment != LineMoment.Fusion) return; // 튜토리얼은 클리오가 안내
            if (!always && !_said.Add(moment)) return;
            string line = StoryLines.For(HeroId, moment);
            if (!string.IsNullOrEmpty(line)) Enqueue($"<color=#FFD966>{HeroName}</color>  {line}");
        }

        private void Enqueue(string text)
        {
            if (!bubble.activeSelf) Show(text);
            else if (_queue.Count < 2) _queue.Enqueue(text); // 밀리면 버린다 (전투를 가리지 않게)
        }

        private void Show(string text)
        {
            label.text = text;
            bubble.SetActive(true);
            _hideAt = Time.unscaledTime + LineSeconds;
        }
    }
}
