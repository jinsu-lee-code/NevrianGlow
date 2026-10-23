using System.Collections.Generic;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>P-4 과일 진열대 (레벨배치 설정안 2절, 10/20 개정): 한 대 = 1층 · 한 종류 · 30개, 여러 대에서 재고 비례로 팔림.</summary>
    public class FruitShelfTests
    {
        private static ShelfState FruitShelf() => new ShelfState(1, 30);

        [Test]
        public void OneKind_Thirty()
        {
            var shelf = FruitShelf();
            for (int i = 0; i < 30; i++)
                Assert.IsTrue(shelf.TryPlace(0, "crop_moonroot"));
            Assert.AreEqual(PlaceCheck.Full, shelf.CanPlace(0, "crop_moonroot"), "한 대에 30개");
            shelf.TakeOne(0);
            Assert.AreEqual(PlaceCheck.OtherItem, shelf.CanPlace(0, "crop_mushroomflower"), "한 대에 한 종류");
            for (int i = 0; i < 29; i++)
                shelf.TakeOne(0);
            Assert.AreEqual(PlaceCheck.Ok, shelf.CanPlace(0, "crop_mushroomflower"), "비우면 다른 종류");
        }

        [Test]
        public void ThreeShelves_SellInProportion()
        {
            // 시작 매장 = 과일 진열대 3대: 월광 20 · 버섯꽃 10 · 빈 대 → 손님은 재고 비례로 2 : 1
            var a = FruitShelf(); var b = FruitShelf(); var c = FruitShelf();
            for (int i = 0; i < 20; i++) a.TryPlace(0, "m");
            for (int i = 0; i < 10; i++) b.TryPlace(0, "f");
            var shelves = new List<ShelfState> { a, b, c };
            var random = new SeededRandom(5);
            int fromA = 0, n = 3000;
            for (int k = 0; k < n; k++)
                if (SalesRules.TryPickUnit(shelves, random, out int shelf, out _) && shelf == 0)
                    fromA++;
            Assert.AreEqual(2.0 / 3.0, (double)fromA / n, 0.03);
        }
    }
}
