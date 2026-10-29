using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Logic.Text;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>5-4c 툴팁 · 목록 둘째 줄 글자 규칙.</summary>
    public class UiTextTests
    {
        // 레벨업 해금 줄: 같은 끝말은 한 번만 (2026-10-29)
        [Test]
        public void JoinSharedTail_FactorsCommonLastWord()
        {
            Assert.AreEqual("괭이 · 대걸레 · 물뿌리개 업그레이드", UiText.JoinSharedTail(new[] { "괭이 업그레이드", "대걸레 업그레이드", "물뿌리개 업그레이드" }));
            Assert.AreEqual("버섯꽃 · 등불 열매 씨앗", UiText.JoinSharedTail(new[] { "버섯꽃 씨앗", "등불 열매 씨앗" }));
            Assert.AreEqual("가방 확장 · 비료", UiText.JoinSharedTail(new[] { "가방 확장", "비료" }), "끝말이 다르면 그대로");
            Assert.AreEqual("비료", UiText.JoinSharedTail(new[] { "비료" }));
            Assert.AreEqual(string.Empty, UiText.JoinSharedTail(new string[0]));
        }

        [Test]
        public void Crop_OneShot_NoRegrowLine()
        {
            // 월광 뿌리: 1일 · 물 필요 · 1개 · 재수확 없음
            string text = UiText.Crop(new CropSpec(1, 1, 1, 0, 1, true), 20);
            Assert.AreEqual("성장 1일 · 물 필요 · 수확 1개 · 판매가 20달러", text);
        }

        [Test]
        public void Crop_Regrow_AndNoWater()
        {
            // 등불 열매: 6일 · 2개 · 3일마다 총 5회 / 버섯꽃: 물 없이
            Assert.AreEqual("성장 6일 · 물 필요 · 수확 2개 · 3일마다 다시 수확 (총 5회) · 판매가 1,200달러",
                UiText.Crop(new CropSpec(6, 1, 2, 3, 5, true), 1200));
            Assert.AreEqual("성장 2일 · 물 없이 자람 · 수확 2개", UiText.Crop(new CropSpec(2, 1, 2, 0, 1, false), 0), "판매가 0이면 생략");
        }

        [Test]
        public void Offer_StatusLines()
        {
            Assert.AreEqual("오늘 10개 남음", UiText.Offer(OfferStatus.Available, 10, null, false));
            Assert.AreEqual(string.Empty, UiText.Offer(OfferStatus.Available, -1, null, false), "무제한이면 빈 줄");
            Assert.AreEqual("잠김 · 스킬 '버섯꽃 해금' 필요", UiText.Offer(OfferStatus.Locked, 0, "스킬 '버섯꽃 해금' 필요", false));
            Assert.AreEqual("잠김", UiText.Offer(OfferStatus.Locked, 5, string.Empty, false), "잠기면 입고 수를 적지 않음");
            Assert.AreEqual("내일부터 입고", UiText.Offer(OfferStatus.SoldOut, 0, null, true));
            Assert.AreEqual("오늘 품절", UiText.Offer(OfferStatus.SoldOut, 0, null, false));
            Assert.AreEqual("최대 단계", UiText.Offer(OfferStatus.MaxedOut, -1, null, false));
            Assert.AreEqual("오늘 3개 남음 · 달러 부족", UiText.Offer(OfferStatus.NotEnoughMoney, 3, null, false));
            Assert.AreEqual("달러 부족", UiText.Offer(OfferStatus.NotEnoughMoney, -1, null, false));
        }

        [Test]
        public void Join_SkipsEmpty_AndOwned()
        {
            Assert.AreEqual("가격 10달러 · 보유 3개", UiText.Join("가격 10달러", string.Empty, null, UiText.Owned(3)));
            Assert.AreEqual(string.Empty, UiText.Owned(0));
            Assert.AreEqual("2/5", UiText.SkillLevel(2, 5));
        }

        [Test]
        public void ForScreenFont_ReplacesDashesTheFontLacks()
        {
            Assert.AreEqual("성장일 -1 (작물당 1회) - 3 → 2", UiText.ForScreenFont("성장일 −1 (작물당 1회) — 3 → 2"), "→ · × 는 글꼴에 있어 그대로");
            Assert.AreEqual("a-b", UiText.ForScreenFont("a–b"));
            Assert.IsNull(UiText.ForScreenFont(null));
        }

        [Test]
        public void NoEmDash_InAnyLine()
        {
            // 게임 화면 글꼴에 "—"가 없다 (TASKS 트러블슈팅)
            foreach (var s in new[]
            {
                UiText.Crop(new CropSpec(6, 1, 2, 3, 5, true), 70),
                UiText.Offer(OfferStatus.Locked, 0, "3일차부터 필요", false),
                UiText.Offer(OfferStatus.NotEnoughMoney, 2, null, false),
            })
                Assert.IsFalse(s.Contains("—"), s);
        }
    }
}
