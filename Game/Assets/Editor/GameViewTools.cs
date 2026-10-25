using System;
using System.Reflection;
using UnityEditor;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// Game 창을 폰 화면 크기로 맞춘다 (세로 1080 × 1920, 또는 세로로 긴 폰 1080 × 2340).
    /// 유니티가 이 기능을 공개 API로 열어두지 않아서 내부 API를 리플렉션으로 부른다. 실패하면 메시지만 돌려준다.
    /// </summary>
    public static class GameViewTools
    {
        [MenuItem("History Heroes/Game 창 세로 1080x1920")]
        public static string SetPortrait() => SetSize(1080, 1920);

        /// <summary>갤럭시 S24 울트라 같은 19.5:9 폰.</summary>
        [MenuItem("History Heroes/Game 창 세로 1080x2340 (긴 폰)")]
        public static string SetTallPhone() => SetSize(1080, 2340);

        public static string SetSize(int width, int height)
        {
            string sizeName = $"Portrait {width}x{height}";
            try
            {
                Assembly editor = typeof(Editor).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance").GetValue(null);
                object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
                Type groupType = group.GetType();

                int index = FindSize(group, groupType, sizeName);
                if (index < 0)
                {
                    Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                    Type kindType = editor.GetType("UnityEditor.GameViewSizeType");
                    ConstructorInfo ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                    object size = ctor.Invoke(new[] { Enum.Parse(kindType, "FixedResolution"), (object)width, height, sizeName });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = FindSize(group, groupType, sizeName);
                }

                Type gameViewType = editor.GetType("UnityEditor.GameView");
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", flags);
                if (selected != null) selected.SetValue(gameView, index);
                else gameViewType.GetMethod("SizeSelectionCallback", flags).Invoke(gameView, new object[] { index, null });

                gameView.Repaint();
                return $"Game 창 크기: {sizeName} (index {index})";
            }
            catch (Exception e)
            {
                return "Game 창 크기를 바꾸지 못했습니다: " + e.Message;
            }
        }

        private static int FindSize(object group, Type groupType, string sizeName)
        {
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < count; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                var baseText = (string)size.GetType().GetProperty("baseText").GetValue(size);
                if (baseText == sizeName) return i;
            }

            return -1;
        }
    }
}
