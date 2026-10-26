using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Audio;
using HistoryHeroes.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v1.0 소리: Assets/Audio의 음악·효과음으로 소리 표(Resources/Audio/SoundBank)를 만들고, 가져오기 설정을 맞춘다.
    /// 음악 = 스트리밍 + Vorbis (메모리 아낌), 효과음 = 불러올 때 풀기 (바로 나게). 모든 버튼에 누르는 소리를 붙인다.
    /// 출처·라이선스는 Docs/Credits.md.
    /// </summary>
    public static partial class SceneBuilder
    {
        private const string AudioDir = "Assets/Audio";
        private const string SoundBankPath = "Assets/Resources/Audio/SoundBank.asset";

        /// <summary>음악 이름 → 파일 (Audio 2장: 7곡 중 5구간·서바이벌은 같은 곡)</summary>
        private static readonly (string id, string file, float volume)[] MusicFiles =
        {
            ("lobby", "lobby", 0.7f), ("opening", "opening", 0.7f), ("chapter1", "chapter1", 0.6f), ("chapter2", "chapter2", 0.6f),
            ("chapter3", "chapter3", 0.6f), ("chapter5", "chapter5", 0.6f), ("survival", "chapter5", 0.6f), ("boss", "boss", 0.65f),
        };

        private static SoundBank CreateSoundBank()
        {
            Directory.CreateDirectory("Assets/Resources/Audio");
            var bank = AssetUtil.LoadOrCreate<SoundBank>(SoundBankPath);
            bank.music = new List<SoundEntry>();
            foreach ((string id, string file, float volume) in MusicFiles)
            {
                AudioClip clip = ImportClip($"{AudioDir}/Music/{file}.ogg", music: true);
                if (clip != null) bank.music.Add(new SoundEntry { id = id, clip = clip, volume = volume });
            }

            bank.sfx = new List<SoundEntry>();
            if (Directory.Exists($"{AudioDir}/Sfx"))
            {
                foreach (string path in Directory.GetFiles($"{AudioDir}/Sfx", "*.ogg"))
                {
                    string asset = path.Replace('\\', '/');
                    AudioClip clip = ImportClip(asset, music: false);
                    if (clip != null) bank.sfx.Add(new SoundEntry { id = Path.GetFileNameWithoutExtension(asset), clip = clip, volume = 1f });
                }
            }

            EditorUtility.SetDirty(bank);
            return bank;
        }

        private static AudioClip ImportClip(string path, bool music)
        {
            if (!File.Exists(path)) return null;
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.5f : 0.7f;
                importer.defaultSampleSettings = s;
                importer.forceToMono = !music;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>장면의 모든 버튼에 누르는 소리 ("뒤로" · "닫기" · "나중에"는 다른 소리)</summary>
        private static void AddUiSounds(Transform root)
        {
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                if (b.GetComponent<UiSound>() != null) continue;
                var sound = b.gameObject.AddComponent<UiSound>();
                string n = b.name;
                bool back = n == "Back" || n == "Later" || n.Contains("Close") || n == "Secondary";
                using (var props = new Props(sound)) props.String("sound", back ? "ui_back" : "ui_click");
            }
        }
    }
}
