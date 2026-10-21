using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Cleaning
{
    /// <summary>청결 · 정돈 등급 (청소 v2 설정안 2절, 회신3 1-1). enum은 끝에만 추가.</summary>
    public enum TidyGrade
    {
        S,
        A,
        B,
        C,
    }

    /// <summary>가게 정리 값 (기획 game_settings.csv trash_* · stain_* · scatter_* · disorder_* · grade_* — CleaningSettingsData.ToMessSpec).</summary>
    public readonly struct MessSpec
    {
        public int TrashDay1 { get; }
        public int StainDay1 { get; }
        public int TrashDailyMin { get; }
        public int TrashDailyMax { get; }
        public double StainPerCustomer { get; }
        public double ScatterUnsoldRatio { get; }
        public double ScatterPerCustomers { get; }
        public double DisorderRatio { get; }
        public double GradeS { get; }
        public double GradeA { get; }
        public double GradeB { get; }

        public MessSpec(int trashDay1, int stainDay1, int trashDailyMin, int trashDailyMax, double stainPerCustomer,
            double scatterUnsoldRatio, double scatterPerCustomers, double disorderRatio, double gradeS, double gradeA, double gradeB)
        {
            TrashDay1 = Math.Max(0, trashDay1);
            StainDay1 = Math.Max(0, stainDay1);
            TrashDailyMin = Math.Max(0, trashDailyMin);
            TrashDailyMax = Math.Max(TrashDailyMin, trashDailyMax);
            StainPerCustomer = Math.Max(0, stainPerCustomer);
            ScatterUnsoldRatio = Math.Max(0, scatterUnsoldRatio);
            ScatterPerCustomers = Math.Max(0, scatterPerCustomers);
            DisorderRatio = Math.Max(0, disorderRatio);
            GradeS = gradeS;
            GradeA = gradeA;
            GradeB = gradeB;
        }
    }

    /// <summary>
    /// 가게 정리 규칙 (청소 v2 설정안 · 최종 기획서 7-2, TASKS P-3). 엔진 의존 없음.
    /// 어지러움은 어제 장사의 흔적으로 청소·진열 단계 시작에 생긴다: 흩어진 상품(진짜 재고) · 흐트러진 진열 · 얼룩 · 쓰레기.
    /// messRate = 청결 유지 스킬 배율 (기본 1, 흩어짐 · 흐트러짐 · 얼룩에만).
    /// </summary>
    public static class TidyRules
    {
        /// <summary>0.5 올림 (BALANCE 3-4와 같은 반올림).</summary>
        public static int Round(double x) => Math.Max(0, (int)Math.Floor(x + 0.5 + 1e-9));

        /// <summary>쓰레기: 1일차 = 대청소(30), 그 뒤 하루 최소 ~ 최대(1~3) 균등.</summary>
        public static int TrashCount(int day, MessSpec spec, IRandomSource random) =>
            day <= 1 ? spec.TrashDay1 : random.NextInt(spec.TrashDailyMin, spec.TrashDailyMax + 1);

        /// <summary>얼룩 · 발자국: 1일차 = 12, 그 뒤 어제 손님 × 0.3 × messRate.</summary>
        public static int StainCount(int day, int customersYesterday, double messRate, MessSpec spec) =>
            day <= 1 ? spec.StainDay1 : Round(Math.Max(0, customersYesterday) * spec.StainPerCustomer * Math.Max(0, messRate));

        /// <summary>흩어진 상품: (안 팔린 진열 × 0.15 + 어제 손님 ÷ 3) × messRate. 1일차는 없음.</summary>
        public static int ScatterCount(int day, int unsoldDisplayed, int customersYesterday, double messRate, MessSpec spec)
        {
            if (day <= 1)
                return 0;
            double perCustomer = spec.ScatterPerCustomers > 0 ? Math.Max(0, customersYesterday) / spec.ScatterPerCustomers : 0;
            return Round((Math.Max(0, unsoldDisplayed) * spec.ScatterUnsoldRatio + perCustomer) * Math.Max(0, messRate));
        }

        /// <summary>흐트러진 진열: 상품이 있는 진열 칸의 20% × messRate. 1일차는 없음.</summary>
        public static int DisorderCount(int day, int filledSlots, double messRate, MessSpec spec) =>
            day <= 1 ? 0 : Math.Min(Math.Max(0, filledSlots), Round(Math.Max(0, filledSlots) * spec.DisorderRatio * Math.Max(0, messRate)));

        /// <summary>
        /// 정돈도 = 1 − (바닥에 남은 상품 + 흐트러진 채 남은 칸) ÷ (처음 흩어진 상품 + 처음 흐트러진 칸), 0~1. 처음이 0이면 1.
        /// </summary>
        public static double Tidiness(int initialScattered, int initialDisordered, int floorProducts, int disorderedNow)
        {
            int total = Math.Max(0, initialScattered) + Math.Max(0, initialDisordered);
            if (total == 0)
                return 1.0;
            double value = 1.0 - (double)(Math.Max(0, floorProducts) + Math.Max(0, disorderedNow)) / total;
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        /// <summary>등급: S ≥ 0.95 · A ≥ 0.80 · B ≥ 0.60 · 그 밖 C (청결 · 정돈 같은 기준).</summary>
        public static TidyGrade Grade(double value, MessSpec spec)
        {
            const double eps = 1e-9;
            if (value + eps >= spec.GradeS) return TidyGrade.S;
            if (value + eps >= spec.GradeA) return TidyGrade.A;
            if (value + eps >= spec.GradeB) return TidyGrade.B;
            return TidyGrade.C;
        }

        /// <summary>
        /// 상품이 있는 칸 중 count개를 흐트러뜨릴 칸으로 고른다 (같은 칸 두 번 없음). 결과 = (진열대 번호, 칸 번호).
        /// </summary>
        public static List<(int Shelf, int Slot)> PickDisorderSlots(IReadOnlyList<ShelfState> shelves, int count, IRandomSource random)
        {
            var filled = new List<(int, int)>();
            for (int s = 0; s < shelves.Count; s++)
                for (int i = 0; i < shelves[s].SlotCount; i++)
                    if (!shelves[s][i].IsEmpty)
                        filled.Add((s, i));
            var picked = new List<(int Shelf, int Slot)>();
            for (int n = 0; n < count && filled.Count > 0; n++)
            {
                int k = random.NextInt(0, filled.Count);
                picked.Add(filled[k]);
                filled.RemoveAt(k);
            }
            return picked;
        }

        /// <summary>상품이 있는 진열 칸 수 (흐트러짐 대상).</summary>
        public static int FilledSlots(IReadOnlyList<ShelfState> shelves)
        {
            int n = 0;
            foreach (var shelf in shelves)
                for (int i = 0; i < shelf.SlotCount; i++)
                    if (!shelf[i].IsEmpty)
                        n++;
            return n;
        }
    }
}
