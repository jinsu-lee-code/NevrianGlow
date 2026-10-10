using System;
using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 플레이어 조작(이동 · 시점 · 상호작용 · 핫바 · 내려놓기) 잠금. 가방 · 창고 · 상점 화면 등이 열릴 때 건다.
    /// 여러 화면이 동시에 걸 수 있다 — 건 쪽이 모두 풀어야 풀린다. 잠기면 마우스 커서가 보인다.
    /// </summary>
    public static class PlayerInputGate
    {
        private static readonly HashSet<object> owners = new HashSet<object>();

        public static bool IsBlocked => owners.Count > 0;

        /// <summary>잠김 상태가 바뀔 때만 발생 (새 상태).</summary>
        public static event Action<bool> Changed;

        public static void Block(object owner)
        {
            bool was = IsBlocked;
            if (owner != null)
                owners.Add(owner);
            if (was != IsBlocked)
                Changed?.Invoke(IsBlocked);
        }

        public static void Release(object owner)
        {
            bool was = IsBlocked;
            owners.Remove(owner);
            if (was != IsBlocked)
                Changed?.Invoke(IsBlocked);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            owners.Clear();
            Changed = null;
        }
    }
}
