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
        // 손 모델의 자루 표시점 (HeldItemView와 같은 이름 — 대걸레: Grip = 손잡이, Contact = 머리 밑)
        private const string ContactName = "Contact";
        private const string GripName = "Grip";
        // 표시점이 없는 손 모델은 경계가 이 배율 넘게 위로 길면 세워 만든 것으로 본다 (괭이)
        private const float LayDownRatio = 1.5f;
        // 자루가 수평에서 이 각도 넘게 서 있으면 눕힌다 (°) — 물뿌리개처럼 원래 누운 것은 그대로
        private const float LayDownAngle = 30f;
        // 프리팹마다 모델 경계 (루트 크기 1 · 회전 없음, 피벗 기준 m) — 처음 한 번만 잰다
        private static readonly Dictionary<GameObject, Bounds> sizes = new Dictionary<GameObject, Bounds>();
        // 손 모델을 바닥에 눕히는 회전 · 그 회전에서의 경계
        private static readonly Dictionary<GameObject, (Quaternion Rotation, Bounds Bounds)> laid = new Dictionary<GameObject, (Quaternion, Bounds)>();

        /// <summary>greybox 아래에 item의 모델을 붙인다 (이미 붙어 있으면 바꾼다). 붙였으면 true.</summary>
        public static bool Apply(Renderer greybox, ItemData item, int seed)
        {
            if (greybox == null)
                return false;
            var old = greybox.transform.Find(ChildName);
            // 놓인 모습 모델 → 없으면 손 모델(도구 — Q로 버리면 상자가 보이던 것, 버그 리포트 10/30 김승희)
            var prefab = item != null ? item.WorldModelFor(seed) : null;
            bool held = false;
            if (prefab == null && item != null)
            {
                prefab = item.HeldPrefab;
                held = prefab != null;
            }
            if (old != null)
                Object.Destroy(old.gameObject);
            greybox.enabled = prefab == null;
            if (prefab == null)
                return false;

            var model = Object.Instantiate(prefab, greybox.transform, false);
            model.name = ChildName;
            // 손 모델은 손에 든 자세(자루가 위)로 만들어져 바닥에 꽂힌 듯 섰음 (2026-11-02 사용자) → 자루를 바닥에 눕히고 가운데로
            var rotation = Quaternion.identity;
            Bounds placed;
            if (held)
                (rotation, placed) = Laid(prefab);
            else
                placed = Measure(prefab);
            float size = Longest(placed);
            // 모델 바닥(피벗이 바닥이 아닌 모델도)을 도형 바닥에 — 진열대 · 바닥에 파묻히지 않게 (2026-10-25)
            Vector3 offset = new Vector3(held ? -placed.center.x : 0f, -placed.min.y, held ? -placed.center.z : 0f);
            model.transform.localPosition = new Vector3(0f, -0.5f, 0f) + offset / size;
            model.transform.localRotation = rotation;
            model.transform.localScale = Vector3.one / size;
            // 놓인 모습은 조준 판정에 끼지 않는다 (판정은 부모 콜라이더)
            foreach (var c in model.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            return true;
        }

        /// <summary>프리팹의 가장 긴 변 (m, 루트 크기 1 기준) — 손에 들 때 크기 맞추기 (HeldItemView).</summary>
        public static float LongestSide(GameObject prefab) => prefab != null ? Longest(Measure(prefab)) : 1f;

        private static float Longest(Bounds b) => Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));

        private static (Quaternion Rotation, Bounds Bounds) Laid(GameObject prefab)
        {
            if (laid.TryGetValue(prefab, out var cached))
                return cached;
            var rotation = LayRotation(prefab);
            cached = (rotation, rotation == Quaternion.identity ? Measure(prefab) : MeasureNow(prefab, rotation));
            laid[prefab] = cached;
            return cached;
        }

        // 자루 방향 = Grip → Contact 표시점(대걸레 — 모델이 비스듬해 경계로는 못 잼), 없으면 경계가 위로 길 때 위(괭이).
        // 자루가 서 있으면 가장 적게 돌려 수평으로 — 머리 모양은 그대로 옆으로 눕는다
        private static Quaternion LayRotation(GameObject prefab)
        {
            var root = prefab.transform;
            Transform contact = null, grip = null;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ContactName)
                    contact = t;
                else if (t.name == GripName)
                    grip = t;
            }
            Vector3 axis;
            if (contact != null)
                axis = root.InverseTransformPoint(contact.position) - (grip != null ? root.InverseTransformPoint(grip.position) : Vector3.zero);
            else
            {
                var b = Measure(prefab);
                if (b.size.y <= LayDownRatio * Mathf.Max(b.size.x, b.size.z))
                    return Quaternion.identity;
                axis = Vector3.up;
            }
            if (axis.sqrMagnitude < 1e-6f)
                return Quaternion.identity;
            axis.Normalize();
            if (Mathf.Abs(axis.y) < Mathf.Sin(LayDownAngle * Mathf.Deg2Rad))
                return Quaternion.identity;
            Vector3 flat = Vector3.ProjectOnPlane(axis, Vector3.up);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.right;
            return Quaternion.FromToRotation(axis, flat);
        }

        private static Bounds Measure(GameObject prefab)
        {
            if (sizes.TryGetValue(prefab, out var measure))
                return measure;
            measure = MeasureNow(prefab, Quaternion.identity);
            sizes[prefab] = measure;
            return measure;
        }

        // 피벗을 원점에 두고 rotation으로 돌린 모델의 경계 (메시 기준 — 상자를 돌리는 것보다 꼭 맞음)
        private static Bounds MeasureNow(GameObject prefab, Quaternion rotation)
        {
            var probe = Object.Instantiate(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, rotation);
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
            return any ? bounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        /// <summary>위치로 정하는 모델 고르기 값 (불러와도 같은 자리면 같은 모델).</summary>
        public static int SeedOf(Vector3 position) =>
            Mathf.RoundToInt(position.x * 7.3f) * 31 + Mathf.RoundToInt(position.z * 11.7f) * 17;
    }
}
