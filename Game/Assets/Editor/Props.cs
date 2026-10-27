using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HistoryHeroes.EditorTools
{
    /// <summary>
    /// private [SerializeField] 값을 에디터에서 넣기 위한 도우미. using 블록이 끝나면 한 번에 적용한다.
    /// 예: using (var p = new Props(door)) p.Ref("sprite", renderer);
    /// </summary>
    internal sealed class Props : IDisposable
    {
        private readonly SerializedObject _so;

        public Props(Object target) => _so = new SerializedObject(target);

        private SerializedProperty Find(string name)
        {
            SerializedProperty p = _so.FindProperty(name);
            if (p == null) throw new ArgumentException($"{_so.targetObject.GetType().Name}에 '{name}' 필드가 없습니다.");
            return p;
        }

        public Props Ref(string name, Object value) { Find(name).objectReferenceValue = value; return this; }
        public Props Float(string name, float value) { Find(name).floatValue = value; return this; }
        public Props Int(string name, int value) { Find(name).intValue = value; return this; }
        public Props Bool(string name, bool value) { Find(name).boolValue = value; return this; }
        public Props String(string name, string value) { Find(name).stringValue = value; return this; }
        public Props Color(string name, Color value) { Find(name).colorValue = value; return this; }
        public Props Vector2(string name, Vector2 value) { Find(name).vector2Value = value; return this; }
        public Props Vector3(string name, Vector3 value) { Find(name).vector3Value = value; return this; }
        public Props Mask(string name, int layerMask) { Find(name).intValue = layerMask; return this; }

        public Props Refs<T>(string name, IList<T> values) where T : Object
        {
            SerializedProperty p = Find(name);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            return this;
        }

        public void Dispose() => _so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static class AssetUtil
    {
        public static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>있으면 불러오고 없으면 만든다. 다시 실행해도 GUID가 바뀌지 않아 참조가 깨지지 않는다.</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                EnsureFolder(Path.GetDirectoryName(path));
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        public static T SavePrefab<T>(GameObject go, string path) where T : Component
        {
            EnsureFolder(Path.GetDirectoryName(path));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }
    }
}
