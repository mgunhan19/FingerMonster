using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Core;

namespace FingerMonster.Data
{
    /// <summary>
    /// Hayaletin fırlatabildiği çizgi film nesnelerinin uçuş ve sıçrama verilerini tutan ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrowableItemData", menuName = "FingerMonster/Throwable Item Data")]
    public class ThrowableItemData : ScriptableObject
    {
        [Header("Temel Kimlik")]
        public ThrowableItemType itemType;
        public string displayName;
        public string shortLabel;

        [Header("Görsel Renkler")]
        public Color primaryColor = Color.white;
        public Color splatColor = Color.white;

        [Header("Balistik ve Etki Parametreleri")]
        public float arcHeight = 1.25f;
        public float flightDuration = 0.55f;
        public float splatDuration = 3.0f;
        public float hitRadius = 0.45f;

        /// <summary>
        /// Asset atanmadığı durumlarda 4 varsayılan fırlatılabilir nesne listesi üretir.
        /// </summary>
        public static List<ThrowableItemData> CreateRuntimeDefaultList()
        {
            return new List<ThrowableItemData>
            {
                CreateDefault(
                    ThrowableItemType.CreamPie,
                    "Kremalı Pasta",
                    "Pasta",
                    new Color(1f, 0.95f, 0.82f),
                    new Color(1f, 0.98f, 0.9f),
                    1.3f,
                    0.58f,
                    3.2f,
                    0.5f),
                CreateDefault(
                    ThrowableItemType.Tomato,
                    "Domates",
                    "Domates",
                    new Color(0.92f, 0.18f, 0.16f),
                    new Color(0.85f, 0.12f, 0.12f),
                    1.15f,
                    0.50f,
                    3.0f,
                    0.42f),
                CreateDefault(
                    ThrowableItemType.PaperBall,
                    "Kâğıt Top",
                    "Kâğıt",
                    new Color(0.94f, 0.92f, 0.86f),
                    new Color(0.88f, 0.86f, 0.8f),
                    1.4f,
                    0.48f,
                    2.4f,
                    0.40f),
                CreateDefault(
                    ThrowableItemType.Sausage,
                    "Sosis",
                    "Sosis",
                    new Color(0.82f, 0.34f, 0.22f),
                    new Color(0.75f, 0.28f, 0.18f),
                    1.2f,
                    0.52f,
                    2.8f,
                    0.45f)
            };
        }

        private static ThrowableItemData CreateDefault(
            ThrowableItemType type,
            string displayName,
            string shortLabel,
            Color primaryColor,
            Color splatColor,
            float arcHeight,
            float flightDuration,
            float splatDuration,
            float hitRadius)
        {
            ThrowableItemData item = CreateInstance<ThrowableItemData>();
            item.name = $"ThrowableItemData_{type}";
            item.itemType = type;
            item.displayName = displayName;
            item.shortLabel = shortLabel;
            item.primaryColor = primaryColor;
            item.splatColor = splatColor;
            item.arcHeight = arcHeight;
            item.flightDuration = flightDuration;
            item.splatDuration = splatDuration;
            item.hitRadius = hitRadius;
            return item;
        }
    }
}
