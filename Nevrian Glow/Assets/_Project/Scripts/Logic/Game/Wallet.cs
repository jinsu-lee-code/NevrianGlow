using System;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 달러(실물 구매)와 SP(영구 해금) (GDD 4-6). 둘 다 0 아래로 내려가지 않는다. 엔진 의존 없음.
    /// </summary>
    public sealed class Wallet
    {
        public int Money { get; private set; }
        public int SkillPoints { get; private set; }

        public Wallet(int money, int skillPoints)
        {
            if (money < 0) throw new ArgumentOutOfRangeException(nameof(money), money, "0 이상");
            if (skillPoints < 0) throw new ArgumentOutOfRangeException(nameof(skillPoints), skillPoints, "0 이상");
            Money = money;
            SkillPoints = skillPoints;
        }

        /// <summary>달러를 더하거나(양수) 쓴다(음수). 모자라면 false, 바뀌지 않음. 0이면 false.</summary>
        public bool TryChangeMoney(int delta)
        {
            if (!CanApply(Money, delta))
                return false;
            Money += delta;
            return true;
        }

        /// <summary>SP를 더하거나(양수) 쓴다(음수). 모자라면 false, 바뀌지 않음. 0이면 false.</summary>
        public bool TryChangeSkillPoints(int delta)
        {
            if (!CanApply(SkillPoints, delta))
                return false;
            SkillPoints += delta;
            return true;
        }

        // 0은 "바뀐 것 없음"이라 알릴 일도 없다. 넘침은 데모 범위에서 생기지 않지만 막아 둔다.
        private static bool CanApply(int current, int delta)
        {
            if (delta == 0)
                return false;
            long next = (long)current + delta;
            return next >= 0 && next <= int.MaxValue;
        }
    }
}
