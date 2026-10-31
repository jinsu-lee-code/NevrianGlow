#if UNITY_EDITOR
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 기획 shop.csv 10/30 새 상품 (더 큰 장바구니 · 매장 홍보 · 수확물 품질 상승) 에셋 준비 — 여러 번 실행해도 같다 (있으면 능력치 연결만 맞춤).
    /// 가격 · 단계는 Import Plan CSV가 넣는다. 단계마다 바뀌는 값은 BALANCE 2-8 (설명 글: +1개 · +20% · +10%).
    /// </summary>
    public static class ShopUpgradeSetup
    {
        private const string ShopFolder = "Assets/_Project/Data/Shop";
        private const string StatsFolder = "Assets/_Project/Data/Stats";

        private static readonly (string Id, string Name, string StatId, string StatName, string StatDescription, ModifierOp Op, float PerStep)[] Upgrades =
        {
            ("shop_big_basket", "더 큰 장바구니", "stat_customer_basket", "손님 장바구니",
                "손님 한 명이 사는 개수에 더한다 (기본 개수는 손님 설정, 기획 shop.csv 10/30 더 큰 장바구니 +1개씩)", ModifierOp.Add, 1f),
            ("shop_store_promo", "매장 홍보", "stat_customer_promo", "매장 홍보",
                "하루 손님 수에 곱한다 (하루 상한 안에서, 기획 shop.csv 10/30 매장 홍보 +20%씩)", ModifierOp.Multiply, 0.2f),
            ("shop_crop_quality", "수확물 품질 상승", "stat_sell_price", null, null, ModifierOp.Multiply, 0.1f),
        };

        [MenuItem("Glow Farm/Setup/Create Shop Upgrades (10-30)")]
        private static void CreateMenu()
        {
            var notes = new List<string>();
            foreach (var u in Upgrades)
            {
                var stat = EnsureStat(u.StatId, u.StatName, u.StatDescription, notes);
                string path = $"{ShopFolder}/{u.Id}.asset";
                var product = AssetDatabase.LoadAssetAtPath<StatUpgradeProductData>(path);
                if (product == null)
                {
                    product = ScriptableObject.CreateInstance<StatUpgradeProductData>();
                    AssetDatabase.CreateAsset(product, path);
                    notes.Add($"{u.Id} 생성");
                }
                var so = new SerializedObject(product);
                so.FindProperty("id").stringValue = u.Id;
                if (string.IsNullOrEmpty(so.FindProperty("displayName").stringValue))
                    so.FindProperty("displayName").stringValue = u.Name;
                so.FindProperty("stat").objectReferenceValue = stat;
                so.FindProperty("op").enumValueIndex = (int)u.Op;
                so.FindProperty("perStep").floatValue = u.PerStep;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(product);
            }
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("Glow Farm/Data/Refresh Content Database");
            Debug.Log("[Setup] 상점 능력치 상품: " + (notes.Count > 0 ? string.Join(" · ", notes) : "모두 있음 — 능력치 연결만 맞춤") + " — 다음: Import Plan CSV");
        }

        private static StatData EnsureStat(string id, string name, string description, List<string> notes)
        {
            string path = $"{StatsFolder}/{id}.asset";
            var stat = AssetDatabase.LoadAssetAtPath<StatData>(path);
            if (stat != null || name == null)
                return stat;
            stat = ScriptableObject.CreateInstance<StatData>();
            AssetDatabase.CreateAsset(stat, path);
            var so = new SerializedObject(stat);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"{id} 생성");
            return stat;
        }
    }
}
#endif
