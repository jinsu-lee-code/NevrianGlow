#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Data;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 기획 CSV ↔ 콘텐츠 에셋 (노션 GDD 12.5, 0-6 결정: 열 이름 = 에셋 필드 이름).
    /// 가져오기는 멱등: 같은 id의 에셋은 값만 갱신, 없는 id는 새로 만들고, CSV에 없는 에셋은 건드리지 않는다.
    /// 전체를 먼저 검사해 한 줄이라도 틀리면 아무것도 바꾸지 않는다.
    /// 다른 에셋 참조 칸(예: 씨앗의 crop)은 상대 id를 적는다. 내보내기로 기획에 보낼 양식을 만든다.
    /// </summary>
    public static class CsvContentImporter
    {
        private const string CropsFolder = ContentDatabaseBuilder.DataRoot + "/Crops";
        private const string IdColumn = "id";

        public sealed class Report
        {
            public int Created, Updated, Unchanged;
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> IgnoredColumns = new List<string>();
            public bool Applied => Errors.Count == 0;

            public override string ToString()
            {
                var sb = new StringBuilder(Applied
                    ? $"생성 {Created} · 갱신 {Updated} · 변화 없음 {Unchanged}"
                    : $"적용 안 함 — 오류 {Errors.Count}건");
                if (IgnoredColumns.Count > 0)
                    sb.Append($" · 모르는 열 무시: {string.Join(", ", IgnoredColumns)}");
                foreach (var e in Errors)
                    sb.Append("\n  ").Append(e);
                return sb.ToString();
            }
        }

        [MenuItem("Glow Farm/Data/Import Crops CSV...")]
        private static void ImportCropsMenu()
        {
            string path = EditorUtility.OpenFilePanel("작물 CSV 가져오기", "", "csv");
            if (string.IsNullOrEmpty(path))
                return;
            var report = Import<CropData>(File.ReadAllText(path, Encoding.UTF8), CropsFolder);
            if (report.Applied) Debug.Log($"[CSV] {Path.GetFileName(path)}: {report}");
            else Debug.LogError($"[CSV] {Path.GetFileName(path)}: {report}");
        }

        [MenuItem("Glow Farm/Data/Export Crops CSV...")]
        private static void ExportCropsMenu()
        {
            string path = EditorUtility.SaveFilePanel("작물 CSV 내보내기", "", "crops.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;
            File.WriteAllText(path, Export<CropData>(), new UTF8Encoding(true));
            Debug.Log($"[CSV] 내보냄: {path}");
        }

        // ── 가져오기 ────────────────────────────────────────

        /// <summary>CSV 글을 T 에셋들에 적용한다. 새 에셋은 folder/{id}.asset.</summary>
        public static Report Import<T>(string csvText, string folder) where T : ContentData
        {
            var report = new Report();
            CsvTable table;
            try { table = CsvTable.Parse(csvText); }
            catch (FormatException e) { report.Errors.Add(e.Message); return report; }

            int idColumn = table.ColumnIndex(IdColumn);
            if (idColumn < 0)
            {
                report.Errors.Add($"'{IdColumn}' 열이 없음");
                return report;
            }

            // 열 → 필드 대응표: 임시 인스턴스로 필드 종류를 알아낸다
            var probe = ScriptableObject.CreateInstance<T>();
            var probeSo = new SerializedObject(probe);
            var columns = new List<(int Index, string Field)>();
            foreach (var header in table.Headers)
            {
                if (string.Equals(header, IdColumn, StringComparison.OrdinalIgnoreCase))
                    continue;
                var prop = FindTopLevel(probeSo, header);
                if (prop == null || !IsSupported(prop))
                    report.IgnoredColumns.Add(header);
                else
                    columns.Add((table.ColumnIndex(header), prop.name));
            }

            var db = ContentDatabaseBuilder.LoadOrCreate();
            var seen = new HashSet<string>();
            var plans = new List<(string Id, string Path, Dictionary<string, string> Values)>();

            // 1) 검사만
            for (int row = 0; row < table.RowCount; row++)
            {
                string line = $"{row + 2}번째 줄";
                string id = table.Get(row, idColumn);
                if (!ContentChecks.IsValidId(id)) { report.Errors.Add($"{line}: id '{id}'가 비었거나 소문자 · 숫자 · 밑줄 외 글자가 있음"); continue; }
                if (!seen.Add(id)) { report.Errors.Add($"{line}: id '{id}'가 CSV 안에서 겹침"); continue; }

                string path = $"{folder}/{id}.asset";
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is T)) { report.Errors.Add($"{line}: {path}에 다른 종류의 에셋이 있음"); continue; }
                if (db.TryGet(id, out ContentData other) && AssetDatabase.GetAssetPath(other) != path)
                { report.Errors.Add($"{line}: id '{id}'가 이미 {AssetDatabase.GetAssetPath(other)}에 있음"); continue; }

                var values = new Dictionary<string, string>();
                foreach (var (index, field) in columns)
                {
                    string text = table.Get(row, index);
                    string error = Validate(probeSo.FindProperty(field), typeof(T), text, db);
                    if (error != null) report.Errors.Add($"{line} {field}: {error}");
                    else values[field] = text;
                }
                plans.Add((id, path, values));
            }
            UnityEngine.Object.DestroyImmediate(probe);
            if (!report.Applied)
                return report;

            // 2) 적용
            foreach (var (id, path, values) in plans)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                bool created = asset == null;
                if (created)
                    asset = ScriptableObject.CreateInstance<T>();

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = id;
                foreach (var pair in values)
                    Apply(so.FindProperty(pair.Key), pair.Value, db);
                bool changed = so.ApplyModifiedPropertiesWithoutUndo();

                // 새 에셋은 값을 다 채운 뒤 만든다 — 만드는 순간 ContentDatabase 자동 검사가 빈 id 오류를 내지 않게.
                if (created)
                {
                    EnsureFolder(folder);
                    AssetDatabase.CreateAsset(asset, path);
                    report.Created++;
                }
                else if (changed)
                {
                    report.Updated++;
                    EditorUtility.SetDirty(asset);
                }
                else report.Unchanged++;
            }
            AssetDatabase.SaveAssets();
            ContentDatabaseBuilder.Refresh();
            return report;
        }

        private static SerializedProperty FindTopLevel(SerializedObject so, string name)
        {
            var it = so.GetIterator();
            if (!it.NextVisible(true))
                return null;
            do
            {
                if (it.name != "m_Script" && string.Equals(it.name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return so.FindProperty(it.name);
            } while (it.NextVisible(false));
            return null;
        }

        private static bool IsSupported(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Integer => true,
            SerializedPropertyType.Float => true,
            SerializedPropertyType.String => true,
            SerializedPropertyType.Boolean => true,
            SerializedPropertyType.Color => true,
            SerializedPropertyType.ObjectReference => true,
            _ => false,
        };

        /// <summary>칸 글자가 필드에 맞는지. 맞으면 null, 틀리면 이유. [Min] · [Range] 범위도 본다.</summary>
        private static string Validate(SerializedProperty p, Type owner, string text, ContentDatabase db)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!CsvValue.TryInt(text, out int i)) return $"정수가 아님 '{text}'";
                    return CheckRange(owner, p.name, i);
                case SerializedPropertyType.Float:
                    if (!CsvValue.TryFloat(text, out float f)) return $"숫자가 아님 '{text}' (소수점은 .)";
                    return CheckRange(owner, p.name, f);
                case SerializedPropertyType.Boolean:
                    return CsvValue.TryBool(text, out _) ? null : $"참/거짓이 아님 '{text}' (true/false, O/X)";
                case SerializedPropertyType.Color:
                    return CsvValue.TryColor(text, out _, out _, out _, out _) ? null : $"색이 아님 '{text}' (#RRGGBB)";
                case SerializedPropertyType.ObjectReference:
                    if (text.Length == 0) return null;
                    return db.TryGet(text, out ContentData _) ? null : $"id '{text}'인 콘텐츠가 없음";
                default:
                    return null;
            }
        }

        private static string CheckRange(Type owner, string fieldName, float value)
        {
            var field = FindField(owner, fieldName);
            if (field == null) return null;
            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null && (value < range.min || value > range.max))
                return $"{value}이(가) 범위 {range.min}~{range.max} 밖";
            var min = field.GetCustomAttribute<MinAttribute>();
            if (min != null && value < min.min)
                return $"{value}이(가) 최소 {min.min}보다 작음";
            return null;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(ScriptableObject); t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static void Apply(SerializedProperty p, string text, ContentDatabase db)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: CsvValue.TryInt(text, out int i); p.intValue = i; break;
                case SerializedPropertyType.Float: CsvValue.TryFloat(text, out float f); p.floatValue = f; break;
                case SerializedPropertyType.String: p.stringValue = text; break;
                case SerializedPropertyType.Boolean: CsvValue.TryBool(text, out bool b); p.boolValue = b; break;
                case SerializedPropertyType.Color:
                    CsvValue.TryColor(text, out float r, out float g, out float bl, out float a);
                    p.colorValue = new Color(r, g, bl, a);
                    break;
                case SerializedPropertyType.ObjectReference:
                    p.objectReferenceValue = text.Length == 0 ? null : db.Get<ContentData>(text);
                    break;
            }
        }

        // ── 내보내기 ────────────────────────────────────────

        /// <summary>T 에셋 전체를 CSV 글로. 열 = id + 지원하는 필드 전부 (목록 · 프리팹 칸은 제외).</summary>
        public static string Export<T>() where T : ContentData
        {
            var db = ContentDatabaseBuilder.LoadOrCreate();
            ContentDatabaseBuilder.Refresh();
            var assets = db.All<T>().ToList();

            var probe = ScriptableObject.CreateInstance<T>();
            var fields = new List<string>();
            var it = new SerializedObject(probe).GetIterator();
            if (it.NextVisible(true))
            {
                do
                {
                    if (it.name == "m_Script" || it.name == IdColumn || !IsSupported(it))
                        continue;
                    if (it.propertyType == SerializedPropertyType.ObjectReference && !IsContentReference(typeof(T), it.name))
                        continue;
                    fields.Add(it.name);
                } while (it.NextVisible(false));
            }
            UnityEngine.Object.DestroyImmediate(probe);

            var sb = new StringBuilder();
            sb.Append(IdColumn);
            foreach (var f in fields) sb.Append(',').Append(f);
            sb.Append('\n');
            foreach (var asset in assets)
            {
                var so = new SerializedObject(asset);
                sb.Append(Escape(asset.Id));
                foreach (var f in fields)
                    sb.Append(',').Append(Escape(Format(so.FindProperty(f))));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static bool IsContentReference(Type owner, string fieldName)
        {
            var field = FindField(owner, fieldName);
            return field != null && typeof(ContentData).IsAssignableFrom(field.FieldType);
        }

        private static string Format(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Integer => p.intValue.ToString(CultureInfo.InvariantCulture),
            SerializedPropertyType.Float => p.floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            SerializedPropertyType.String => p.stringValue,
            SerializedPropertyType.Boolean => p.boolValue ? "true" : "false",
            SerializedPropertyType.Color => "#" + (p.colorValue.a < 1f ? ColorUtility.ToHtmlStringRGBA(p.colorValue) : ColorUtility.ToHtmlStringRGB(p.colorValue)),
            SerializedPropertyType.ObjectReference => p.objectReferenceValue is ContentData c ? c.Id : string.Empty,
            _ => string.Empty,
        };

        private static string Escape(string s)
        {
            s ??= string.Empty;
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
