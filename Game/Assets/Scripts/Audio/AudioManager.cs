using System.Collections;
using System.Collections.Generic;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Audio
{
    /// <summary>
    /// 소리 (Audio 1·6장): 음악은 바뀔 때 0.6초 동안 겹쳐 바뀐다. 효과음은 소리 칸 16개를 돌려 쓰고, **같은 소리는 동시에 4개까지**.
    /// 음량은 설정의 배경음 · 효과음 (0~100%). 장면이 바뀌어도 남는다. 소리 표가 없으면 조용히 아무것도 안 한다.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int Voices = 16;
        private const int SameClipLimit = 4;
        private const float FadeSeconds = 0.6f;

        private static AudioManager _instance;
        private SoundBank _bank;
        private AudioSource _musicA, _musicB;
        private bool _aPlaying = true;
        private string _currentMusic;
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly Dictionary<AudioClip, float> _lastPlayed = new Dictionary<AudioClip, float>();
        private int _next;

        private static AudioManager Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("[Audio]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AudioManager>();
                    _instance.Init();
                }

                return _instance;
            }
        }

        private void Init()
        {
            _bank = Resources.Load<SoundBank>("Audio/SoundBank");
            _musicA = NewSource(true);
            _musicB = NewSource(true);
            for (int i = 0; i < Voices; i++) _voices.Add(NewSource(false));
            Combat.Health.AnyDamaged += OnAnyDamaged;
        }

        private static void OnAnyDamaged(Combat.Health target, Combat.DamageInfo info, float dealt)
        {
            if (dealt <= 0f || target == null) return;
            if (target.Team == Combat.Team.Player)
            {
                Play(target.IsDead ? "death" : "player_hurt", target.IsDead ? 1f : 0.8f);
                Haptics.Light();
            }
            else
            {
                Play("hit", 0.45f, 0.12f);
            }
        }

        /// <summary>모드·구간에 맞는 음악 (Audio 2장: 구간 1 · 2 · 3~4 · 5, 보스전, 서바이벌)</summary>
        public static string MusicFor(Common.BattleMode mode, int chapter)
        {
            switch (mode)
            {
                case Common.BattleMode.Boss: return "boss";
                case Common.BattleMode.Survival: return "survival";
                case Common.BattleMode.Rift: return "chapter3";
                default:
                    if (chapter >= 5) return "chapter5";
                    if (chapter >= 3) return "chapter3";
                    return chapter == 2 ? "chapter2" : "chapter1";
            }
        }

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            return s;
        }

        private static float Step(int v) => Mathf.Clamp(v, 0, 4) * 0.25f;
        private static GameSettings Settings => SaveSystem.Data.settings;
        public static float MusicVolume => Step(Settings.musicVolume);
        public static float SfxVolume => Step(Settings.sfxVolume);

        /// <summary>음악 (같은 곡이면 그대로). 이름: lobby · opening · chapter1~5 · boss · survival</summary>
        public static void PlayMusic(string id)
        {
            AudioManager a = Instance;
            if (a == null || a._bank == null || a._currentMusic == id) return;
            SoundEntry e = a._bank.Music(id);
            if (e == null || e.clip == null) return;
            a._currentMusic = id;
            a.StopAllCoroutines();
            a.StartCoroutine(a.CrossFade(e));
        }

        private IEnumerator CrossFade(SoundEntry e)
        {
            AudioSource from = _aPlaying ? _musicA : _musicB;
            AudioSource to = _aPlaying ? _musicB : _musicA;
            _aPlaying = !_aPlaying;
            to.clip = e.clip;
            to.volume = 0f;
            to.Play();
            float startFrom = from.volume;
            for (float t = 0f; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / FadeSeconds;
                to.volume = e.volume * MusicVolume * k;
                from.volume = startFrom * (1f - k);
                yield return null;
            }

            to.volume = e.volume * MusicVolume;
            from.Stop();
        }

        /// <summary>설정에서 배경음을 바꿨을 때</summary>
        public static void RefreshVolume()
        {
            AudioManager a = _instance;
            if (a == null || a._bank == null || a._currentMusic == null) return;
            SoundEntry e = a._bank.Music(a._currentMusic);
            AudioSource now = a._aPlaying ? a._musicA : a._musicB;
            if (e != null) now.volume = e.volume * MusicVolume;
        }

        /// <summary>효과음 (이름은 SoundBank). volume은 이 소리 기본값에 곱한다. 같은 소리가 4개 넘게 겹치면 버린다</summary>
        public static void Play(string id, float volume = 1f, float pitchJitter = 0.06f)
        {
            AudioManager a = Instance;
            if (a == null || a._bank == null || SfxVolume <= 0f) return;
            SoundEntry e = a._bank.Sfx(id);
            if (e == null || e.clip == null) return;

            int playing = 0;
            foreach (AudioSource v in a._voices)
            {
                if (v.isPlaying && v.clip == e.clip) playing++;
            }

            if (playing >= SameClipLimit) return;
            // 같은 소리가 한 프레임에 여럿 겹치면 하나만 (터지는 순간 귀가 아프지 않게)
            if (a._lastPlayed.TryGetValue(e.clip, out float last) && Time.unscaledTime - last < 0.03f) return;
            a._lastPlayed[e.clip] = Time.unscaledTime;

            AudioSource voice = a.FreeVoice();
            voice.clip = e.clip;
            voice.volume = e.volume * volume * SfxVolume;
            voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            voice.Play();
        }

        private AudioSource FreeVoice()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                AudioSource v = _voices[(_next + i) % _voices.Count];
                if (!v.isPlaying)
                {
                    _next = (_next + i + 1) % _voices.Count;
                    return v;
                }
            }

            AudioSource oldest = _voices[_next]; // 다 쓰는 중이면 가장 오래된 칸
            _next = (_next + 1) % _voices.Count;
            return oldest;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }

    /// <summary>진동 (Audio 4장): 피격 짧게 약하게 · 전설/조합 짧게 두 번 · 보스 격파 길게. 설정에서 끈다 (기본 켜짐)</summary>
    public static class Haptics
    {
        public static void Light() => Vibrate(new long[] { 0, 25 });
        public static void Double() => Vibrate(new long[] { 0, 40, 70, 40 });
        public static void Long() => Vibrate(new long[] { 0, 220 });

        private static void Vibrate(long[] pattern)
        {
            if (!SaveSystem.Data.settings.vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null) return;
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (AndroidJavaObject e = effect.CallStatic<AndroidJavaObject>("createWaveform", pattern, -1))
                            {
                                vibrator.Call("vibrate", e);
                            }
                        }
                        else
                        {
                            vibrator.Call("vibrate", pattern, -1);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] " + e.Message);
            }
#endif
        }
    }
}
