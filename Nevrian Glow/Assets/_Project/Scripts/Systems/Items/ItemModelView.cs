using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Items
{
    /// <summary>
    /// 놓인 아이템의 겉모습 (바닥 · 진열대 · 손님 바구니 공용, 5-2). 그레이박스 도형(가운데 피벗 단위 도형 — 부모 크기 = 아이템 크기)을
    /// 아이템의 놓인 모습 모델로 바꾼다: 도형은 숨기고, 모델의 가장 긴 변을 도형 크기에 맞추고, 모델 바닥을 도형 바닥에 둔다.
    /// 모델이 없으면 도형 그대로 (색은 부르는 쪽이 칠한다).
    /// </summary>
    public static class ItemModelView
    {
        private const string ChildName = "ItemModel";
        // 프리팹마다 가장 긴 변 (m) · 피벗에서 모델 바닥까지 (m, 보통 0 또는 음수) — 처음 한 번만 잰다
        private static readonly Dictionary<GameObject, Vector2> sizes = new Dictionary<GameObject, Vector2>();

        /// <summary>greybox 아래에 item의 모델을 붙인다 (이미 붙어 있으면 바꾼다). 붙였으면 true.</summary>
        public static bool Apply(Renderer greybox, ItemData item, int seed)
        {
            if (greybox == null)
                return false;
            var old = greybox.transform.Find(ChildName);
            var prefab = item != null ? item.WorldModelFor(seed) : null;
            if (old != null)
                Object.Destroy(old.gameObject);
            greybox.enabled = prefab == null;
            if (prefab == null)
                return false;

            var model = Object.Instantiate(prefab, greybox.transform, false);
            model.name = ChildName;
            var measure = Measure(prefab);
            float size = measure.x;
            // 모델 바닥(피벗이 바닥이 아닌 모델도)을 도형 바닥에 — 진열대 · 바닥에 파묻히지 않게 (2026-10-25)
            model.transform.localPosition = new Vector3(0f, -0.5f - measure.y / size, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one / size;
            // 놓인 모습은 조준 판정에 끼지 않는다 (판정은 부모 콜라이더)
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            return true;
        }

        /// <summary>프리팹의 가장 긴 변 (m, 루트 크기 1 기준) — 손에 들 때 크기 맞추기 (HeldItemView).</summary>
        public static float LongestSide(GameObject prefab) => prefab != null ? Measure(prefab).x : 1f;

        private static Vector2 Measure(GameObject prefab)
        {
            if (sizes.TryGetValue(prefab, out var measure))
                return measure;
            var probe = Object.Instantiate(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            bool any = false;
            var bounds = new Bounds();
            foreach (var r in probe.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                    continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(probe);
            float size = any ? Mathf.Max(0.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))) : 1f;
            measure = new Vector2(size, any ? bounds.min.y : 0f);
            sizes[prefab] = measure;
            return measure;
        }

        /// <summary>위치로 정하는 모델 고르기 값 (불러와도 같은 자리면 같은 모델).</summary>
        public static int SeedOf(Vector3 position) =>
            Mathf.RoundToInt(position.x * 7.3f) * 31 + Mathf.RoundToInt(position.z * 11.7f) * 17;
    }
}
