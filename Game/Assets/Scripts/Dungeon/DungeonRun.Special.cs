using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Combat;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Enemy;
using HistoryHeroes.Records;
using HistoryHeroes.UI;
using UnityEngine;

namespace HistoryHeroes.Dungeon
{
    /// <summary>
    /// 특수 방 (확장 1차 3장, 개발자: "맵도 너무 단조로운거 같은데"): 갈림길 2번 · 도전 방 · 저주받은 엘리트 · 기억의 석비.
    /// 계획은 판 시작 때 SpecialRooms.Plan (갈림길 난수), 여기서는 그 계획대로 방을 돌린다.
    /// </summary>
    public sealed partial class DungeonRun
    {
        [System.Serializable]
        private sealed class SpecialArt
        {
            public Sprite normal, survive, noHit, elite, cursed, fork, stele, pass;
            [Tooltip("Blessings.All 순서")]
            public Sprite[] blessings = new Sprite[0];
            public GameObject chestModel, steleModel;
        }

        [Header("확장 1차 3장 특수 방")]
        [SerializeField] private ForkGate[] forkGates = new ForkGate[0];
        [SerializeField] private SpecialArt specialArt = new SpecialArt();

        private static readonly Color ChallengeColor = new Color(1f, 0.75f, 0.25f, 0.95f);
        private static readonly Color CursedColor = new Color(0.72f, 0.38f, 1f, 0.95f);
        private static readonly Color SteleColor = new Color(0.4f, 0.85f, 1f, 0.95f);
        private static readonly Color ForkColor = new Color(1f, 0.9f, 0.45f, 0.95f);

        private SpecialPlan _special;
        private SpecialPlan.Fork _openFork;
        private readonly List<string> _blessings = new List<string>();
        private readonly object _blessingOwner = new object();
        /// <summary>"20초 버티기" 동안: 적을 다 잡아도 방이 끝나지 않는다</summary>
        private bool _holdClear;
        private bool _challengeFailed;
        private int _challengeFailedRoom = -1;
        private EnemyBase _cursedElite;
        private string _roomTitle = string.Empty;
        /// <summary>방을 깬 뒤 맨 나중에 보여 줄 알림 (도전 보상)</summary>
        private string _pendingNotice;

        /// <summary>지금 방의 특수 종류 (갈림길에서 고른 쪽 · 석비)</summary>
        public RoomSpecial CurrentSpecial => _special != null && !_rift ? _special.SpecialOf(_room) : RoomSpecial.None;
        public SpecialPlan Special => _special;
        public IReadOnlyList<string> ActiveBlessings => _blessings;
        public bool ChallengeFailed => _challengeFailed;

        // ---------------------------------------------------------------- 판 시작

        private void PlanSpecials()
        {
            bool eligible = SpecialRooms.Eligible(_plans, _daily, _rift, GameSession.Tutorial, GameSession.CustomPlan != null);
            _special = eligible ? SpecialRooms.Plan(_plans, _chapter, RunRandom.Forks) : null;
        }

        private void WireSpecials()
        {
            for (int i = 0; forkGates != null && i < forkGates.Length; i++)
            {
                if (forkGates[i] == null) continue;
                int index = i;
                forkGates[i].Entered += () => OnForkEntered(index);
            }

            player.Health.Damaged += (_, dealt) =>
            {
                if (dealt > 0f) OnChallengeHit();
            };
        }

        // ---------------------------------------------------------------- 이어하기

        private void CaptureSpecial(RunSnapshot s, int room)
        {
            s.forkChoices = _special != null ? _special.Choices() : new List<int>();
            s.blessings = new List<string>(_blessings);
            s.challengeFailed = room == _challengeFailedRoom;
        }

        /// <summary>체력을 되돌리기 전에 부른다 (축복이 최대체력을 바꾸므로)</summary>
        private void RestoreSpecial(RunSnapshot s, int room)
        {
            _special?.RestoreChoices(s.forkChoices, room);
            if (s.blessings != null)
            {
                foreach (string id in s.blessings) ApplyBlessing(id);
            }

            _challengeFailedRoom = s.challengeFailed ? room : -1;
        }

        // ---------------------------------------------------------------- 방 들어갈 때

        private void BeginSpecialRoom(int room)
        {
            HideForks();
            _holdClear = false;
            _cursedElite = null;
            _challengeFailed = room == _challengeFailedRoom;
        }

        /// <summary>방 이름 뒤 꼬리표 (없으면 null → 원래 꼬리표)</summary>
        private string SpecialTag()
        {
            RoomSpecial special = CurrentSpecial;
            return special == RoomSpecial.None ? null : "  " + SpecialRooms.Title(special);
        }

        /// <summary>방 진행 바: 갈림길 칸 = 두 갈래 표시 (고른 뒤 그 종류), 지나온 석비 칸 = 석비</summary>
        private void MarkSpecialCells(int first)
        {
            if (_special == null || _rift || hud == null) return;
            foreach (SpecialPlan.Fork fork in _special.Forks)
            {
                int index = fork.Room - 1 - first;
                bool past = fork.Room < _room;
                if (fork.Choice < 0) hud.MarkRoom(index, specialArt.fork, ForkColor);
                else if (fork.Choice == 1) hud.MarkRoom(index, IconOf(fork.Alternative), Dim(ColorOf(fork.Alternative), past));
            }

            foreach (int room in _special.Steles.Keys)
            {
                if (room <= _room) hud.MarkRoom(room - 1 - first, specialArt.stele, Dim(SteleColor, room < _room));
            }
        }

        private static Color Dim(Color c, bool past) => past ? new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 0.9f) : c;

        private Sprite IconOf(RoomSpecial special)
        {
            switch (special)
            {
                case RoomSpecial.ChallengeSurvive: return specialArt.survive;
                case RoomSpecial.ChallengeNoHit: return specialArt.noHit;
                case RoomSpecial.Cursed: return specialArt.cursed;
                case RoomSpecial.Stele: return specialArt.stele;
                default: return specialArt.normal;
            }
        }

        private static Color ColorOf(RoomSpecial special) =>
            special == RoomSpecial.Cursed ? CursedColor : special == RoomSpecial.Stele ? SteleColor : ChallengeColor;

        /// <summary>도전 방이면 여기서 돌린다 (true = 맡았다)</summary>
        private bool StartChallenge(RoomPlan plan)
        {
            switch (CurrentSpecial)
            {
                case RoomSpecial.ChallengeSurvive:
                    hud.ShowNotice(_challengeFailed ? "이미 실패한 도전이에요 — 보상 없이 진행" : "도전! 20초 동안 버티세요 (다 잡지 않아도 돼요)", 2.5f);
                    StartCoroutine(RunSurvive());
                    return true;
                case RoomSpecial.ChallengeNoHit:
                    hud.ShowNotice(_challengeFailed ? "이미 실패한 도전이에요 — 보상 없이 진행" : "도전! 한 번도 맞지 말고 깨세요 (함정 포함)", 2.5f);
                    StartCoroutine(SpawnRoom(plan, _special.NoHitEncounter));
                    return true;
                default:
                    return false;
            }
        }

        // ---------------------------------------------------------------- 갈림길

        /// <summary>갈림길이면 문 2개를 연다 (true). 가운데 문은 닫힌 채로</summary>
        private bool TryOpenFork()
        {
            if (_special == null || _rift || forkGates == null || forkGates.Length < 2 || forkGates[0] == null || forkGates[1] == null) return false;
            SpecialPlan.Fork fork = _special.ForkAfter(_room);
            if (fork == null) return false;

            _openFork = fork;
            bool elite = fork.Alternative == RoomSpecial.Cursed;
            if (elite) forkGates[0].Show("엘리트\n<size=70%>평소대로</size>", specialArt.elite, new Color(1f, 0.82f, 0.35f));
            else forkGates[0].Show("평범한 방\n<size=70%>평소대로</size>", specialArt.normal, new Color(0.8f, 0.86f, 0.95f));
            forkGates[1].Show(ForkText(fork.Alternative), IconOf(fork.Alternative), ColorOf(fork.Alternative));
            hud.ShowNotice("갈림길 — 들어갈 문을 고르세요", 2.5f);
            return true;
        }

        private static string ForkText(RoomSpecial special)
        {
            switch (special)
            {
                case RoomSpecial.ChallengeSurvive: return "도전 방\n<size=70%>20초 버티기 · 보상 +1</size>";
                case RoomSpecial.ChallengeNoHit: return "도전 방\n<size=70%>맞지 않고 깨기 · 보상 +1</size>";
                case RoomSpecial.Cursed: return "저주받은 엘리트\n<size=70%>속성 2개 · 희귀 카드</size>";
                default: return SpecialRooms.Title(special);
            }
        }

        private void OnForkEntered(int index)
        {
            if (_openFork == null || _finished) return;
            _openFork.Choice = index == 1 ? 1 : 0;
            _openFork = null;
            HideForks();
            StartCoroutine(GoToNextRoom());
        }

        private void HideForks()
        {
            _openFork = null;
            if (forkGates == null) return;
            foreach (ForkGate gate in forkGates)
            {
                if (gate != null) gate.Hide();
            }
        }

        /// <summary>QA·벤치: 갈림길 문 고르기 (0 왼쪽 · 1 오른쪽). 열린 갈림길이 없으면 false</summary>
        public bool DevChooseFork(int index)
        {
            if (_openFork == null) return false;
            OnForkEntered(index);
            return true;
        }

        public bool ForkOpen => _openFork != null;

        // ---------------------------------------------------------------- 도전 방

        /// <summary>"20초 버티기": 0·7·14초에 무리, 20초가 되면 남은 적은 연기로 사라진다 (경험치·처치 없음). 셋째 무리까지 다 잡으면 바로 성공</summary>
        private IEnumerator RunSurvive()
        {
            _holdClear = true;
            _alive = 0;
            float start = Time.time;
            int shown = -1;
            List<List<SpawnEntry>> waves = _special.SurviveWaves;
            for (int w = 0; w < waves.Count; w++)
            {
                float at = start + SpecialRooms.WaveTimes[Mathf.Min(w, SpecialRooms.WaveTimes.Length - 1)];
                while (Time.time < at)
                {
                    if (_finished) yield break;
                    shown = ShowSurviveTime(start, shown);
                    yield return null;
                }

                if (w > 0) hud.ShowNotice($"무리 {w + 1}번째!", 1.2f);
                foreach (SpawnEntry entry in waves[w])
                {
                    if (entry.prefab != null) _alive += entry.count;
                }

                foreach (SpawnEntry entry in waves[w])
                {
                    if (entry.prefab == null) continue;
                    for (int i = 0; i < entry.count; i++)
                    {
                        StartCoroutine(SpawnOne(entry, PickSpawnPoint()));
                        yield return new WaitForSeconds(spawnStagger);
                    }
                }
            }

            while (Time.time < start + SpecialRooms.SurviveSeconds && _alive > 0)
            {
                if (_finished) yield break;
                shown = ShowSurviveTime(start, shown);
                yield return null;
            }

            if (_finished) yield break;
            _holdClear = false;
            VanishEnemies();
            _alive = 0;
            hud.RoomText = _roomTitle;
            if (!_challengeFailed) ChallengeSucceeded();
            OnRoomCleared();
        }

        private int ShowSurviveTime(float start, int shown)
        {
            int left = Mathf.CeilToInt(SpecialRooms.SurviveSeconds - (Time.time - start));
            if (left == shown) return shown;
            hud.RoomText = $"{_roomTitle}  ·  {Mathf.Max(0, left)}초";
            return left;
        }

        /// <summary>남은 적을 연기로 없앤다 (Killed를 부르지 않는다 → 경험치·처치 수 없음)</summary>
        private void VanishEnemies()
        {
            var alive = new List<EnemyBase>(EnemyBase.Alive);
            foreach (EnemyBase e in alive)
            {
                if (e == null || !e.gameObject.activeSelf) continue;
                FxBursts.Puff(e.transform.position, new Color(0.8f, 0.8f, 0.85f, 0.85f), 1.4f);
                e.gameObject.SetActive(false);
            }

            foreach (SpriteRenderer m in _markers) m.gameObject.SetActive(false);
        }

        private void OnChallengeHit()
        {
            if (_finished || _challengeFailed || _alive <= 0 || CurrentSpecial != RoomSpecial.ChallengeNoHit) return;
            _challengeFailed = true;
            _challengeFailedRoom = _room;
            hud.ShowNotice("도전 실패 — 맞았어요. 방은 그대로 깨면 돼요", 2.5f);
            RunSnapshot run = SaveSystem.Data.run;
            if (run != null && run.valid && run.room == _room)
            {
                run.challengeFailed = true; // 껐다 켜서 다시 도전하지 못하게
                SaveSystem.Save();
            }
        }

        /// <summary>방을 깰 때: "맞지 않고 깨기"를 해냈으면 보상</summary>
        private void ResolveChallengeOnClear()
        {
            if (CurrentSpecial == RoomSpecial.ChallengeNoHit && !_challengeFailed) ChallengeSucceeded();
        }

        /// <summary>도전 성공: 레벨업 카드 1장 더 + 골드 (보상 방의 2배). 한 방에 한 번 (이어하기로 다시 받지 못한다)</summary>
        private void ChallengeSucceeded()
        {
            if (_resumeRewardTaken && _room == _resumeRoom) return;
            int gold = SpecialRooms.ChallengeGold(GameSession.Chapter);
            Wallet.Grant(SaveSystem.Data, new Reward { Gold = gold });
            SaveSystem.MarkDirty();
            experience.GrantLevelUp();
            HypeFx.Banner("도전 성공!", new Color(1f, 0.82f, 0.3f));
            _pendingNotice = $"도전 보상: 레벨업 1번 + 골드 {gold}"; // 방 깰 때 알림(기록 용량)보다 뒤에 보이게
            MarkRewardTaken();
        }

        // ---------------------------------------------------------------- 저주받은 엘리트

        /// <summary>저주받은 엘리트 방이면 미리 뽑아 둔 속성 2개 (true)</summary>
        private bool TryCursedAffixes(EnemyBase enemy, out EliteAffix affixes)
        {
            affixes = EliteAffix.None;
            if (CurrentSpecial != RoomSpecial.Cursed) return false;
            affixes = _special.CursedAffixes;
            if (_cursedElite == null) _cursedElite = enemy;
            return true;
        }

        /// <summary>잡으면: 확정 레벨업(엘리트 공통) 뒤에 희귀 이상 2장 이상 보장 카드 1번 더 (전설 확률은 평소대로)</summary>
        private void OnCursedKilled(EnemyBase enemy)
        {
            if (enemy != _cursedElite) return;
            _cursedElite = null;
            if (_resumeRewardTaken && _room == _resumeRoom) return;
            skills.MinRareNext = 2;
            experience.GrantLevelUp();
            HypeFx.Banner("저주를 이겼다!", CursedColor);
            hud.ShowNotice("저주 보상: 희귀 카드 보장 1번 더", 2.5f);
            MarkRewardTaken();
        }

        // ---------------------------------------------------------------- 기억의 석비

        private void OpenStele()
        {
            _alive = 0;
            PlacePedestalModel(specialArt.steleModel, 1.1f);
            if (rewardRoom == null || !_special.Steles.TryGetValue(_room, out string[] offer))
            {
                OnRoomCleared();
                return;
            }

            var texts = new[]
            {
                Blessings.Text(offer[0]), Blessings.Text(offer[1]),
                $"그냥 지나가기\n<size=70%>체력 {Mathf.RoundToInt(SpecialRooms.PassHeal * 100f)}% 회복</size>",
            };
            var icons = new[] { BlessingIcon(offer[0]), BlessingIcon(offer[1]), specialArt.pass };
            var colors = new[] { new Color(0.25f, 0.62f, 0.85f), new Color(0.42f, 0.4f, 0.9f), new Color(0.42f, 0.47f, 0.52f) };
            rewardRoom.OpenChoice("기억의 석비 — 축복과 대가", texts, icons, index =>
            {
                if (index < 2) ChooseBlessing(offer[index]);
                else
                {
                    player.Health.Heal(player.Health.Max * SpecialRooms.PassHeal);
                    hud.ShowNotice($"체력 회복 +{Mathf.RoundToInt(SpecialRooms.PassHeal * 100f)}%");
                }

                MarkRewardTaken();
                if (!_finished) OnRoomCleared();
            }, colors);
        }

        private Sprite BlessingIcon(string id)
        {
            int i = Blessings.IndexOf(id);
            return specialArt.blessings != null && i >= 0 && i < specialArt.blessings.Length ? specialArt.blessings[i] : null;
        }

        private void ChooseBlessing(string id)
        {
            ApplyBlessing(id);
            RunSnapshot run = SaveSystem.Data.run;
            if (run != null && run.valid && run.room == _room) run.blessings.Add(id); // MarkRewardTaken이 같이 저장한다
            if (id == "wisdom") experience.GrantLevelUp();
            int i = Blessings.IndexOf(id);
            if (i >= 0) HypeFx.Banner($"{Blessings.All[i].Name}의 축복", SteleColor);
        }

        /// <summary>축복 능력치 (최대체력이 바뀌어도 지금 체력은 같은 비율로 — 검토 9장 8번)</summary>
        private void ApplyBlessing(string id)
        {
            if (Blessings.IndexOf(id) < 0) return;
            float ratio = player.Health.Current / Mathf.Max(1f, player.Health.Max);
            Blessings.Apply(id, player.Stats, _blessingOwner);
            _blessings.Add(id);
            if (!player.Health.IsDead) player.Health.Revive(player.Health.Max * ratio);
        }

        // ---------------------------------------------------------------- 받침대 모델

        /// <summary>보상 방 가운데 받침대(첫 장애물)를 보상 상자 · 석비 모델로 바꾼다 (판정은 그대로)</summary>
        private void PlacePedestalModel(GameObject prefab, float footprint)
        {
            RoomLayout layout = CurrentLayout;
            if (prefab == null || layout == null || layout.use != RoomUse.Reward || layout.obstacles.Count == 0 || _layoutObjects.Count == 0) return;
            GameObject pedestal = _layoutObjects[0];
            if (pedestal == null) return;
            foreach (Transform child in pedestal.transform) child.gameObject.SetActive(false); // 돌기둥 모양 숨김

            GameObject model = Instantiate(prefab, layoutRoot);
            model.transform.position = layout.obstacles[0].position;
            var bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }

            float size = any ? Mathf.Max(bounds.size.x, bounds.size.y) : 1f;
            if (size > 0.01f) model.transform.localScale *= footprint / size;
            StageLook.Retint(model);
            _layoutObjects.Add(model);
        }
    }
}
