using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Inventory;

namespace NevrianGlowFarm.Logic.Save
{
    /// <summary>
    /// 저장 파일 글 형식 (TASKS 6-1). 한 줄 = "키 값 값 …" (공백 구분). 콘텐츠 id에는 공백이 없다(ContentChecks 규칙).
    /// 첫 줄 "ngf_save 형식버전", 마지막 줄 "end" — end가 없으면 쓰다 끊긴 파일로 보고 읽지 않는다.
    /// 모르는 키는 건너뛰고 경고(새 버전에서 쓴 키), 형식 버전이 더 높으면 읽지 않는다. 숫자는 InvariantCulture.
    /// </summary>
    public static class SaveFormat
    {
        public const string Magic = "ngf_save";
        public const int CurrentVersion = 2; // 2: 튜토리얼 단계 (5-7, 2026-10-24)
        private const string None = "-";

        public static string Write(SaveSnapshot s)
        {
            var sb = new StringBuilder();
            Line(sb, Magic, CurrentVersion);
            Line(sb, "day", s.Day);
            Line(sb, "phase", s.Phase.ToString());
            Line(sb, "money", s.Money);
            Line(sb, "sp", s.SkillPoints);
            Line(sb, "level", s.Level);
            Line(sb, "exp", s.Exp);
            foreach (var pair in Sorted(s.Skills))
                Line(sb, "skill", pair.Key, pair.Value);
            foreach (var id in s.Harvested)
                Line(sb, "harvested", id);
            foreach (var r in s.Settlements)
                Line(sb, "report", r.Day, r.HasSales ? 1 : 0, r.Customers, r.Buyers, r.EmptyHanded, r.ItemsSold, r.Revenue, r.Spent,
                    r.Exp.Exp, r.Exp.LevelsGained, r.Exp.SkillPointsGained, r.Level, SoldText(r.Sold));

            Line(sb, "plots", s.PlotCount);
            for (int i = 0; i < s.Cells.Count; i++)
            {
                var c = s.Cells[i];
                Line(sb, "cell", i, c.IsTilled ? 1 : 0, F(c.TillProgress), Id(c.CropId), c.Age, c.WetDaysLeft, c.HarvestCount, c.FertilizerBonus, c.PendingFertilizer);
            }

            Slots(sb, "hotbar", s.Hotbar);
            Slots(sb, "bag", s.Bag);
            foreach (var st in s.Storage)
                Line(sb, "storage", st.ItemId, st.Count);
            for (int k = 0; k < s.Shelves.Count; k++)
            {
                Line(sb, "shelf_size", k, s.Shelves[k].Count);
                for (int i = 0; i < s.Shelves[k].Count; i++)
                    if (!s.Shelves[k][i].IsEmpty)
                        Line(sb, "shelf", k, i, s.Shelves[k][i].ItemId, s.Shelves[k][i].Count);
            }

            foreach (var pair in Sorted(s.ShopTiers))
                Line(sb, "shop_tier", pair.Key, pair.Value);
            foreach (var pair in Sorted(s.ShopBoughtToday))
                Line(sb, "shop_today", pair.Key, pair.Value);
            foreach (var pair in Sorted(s.ShopFirstOpen))
                Line(sb, "shop_open", pair.Key, pair.Value);
            Line(sb, "shop_spent", s.ShopSpentToday);
            if (s.HasCustomerModel)
                Line(sb, "customers", D(s.CustomerRegulars), D(s.CustomerNewToday), D(s.CustomerSatisfaction)); // P-2 입소문 모델 — 그 전 파일에는 없음

            foreach (var w in s.WorldItems)
                Line(sb, "world", w.ItemId, w.Count, w.Zone, F(w.X), F(w.Y), F(w.Z), F(w.Yaw));
            if (!string.IsNullOrEmpty(s.TutorialStep))
                Line(sb, "tutorial", s.TutorialStep); // 형식 2 (5-7) — 없으면 튜토리얼 안 켜짐
            sb.Append("end\n");
            return sb.ToString();
        }

        /// <summary>읽는다. 실패하면 false + 이유. warnings = 건너뛴 줄 (모르는 키 등).</summary>
        public static bool TryRead(string text, out SaveSnapshot snapshot, out string error, List<string> warnings = null)
        {
            snapshot = null;
            error = null;
            if (string.IsNullOrEmpty(text))
            {
                error = "빈 파일";
                return false;
            }
            var lines = text.Replace("\r", string.Empty).Split('\n');
            var head = Tokens(lines[0]);
            if (head.Length < 2 || head[0] != Magic || !int.TryParse(head[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
            {
                error = "저장 파일이 아님 (첫 줄)";
                return false;
            }
            if (version > CurrentVersion)
            {
                error = $"더 새로운 형식 버전 {version} (이 게임은 {CurrentVersion}까지)";
                return false;
            }

            var s = new SaveSnapshot();
            var hotbar = new SortedDictionary<int, ItemStack>();
            var bag = new SortedDictionary<int, ItemStack>();
            int hotbarSize = 0, bagSize = 0;
            var shelfSizes = new SortedDictionary<int, int>();
            var shelfItems = new List<(int Shelf, int Slot, ItemStack Stack)>();
            var cells = new SortedDictionary<int, CellSave>();
            bool ended = false;

            for (int n = 1; n < lines.Length; n++)
            {
                var t = Tokens(lines[n]);
                if (t.Length == 0)
                    continue;
                if (ended)
                {
                    error = $"{n + 1}번째 줄: end 뒤에 내용이 있음";
                    return false;
                }
                try
                {
                    switch (t[0])
                    {
                        case "end": ended = true; break;
                        case "day": s.Day = I(t[1]); break;
                        case "phase": s.Phase = (DayPhase)Enum.Parse(typeof(DayPhase), t[1]); break;
                        case "money": s.Money = I(t[1]); break;
                        case "sp": s.SkillPoints = I(t[1]); break;
                        case "level": s.Level = I(t[1]); break;
                        case "exp": s.Exp = I(t[1]); break;
                        case "skill": s.Skills[t[1]] = I(t[2]); break;
                        case "harvested": s.Harvested.Add(t[1]); break;
                        case "report":
                            s.Settlements.Add(new SettlementReport(I(t[1]), I(t[2]) != 0, I(t[3]), I(t[4]), I(t[5]), I(t[6]), I(t[7]),
                                ParseSold(t[13]), I(t[8]), new ExpGain(I(t[9]), I(t[10]), I(t[11])), I(t[12])));
                            break;
                        case "plots": s.PlotCount = I(t[1]); break;
                        case "cell":
                            cells[I(t[1])] = new CellSave
                            {
                                IsTilled = I(t[2]) != 0,
                                TillProgress = Fl(t[3]),
                                CropId = t[4] == None ? null : t[4],
                                Age = I(t[5]),
                                WetDaysLeft = I(t[6]),
                                HarvestCount = I(t[7]),
                                FertilizerBonus = I(t[8]),
                                PendingFertilizer = t.Length > 9 ? I(t[9]) : 0, // 4-6에서 추가 — 그 전 파일에는 없음
                            };
                            break;
                        case "hotbar_size": hotbarSize = I(t[1]); break;
                        case "hotbar": hotbar[I(t[1])] = new ItemStack(t[2], I(t[3])); break;
                        case "bag_size": bagSize = I(t[1]); break;
                        case "bag": bag[I(t[1])] = new ItemStack(t[2], I(t[3])); break;
                        case "storage": s.Storage.Add(new ItemStack(t[1], I(t[2]))); break;
                        case "shelf_size": shelfSizes[I(t[1])] = I(t[2]); break;
                        case "shelf": shelfItems.Add((I(t[1]), I(t[2]), new ItemStack(t[3], I(t[4])))); break;
                        case "shop_tier": s.ShopTiers[t[1]] = I(t[2]); break;
                        case "shop_today": s.ShopBoughtToday[t[1]] = I(t[2]); break;
                        case "shop_open": s.ShopFirstOpen[t[1]] = I(t[2]); break;
                        case "shop_spent": s.ShopSpentToday = I(t[1]); break;
                        case "customers":
                            s.HasCustomerModel = true;
                            s.CustomerRegulars = Dl(t[1]);
                            s.CustomerNewToday = Dl(t[2]);
                            s.CustomerSatisfaction = Dl(t[3]);
                            break;
                        case "world":
                            s.WorldItems.Add(new WorldItemSave { ItemId = t[1], Count = I(t[2]), Zone = t[3], X = Fl(t[4]), Y = Fl(t[5]), Z = Fl(t[6]), Yaw = Fl(t[7]) });
                            break;
                        case "tutorial": s.TutorialStep = t[1]; break;
                        default:
                            warnings?.Add($"{n + 1}번째 줄: 모르는 키 '{t[0]}' - 건너뜀");
                            break;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is ArgumentException || e is OverflowException)
                {
                    error = $"{n + 1}번째 줄을 읽지 못함: {lines[n]}";
                    return false;
                }
            }
            if (!ended)
            {
                error = "파일 끝(end)이 없음 - 쓰다 끊긴 파일";
                return false;
            }

            Fill(s.Hotbar, hotbarSize, hotbar);
            Fill(s.Bag, bagSize, bag);
            int cellCount = cells.Count > 0 ? MaxKey(cells) + 1 : 0;
            for (int i = 0; i < cellCount; i++)
                s.Cells.Add(cells.TryGetValue(i, out var c) ? c : default);
            foreach (var pair in shelfSizes)
            {
                while (s.Shelves.Count <= pair.Key)
                    s.Shelves.Add(new List<ItemStack>());
                var list = s.Shelves[pair.Key];
                while (list.Count < pair.Value)
                    list.Add(ItemStack.Empty);
            }
            foreach (var (shelf, slot, stack) in shelfItems)
                if (shelf < s.Shelves.Count && slot < s.Shelves[shelf].Count)
                    s.Shelves[shelf][slot] = stack;
                else
                    warnings?.Add($"진열대 {shelf}의 {slot}번 칸이 칸 수 밖 - 건너뜀");

            snapshot = s;
            return true;
        }

        // ── 내부 ──

        private static void Slots(StringBuilder sb, string key, List<ItemStack> slots)
        {
            Line(sb, key + "_size", slots.Count);
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty)
                    Line(sb, key, i, slots[i].ItemId, slots[i].Count);
        }

        private static void Fill(List<ItemStack> into, int size, SortedDictionary<int, ItemStack> stacks)
        {
            into.Clear();
            for (int i = 0; i < size; i++)
                into.Add(stacks.TryGetValue(i, out var st) ? st : ItemStack.Empty);
        }

        private static int MaxKey<T>(SortedDictionary<int, T> d)
        {
            int max = -1;
            foreach (var k in d.Keys)
                max = Math.Max(max, k);
            return max;
        }

        private static IEnumerable<KeyValuePair<string, int>> Sorted(Dictionary<string, int> d)
        {
            var list = new List<KeyValuePair<string, int>>(d);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        private static string SoldText(IReadOnlyDictionary<string, int> sold)
        {
            if (sold == null || sold.Count == 0)
                return None;
            var parts = new List<string>();
            foreach (var pair in sold)
                parts.Add($"{pair.Key}:{pair.Value.ToString(CultureInfo.InvariantCulture)}");
            parts.Sort(string.CompareOrdinal);
            return string.Join(",", parts);
        }

        private static Dictionary<string, int> ParseSold(string text)
        {
            var sold = new Dictionary<string, int>();
            if (text == None)
                return sold;
            foreach (var part in text.Split(','))
            {
                int colon = part.LastIndexOf(':');
                if (colon <= 0)
                    throw new FormatException(part);
                sold[part.Substring(0, colon)] = I(part.Substring(colon + 1));
            }
            return sold;
        }

        private static void Line(StringBuilder sb, string key, params object[] values)
        {
            sb.Append(key);
            foreach (var v in values)
            {
                sb.Append(' ');
                sb.Append(v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? None);
            }
            sb.Append('\n');
        }

        private static string[] Tokens(string line) => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        private static string Id(string id) => string.IsNullOrEmpty(id) ? None : id;
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static int I(string s) => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static float Fl(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static string D(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static double Dl(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
