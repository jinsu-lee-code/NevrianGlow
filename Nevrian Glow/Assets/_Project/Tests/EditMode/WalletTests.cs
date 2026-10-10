using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class WalletTests
    {
        [Test]
        public void AddAndSpendMoney()
        {
            var wallet = new Wallet(150, 0);

            Assert.IsTrue(wallet.TryChangeMoney(30));
            Assert.IsTrue(wallet.TryChangeMoney(-180));
            Assert.AreEqual(0, wallet.Money);
        }

        [Test]
        public void SpendingMoreThanOwned_FailsAndKeepsValue()
        {
            var wallet = new Wallet(20, 3);

            Assert.IsFalse(wallet.TryChangeMoney(-21));
            Assert.AreEqual(20, wallet.Money);
            Assert.IsFalse(wallet.TryChangeSkillPoints(-4));
            Assert.AreEqual(3, wallet.SkillPoints);
        }

        [Test]
        public void ZeroChange_IsNotAChange()
        {
            var wallet = new Wallet(10, 1);

            Assert.IsFalse(wallet.TryChangeMoney(0));
            Assert.IsFalse(wallet.TryChangeSkillPoints(0));
        }

        [Test]
        public void SkillPoints_AreSeparateFromMoney()
        {
            var wallet = new Wallet(100, 0);

            Assert.IsTrue(wallet.TryChangeSkillPoints(3));
            Assert.AreEqual(3, wallet.SkillPoints);
            Assert.AreEqual(100, wallet.Money);
        }

        [Test]
        public void NegativeStart_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Wallet(-1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Wallet(0, -1));
        }
    }
}
