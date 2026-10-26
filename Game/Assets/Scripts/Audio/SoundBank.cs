using System;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Audio
{
    [Serializable]
    public sealed class SoundEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    /// <summary>
    /// 소리 표 (Audio 2·3장): 음악 · 효과음을 이름으로 찾는다. 장면 빌더가 Assets/Audio에서 만든다 (Resources/Audio/SoundBank).
    /// 출처·라이선스는 Docs/Credits.md.
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        public List<SoundEntry> music = new List<SoundEntry>();
        public List<SoundEntry> sfx = new List<SoundEntry>();

        public SoundEntry Music(string id) => music.Find(m => m != null && m.id == id);
        public SoundEntry Sfx(string id) => sfx.Find(s => s != null && s.id == id);
    }
}
