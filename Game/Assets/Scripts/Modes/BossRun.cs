using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Enemy;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Modes
{
    /// <summary>
    /// 보스맵 한 판 (GameDesign 7-3 · 7-4, Modes.md 2-9).
    /// - 고른 기록의 빌드 그대로 싸운다. 레벨업 없음, 현재 장비를 덧붙이지 않음
    /// - 기록은 재사용형: 이겨도 져도 사라지지 않는다
    /// - 처음 깨면 다음 구간이 열린다
    /// </summary>
    public sealed class BossRun : MonoBehaviour
    {
        [SerializeField] private List<BossData> bosses = new List<BossData>();
        [SerializeField] private SkillDatabase database;

        [Header("장면")]
        [SerializeField] private PlayerController player;
        [SerializeField] private SkillManager skills;
        [SerializeField] private Transform playerStart;
        [SerializeField] private Transform bossSpawn;

        [Header("UI")]
        [SerializeField] private LevelUpScreen levelUp;
        [SerializeField] private HudView hud;
        [SerializeField] private BossHud bossHud;
        [SerializeField] private RunResultScreen result;
        [SerializeField] private float introSeconds = 1.5f;

        private BossData _info;
        private EnemyBase _boss;
        private RunRecord _record;
        private float _startTime;
        private int _hitsTaken;
        private bool _finished;

        public EnemyBase Boss => _boss;
        public BossData Info => _info;
        public int HitsTaken => _hitsTaken;
        public bool IsFinished => _finished;

        private void Start()
        {
            GamePause.ResetAll();
            DamageLog.Reset();

            _info = FindBoss(GameSession.Chapter);
            _record = SaveSystem.Data.FindRecord(GameSession.BossRecordId) ?? LatestRecord();

            // 보스전에서는 레벨업하지 않는다 (7-3)
            if (levelUp != null) levelUp.enabled = false;

            RecordValidator.Result build = RecordValidator.Validate(_record, database, skills.Loadout.ActiveSlots, skills.Loadout.PassiveSlots);
            foreach (string note in build.Notes) Debug.Log("[BossRun] " + note);
            skills.ApplyRecord(build.Skills, database);

            hud.RoomText = _info != null ? $"보스  {_info.displayName}" : "보스";
            player.Teleport(playerStart.position);
            player.Health.Died += _ => Finish(victory: false);
            player.Health.Damaged += (_, dealt) =>
            {
                if (dealt > 0f && !_finished) _hitsTaken++;
            };

            StartCoroutine(Begin());
        }

        private IEnumerator Begin()
        {
            if (bossHud != null) bossHud.ShowIntro(_info != null ? _info.displayName : "보스", _info != null ? _info.tests : string.Empty);
            yield return new WaitForSeconds(introSeconds);

            if (_info == null || _info.prefab == null)
            {
                Debug.LogError("[BossRun] 보스 데이터가 없음");
                yield break;
            }

            _boss = Instantiate(_info.prefab, bossSpawn.position, Quaternion.identity, transform);
            _boss.Spawn(player.transform, _info.hpMultiplier, _info.attackMultiplier, false);
            _boss.Killed += _ => Finish(victory: true);
            _startTime = Time.time;
            if (bossHud != null) bossHud.Bind(_boss, _info.displayName);
        }

        /// <summary>
        /// 승패는 보스(또는 플레이어)가 쓰러지는 순간 확정하고 저장한다. 결과 화면만 조금 늦게 띄운다.
        /// (보스가 죽은 뒤 남은 파편에 맞아 죽어도 패배가 되면 안 된다)
        /// </summary>
        private void Finish(bool victory)
        {
            if (_finished) return;
            _finished = true;
            player.Health.Invulnerable = true;
            float elapsed = _boss != null ? Time.time - _startTime : 0f;

            SaveData save = SaveSystem.Data;
            save.bossRuns++;
            bool firstClear = false;
            if (victory && _info != null)
            {
                firstClear = !save.defeatedBosses.Contains(_info.id);
                if (firstClear) save.defeatedBosses.Add(_info.id);
                save.unlockedChapter = Mathf.Max(save.unlockedChapter, _info.chapter + 1);
                if (_record != null && !_record.bossesDefeated.Contains(_info.id)) _record.bossesDefeated.Add(_info.id);
            }

            SaveSystem.Save();

            var summary = new BossSummary
            {
                Victory = victory,
                Seconds = elapsed,
                BossName = _info != null ? _info.displayName : "보스",
                HitsTaken = _hitsTaken,
                Star2MaxHits = _info != null ? _info.star2MaxHits : 0,
                FirstClear = firstClear,
                UnlockedChapter = firstClear ? _info.chapter + 1 : 0,
                BasicAttackShare = DamageLog.BasicShare,
                RecordLabel = _record != null ? RecordFormat.Title(_record) : "기록 없음 (기본 공격만)",
            };
            StartCoroutine(ShowResultAfter(victory ? 1f : 0.6f, summary));
        }

        private IEnumerator ShowResultAfter(float seconds, BossSummary summary)
        {
            yield return new WaitForSeconds(seconds);
            result.ShowBoss(summary);
        }

        private BossData FindBoss(int chapter)
        {
            foreach (BossData b in bosses)
            {
                if (b != null && b.chapter == chapter) return b;
            }

            return bosses.Count > 0 ? bosses[0] : null;
        }

        /// <summary>에디터에서 전투 장면을 바로 보스 모드로 열었을 때: 가장 최근 기록</summary>
        private static RunRecord LatestRecord()
        {
            List<RunRecord> records = SaveSystem.Data.records;
            return records.Count > 0 ? records[records.Count - 1] : null;
        }
    }

    /// <summary>보스전 결과 화면에 보여줄 요약</summary>
    public struct BossSummary
    {
        public bool Victory;
        public float Seconds;
        public string BossName;
        public int HitsTaken;
        public int Star2MaxHits;
        public bool FirstClear;
        public int UnlockedChapter;
        public float BasicAttackShare;
        public string RecordLabel;
    }
}
