using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NevrianGlowFarm.Logic.Data
{
    /// <summary>
    /// CSV 한 장 (첫 줄 = 열 이름). 기획 CSV(작물 · 상점 · 스킬)를 읽는다. 엔진 의존 없음.
    /// 따옴표 안의 쉼표 · 줄바꿈 · 두 번 쓴 따옴표("")를 지원하고, UTF-8 BOM과 빈 줄은 무시한다.
    /// 열 이름은 앞뒤 공백을 지우고 대소문자를 가리지 않는다.
    /// </summary>
    public sealed class CsvTable
    {
        private readonly List<string> headers;
        private readonly List<string[]> rows;

        private CsvTable(List<string> headers, List<string[]> rows)
        {
            this.headers = headers;
            this.rows = rows;
        }

        public IReadOnlyList<string> Headers => headers;
        public int RowCount => rows.Count;

        /// <summary>열 번호. 없으면 -1.</summary>
        public int ColumnIndex(string header)
        {
            for (int i = 0; i < headers.Count; i++)
                if (string.Equals(headers[i], header?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        /// <summary>칸 값 (앞뒤 공백 제거). 짧은 줄의 없는 칸은 빈 문자열.</summary>
        public string Get(int row, int column)
        {
            var cells = rows[row];
            return column >= 0 && column < cells.Length ? cells[column].Trim() : string.Empty;
        }

        public string Get(int row, string header) => Get(row, ColumnIndex(header));

        /// <summary>CSV 글을 읽는다. 첫 줄이 비어 있거나 따옴표가 닫히지 않으면 FormatException.</summary>
        public static CsvTable Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > 0 && text[0] == '﻿')
                text = text.Substring(1);

            var records = ReadRecords(text);
            records.RemoveAll(r => r.Length == 1 && r[0].Trim().Length == 0);
            if (records.Count == 0)
                throw new FormatException("CSV에 열 이름 줄이 없음");

            var headers = new List<string>();
            foreach (var h in records[0])
                headers.Add(h.Trim());
            records.RemoveAt(0);
            return new CsvTable(headers, records);
        }

        private static List<string[]> ReadRecords(string text)
        {
            var records = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': fields.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        fields.Add(field.ToString()); field.Clear();
                        records.Add(fields.ToArray()); fields.Clear();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (quoted)
                throw new FormatException("CSV 따옴표가 닫히지 않음");
            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                records.Add(fields.ToArray());
            }
            return records;
        }
    }

    /// <summary>CSV 칸 글자 → 값. 숫자는 문화권과 무관하게 점(.)을 소수점으로 읽는다.</summary>
    public static class CsvValue
    {
        public static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>true/false · 1/0 · O/X · 예/아니오.</summary>
        public static bool TryBool(string text, out bool value)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true": case "1": case "o": case "y": case "yes": case "예":
                    value = true; return true;
                case "false": case "0": case "x": case "n": case "no": case "아니오":
                    value = false; return true;
                default:
                    value = false; return false;
            }
        }

        /// <summary>#RRGGBB 또는 #RRGGBBAA (# 생략 가능) → 0~1 rgba.</summary>
        public static bool TryColor(string text, out float r, out float g, out float b, out float a)
        {
            r = g = b = 0f; a = 1f;
            string hex = (text ?? string.Empty).Trim().TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8)
                return false;
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
                return false;
            if (hex.Length == 6)
                v = (v << 8) | 0xFF;
            r = ((v >> 24) & 0xFF) / 255f;
            g = ((v >> 16) & 0xFF) / 255f;
            b = ((v >> 8) & 0xFF) / 255f;
            a = (v & 0xFF) / 255f;
            return true;
        }
    }
}
