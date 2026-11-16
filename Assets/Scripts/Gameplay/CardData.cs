using System.Collections.Generic;
using FingerMonster.Core;
using UnityEngine;

namespace FingerMonster.Gameplay
{
    /// <summary>
    /// Stratejik yetenek kartlarının (Ability Cards) görsel, metinsel ve kural verilerini tutan ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbilityCard", menuName = "FingerMonster/Ability Card Data")]
    public class CardData : ScriptableObject
    {
        public AbilityCardType cardType = AbilityCardType.Shield;
        public string cardId = "shield";
        public string displayName = "KALKAN";
        [TextArea(2, 4)]
        public string description = "Daktiloda tuzak tuşa basarsan elinin kopmasını 1 kez engeller.";
        public string shortTag = "KORUMA";
        public Color accentColor = new Color(0.2f, 0.8f, 0.95f, 1f);
        public int defaultWeight = 15;
        public bool canUseDuringCardSelection = true;
        public bool canUseDuringTypewriterTurn = true;
        public bool requiresTargetKey = false;
        public bool requiresTargetOpponent = false;

        private static Dictionary<AbilityCardType, CardData> _cachedByType;
        private static List<CardData> _cachedList;

        /// <summary>
        /// Belirtilen kart türünün tanımını döner. Sahnede/Resources'ta asset olmasa bile
        /// çalışma zamanında varsayılan Türkçe tanımları önbelleğe alarak sıfır hatayla çalışır.
        /// </summary>
        public static CardData GetDefinition(AbilityCardType type)
        {
            EnsureCatalogInitialized();
            if (_cachedByType != null && _cachedByType.TryGetValue(type, out CardData data) && data != null)
            {
                return data;
            }
            return _cachedList != null && _cachedList.Count > 0 ? _cachedList[0] : null;
        }

        /// <summary>
        /// Oyundaki 6 stratejik yetenek kartının tüm tanımlarını sıralı olarak döner.
        /// </summary>
        public static IReadOnlyList<CardData> GetAllDefinitions()
        {
            EnsureCatalogInitialized();
            return _cachedList;
        }

        private static void EnsureCatalogInitialized()
        {
            if (_cachedList != null && _cachedByType != null && _cachedList.Count == 6 && _cachedList[0] != null)
            {
                return;
            }

            _cachedByType = new Dictionary<AbilityCardType, CardData>(6);
            _cachedList = new List<CardData>(6);

            RegisterRuntimeCard(
                AbilityCardType.Shield,
                "shield",
                "KALKAN",
                "Bu tur daktiloda tuzak tuşa basarsan elinin kopmasını 1 kez engeller (pasif olarak eldeyken de otomatik korur).",
                "KORUMA",
                new Color(0.18f, 0.78f, 0.96f, 1f),
                15,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: true,
                requiresTargetKey: false,
                requiresTargetOpponent: false
            );

            RegisterRuntimeCard(
                AbilityCardType.Possess,
                "possess",
                "ELE GEÇİR",
                "3 kart seçiminde 2 canavar kartından 1 tanesinin yerini sadece sana ifşa eder (%50 güvenli şans).",
                "İPUCU",
                new Color(0.68f, 0.36f, 0.96f, 1f),
                22,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: false,
                requiresTargetKey: false,
                requiresTargetOpponent: false
            );

            RegisterRuntimeCard(
                AbilityCardType.XRay,
                "xray",
                "RÖNTGEN",
                "Daktilodaki basılmamış 1 tuşu tarar; güvenli mi (Yeşil) tuzak mı (Kırmızı) sadece sana gösterir.",
                "TARAMA",
                new Color(0.25f, 0.92f, 0.55f, 1f),
                20,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: true,
                requiresTargetKey: true,
                requiresTargetOpponent: false
            );

            RegisterRuntimeCard(
                AbilityCardType.SkipTurn,
                "skip_turn",
                "SIRA ATLAT",
                "Kader kartı çekmeden ve tuşa basmadan sıranı doğrudan sonraki oyuncuya devreder. Üst üste 2 tur kullanılamaz.",
                "PAS GEÇ",
                new Color(0.96f, 0.76f, 0.20f, 1f),
                15,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: true,
                requiresTargetKey: false,
                requiresTargetOpponent: false
            );

            RegisterRuntimeCard(
                AbilityCardType.ReverseDirection,
                "reverse_direction",
                "TERS YÖN",
                "Tur sonuna kadar masa dönüş yönünü tersine çevirir ve sıranı yeni yöndeki oyuncuya aktarır.",
                "MANEVRA",
                new Color(0.96f, 0.48f, 0.22f, 1f),
                15,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: false,
                requiresTargetKey: false,
                requiresTargetOpponent: false
            );

            RegisterRuntimeCard(
                AbilityCardType.Fog,
                "fog",
                "SİS",
                "Seçili rakibin (Q/E) bir sonraki 3 kart seçimini yoğun sise boğar; ipucu kartlarını (Ele Geçir) etkisiz kılar.",
                "SABOTAJ",
                new Color(0.55f, 0.68f, 0.78f, 1f),
                13,
                canUseDuringCardSelection: true,
                canUseDuringTypewriterTurn: true,
                requiresTargetKey: false,
                requiresTargetOpponent: true
            );
        }

        private static void RegisterRuntimeCard(
            AbilityCardType type,
            string id,
            string title,
            string desc,
            string tag,
            Color color,
            int weight,
            bool canUseDuringCardSelection,
            bool canUseDuringTypewriterTurn,
            bool requiresTargetKey,
            bool requiresTargetOpponent)
        {
            CardData card = CreateInstance<CardData>();
            card.name = $"RuntimeCard_{id}";
            card.hideFlags = HideFlags.DontSave;
            card.cardType = type;
            card.cardId = id;
            card.displayName = title;
            card.description = desc;
            card.shortTag = tag;
            card.accentColor = color;
            card.defaultWeight = weight;
            card.canUseDuringCardSelection = canUseDuringCardSelection;
            card.canUseDuringTypewriterTurn = canUseDuringTypewriterTurn;
            card.requiresTargetKey = requiresTargetKey;
            card.requiresTargetOpponent = requiresTargetOpponent;

            _cachedByType[type] = card;
            _cachedList.Add(card);
        }
    }

    /// <summary>
    /// Yardımcı statik katalog sarmalayıcısı.
    /// </summary>
    public static class CardDataCatalog
    {
        public static CardData GetDefinition(AbilityCardType type) => CardData.GetDefinition(type);
        public static IReadOnlyList<CardData> GetAllDefinitions() => CardData.GetAllDefinitions();
    }
}
