using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// v1.0 법·정책 글: 저장소의 Docs/Legal · Docs/Credits.md를 게임 안 Resources/Legal로 복사한다 (웹 페이지와 같은 글, 한 곳만 고치면 된다).
    /// 마크다운 제목·굵게·목록·표를 TMP 글자로 바꾼다.
    /// </summary>
    public static partial class SceneBuilder
    {
        private const string LegalDir = "Assets/Resources/Legal";

        private static void CopyLegalTexts()
        {
            string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs"));
            Directory.CreateDirectory(LegalDir);
            Copy(Path.Combine(docs, "Legal", "terms-ko.md"), "terms");
            Copy(Path.Combine(docs, "Legal", "privacy-ko.md"), "privacy");
            Copy(Path.Combine(docs, "Credits.md"), "credits");
            AssetDatabase.Refresh();

            void Copy(string from, string name)
            {
                if (!File.Exists(from))
                {
                    Debug.LogWarning("[SceneBuilder] 법·정책 글이 없음: " + from);
                    return;
                }

                File.WriteAllText($"{LegalDir}/{name}.txt", MarkdownToRichText(File.ReadAllText(from, Encoding.UTF8)), new UTF8Encoding(false));
            }
        }

        /// <summary>간단한 마크다운 → TMP 글 (제목 · 굵게 · 목록 · 인용 · 표 · 링크)</summary>
        public static string MarkdownToRichText(string md)
        {
            var sb = new StringBuilder();
            foreach (string raw in md.Replace("\r", string.Empty).Split('\n'))
            {
                string line = raw.TrimEnd();
                if (Regex.IsMatch(line, @"^\|[\s\-|:]+\|$")) continue; // 표 구분 줄
                line = Regex.Replace(line, @"\*\*(.+?)\*\*", "<b>$1</b>");
                line = Regex.Replace(line, @"`([^`]+)`", "$1");
                line = Regex.Replace(line, @"\[([^\]]+)\]\(([^)]+)\)", "$1");
                if (line.StartsWith("# ")) line = $"<size=130%><b>{line.Substring(2)}</b></size>";
                else if (line.StartsWith("## ")) line = $"\n<color=#FFD966><b>{line.Substring(3)}</b></color>";
                else if (line.StartsWith("### ")) line = $"<b>{line.Substring(4)}</b>";
                else if (line.StartsWith("> ")) line = $"<size=85%><color=#A9B4C2>{line.Substring(2)}</color></size>";
                else if (line.StartsWith("- ")) line = "• " + line.Substring(2);
                else if (line.StartsWith("|")) line = "  " + string.Join("  ·  ", line.Trim('|').Split('|')).Trim();
                sb.AppendLine(line);
            }

            return sb.ToString().Trim();
        }
    }
}
