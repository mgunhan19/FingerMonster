using System;
using UnityEngine;

namespace FingerMonster.Player
{
    [Serializable]
    public class EmoteSlotInfo
    {
        [Range(1, 5)] public int SlotNumber = 1;
        public string DisplayName = "Alay";
        public string PopupText = "HA!";
        public Color PopupColor = Color.yellow;
        public string ExpectedMixamoFile = "Emote1.fbx";
        public string RecommendedMixamoSearch = "Taunt";
        [TextArea(1, 2)] public string Description = "Rakibe komik bir hareket yapar.";
    }

    /// <summary>
    /// Karakter başına 5 özel emote slotunun görünen adlarını, baloncuk yazılarını,
    /// beklenen Mixamo dosya adlarını ve rehber açıklamalarını tutan ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "EmoteData_Character", menuName = "FingerMonster/Character Emote Data")]
    public class CharacterEmoteData : ScriptableObject
    {
        public CharacterArchetype Archetype;
        public string CharacterName;
        public string PersonalityTag;
        public EmoteSlotInfo[] Slots = new EmoteSlotInfo[5];

        public EmoteSlotInfo GetSlot(int slotIndex1Based)
        {
            int idx = Mathf.Clamp(slotIndex1Based - 1, 0, Slots != null ? Slots.Length - 1 : 0);
            return (Slots != null && idx < Slots.Length) ? Slots[idx] : null;
        }

        /// <summary>
        /// Asset bulunamazsa çalışma zamanında varsayılan kişilik emote verisi döndürür.
        /// </summary>
        public static CharacterEmoteData CreateRuntimeFallback(CharacterArchetype archetype)
        {
            var data = CreateInstance<CharacterEmoteData>();
            data.Archetype = archetype;
            var def = CharacterDefinition.GetDefinition(archetype);
            data.CharacterName = def.DisplayName;
            data.PersonalityTag = def.PersonalityTag;
            data.Slots = GetDefaultSlotsForArchetype(archetype);
            return data;
        }

        public static EmoteSlotInfo[] GetDefaultSlotsForArchetype(CharacterArchetype archetype)
        {
            switch (archetype)
            {
                case CharacterArchetype.Kabadayi: // Feg - Cesur / Kabadayı
                    return new EmoteSlotInfo[]
                    {
                        new EmoteSlotInfo { SlotNumber = 1, DisplayName = "Gür Kahkaha", PopupText = "MUAHAHA!", PopupColor = new Color(1f, 0.35f, 0.25f), ExpectedMixamoFile = "Emote1_Feg.fbx", RecommendedMixamoSearch = "Sitting Laughing / Laughing", Description = "Rakibin haline kahkahalarla güler." },
                        new EmoteSlotInfo { SlotNumber = 2, DisplayName = "Meydan Okuma", PopupText = "GEL BURAYA!", PopupColor = new Color(1f, 0.55f, 0.15f), ExpectedMixamoFile = "Emote2_Feg.fbx", RecommendedMixamoSearch = "Taunt Gesture / Chest Pound", Description = "Rakibe korkusuzca meydan okur." },
                        new EmoteSlotInfo { SlotNumber = 3, DisplayName = "Sıradaki Sensin", PopupText = "SIRADAKİ SENSİN!", PopupColor = Color.yellow, ExpectedMixamoFile = "Emote3_Feg.fbx", RecommendedMixamoSearch = "Pointing / Pointing Threat", Description = "Parmağıyla hedef rakibi işaret eder." },
                        new EmoteSlotInfo { SlotNumber = 4, DisplayName = "Ağır Alkış", PopupText = "BRAVO PAŞAM!", PopupColor = new Color(0.95f, 0.75f, 0.25f), ExpectedMixamoFile = "Emote4_Feg.fbx", RecommendedMixamoSearch = "Sitting Clap / Sarcastic Clap", Description = "Rakibin hamlesini alaycı şekilde alkışlar." },
                        new EmoteSlotInfo { SlotNumber = 5, DisplayName = "Deli Bakışı", PopupText = "KAFAYI YEDİM!", PopupColor = Color.red, ExpectedMixamoFile = "Emote5_Feg.fbx", RecommendedMixamoSearch = "Crazy Gesture / Flexing", Description = "Çılgınca el kol hareketleri yapar." }
                    };

                case CharacterArchetype.Korkak: // Kenan - Ürkek / Panikleyen Protokol
                    return new EmoteSlotInfo[]
                    {
                        new EmoteSlotInfo { SlotNumber = 1, DisplayName = "Sahte Ağlama", PopupText = "ÜHÜ ÜHÜ!", PopupColor = new Color(0.45f, 0.75f, 1f), ExpectedMixamoFile = "Emote1_Kenan.fbx", RecommendedMixamoSearch = "Crying / Sad Idle", Description = "Ellerini yüzüne kapatıp panikle ağlar." },
                        new EmoteSlotInfo { SlotNumber = 2, DisplayName = "Panik Bakış", PopupText = "KİMSE VAR MI?!", PopupColor = Color.cyan, ExpectedMixamoFile = "Emote2_Kenan.fbx", RecommendedMixamoSearch = "Looking / Terrified", Description = "Etrafına korkuyla göz gezdirir." },
                        new EmoteSlotInfo { SlotNumber = 3, DisplayName = "Korkuyla Reddet", PopupText = "YAPMA NOLUR!", PopupColor = new Color(0.75f, 0.85f, 1f), ExpectedMixamoFile = "Emote3_Kenan.fbx", RecommendedMixamoSearch = "Sitting Disapproval / Shaking Head No", Description = "Başını sallayarak tuzağı reddeder." },
                        new EmoteSlotInfo { SlotNumber = 4, DisplayName = "Sinir Krizi", PopupText = "AKLIM GİTTİ!", PopupColor = new Color(1f, 0.8f, 0.3f), ExpectedMixamoFile = "Emote4_Kenan.fbx", RecommendedMixamoSearch = "Crazy Gesture / Nervous", Description = "Stresten çılgına dönmüş gibi titrer." },
                        new EmoteSlotInfo { SlotNumber = 5, DisplayName = "Suçlama Parmağı", PopupText = "O YAPTI!", PopupColor = new Color(0.35f, 0.65f, 1f), ExpectedMixamoFile = "Emote5_Kenan.fbx", RecommendedMixamoSearch = "Pointing / Blaming", Description = "Panikle hedef rakibi işaret eder." }
                    };

                case CharacterArchetype.Sinsi: // Feyzi - Sinsi / Kurnaz Fedai
                    return new EmoteSlotInfo[]
                    {
                        new EmoteSlotInfo { SlotNumber = 1, DisplayName = "Yavaş Alkış", PopupText = "ŞAK... ŞAK...", PopupColor = new Color(0.4f, 0.95f, 0.55f), ExpectedMixamoFile = "Emote1_Feyzi.fbx", RecommendedMixamoSearch = "Sitting Clap / Slow Clap", Description = "Rakibi sinir etmek için yavaşça alkışlar." },
                        new EmoteSlotInfo { SlotNumber = 2, DisplayName = "Cık Cık Sitem", PopupText = "CIK CIK CIK...", PopupColor = new Color(0.7f, 0.4f, 0.95f), ExpectedMixamoFile = "Emote2_Feyzi.fbx", RecommendedMixamoSearch = "Sitting Disapproval / Finger Wag", Description = "Kurnazca başını sallayıp onaylamaz." },
                        new EmoteSlotInfo { SlotNumber = 3, DisplayName = "Hedef İşareti", PopupText = "TUZAK SENİN!", PopupColor = Color.green, ExpectedMixamoFile = "Emote3_Feyzi.fbx", RecommendedMixamoSearch = "Pointing / Sneaky Point", Description = "Sinsi bir sırıtışla rakibi gösterir." },
                        new EmoteSlotInfo { SlotNumber = 4, DisplayName = "Sinsi Alay", PopupText = "ÇOK ZAYIFSIN!", PopupColor = new Color(0.85f, 0.35f, 0.95f), ExpectedMixamoFile = "Emote4_Feyzi.fbx", RecommendedMixamoSearch = "Taunt Gesture / Mock", Description = "Rakibi kışkırtan el hareketi yapar." },
                        new EmoteSlotInfo { SlotNumber = 5, DisplayName = "Kıs Kıs Gülüş", PopupText = "HIHIHI...", PopupColor = new Color(0.35f, 0.9f, 0.45f), ExpectedMixamoFile = "Emote5_Feyzi.fbx", RecommendedMixamoSearch = "Sitting Laughing / Evil Laugh", Description = "Rakip zorlanınca kıs kıs güler." }
                    };

                case CharacterArchetype.Sovmen: // MMG - Şovmen / VIP Dansçı
                default:
                    return new EmoteSlotInfo[]
                    {
                        new EmoteSlotInfo { SlotNumber = 1, DisplayName = "Şovmen Dansı", PopupText = "SAHNE BENİM!", PopupColor = new Color(1f, 0.8f, 0.2f), ExpectedMixamoFile = "Emote1_MMG.fbx", RecommendedMixamoSearch = "Crazy Gesture / Hip Hop Dance", Description = "VIP tarzı çılgın şov hareketi yapar." },
                        new EmoteSlotInfo { SlotNumber = 2, DisplayName = "Alkış İsteme", PopupText = "ALKIŞLAR BANA!", PopupColor = Color.yellow, ExpectedMixamoFile = "Emote2_MMG.fbx", RecommendedMixamoSearch = "Sitting Clap / Cheering", Description = "Masadakilerden alkış toplar." },
                        new EmoteSlotInfo { SlotNumber = 3, DisplayName = "Sahne Kahkahası", PopupText = "HAHAHA HARİKA!", PopupColor = new Color(1f, 0.65f, 0.15f), ExpectedMixamoFile = "Emote3_MMG.fbx", RecommendedMixamoSearch = "Sitting Laughing / Victory", Description = "Gösterişli bir sahne kahkahası atar." },
                        new EmoteSlotInfo { SlotNumber = 4, DisplayName = "Artist Hareket", PopupText = "İZLE VE ÖĞREN!", PopupColor = Color.white, ExpectedMixamoFile = "Emote4_MMG.fbx", RecommendedMixamoSearch = "Taunt Gesture / Bow", Description = "Artistçe rakibe hava atar." },
                        new EmoteSlotInfo { SlotNumber = 5, DisplayName = "Spot Işığı", PopupText = "SIRA SENDE YILDIZ!", PopupColor = new Color(1f, 0.85f, 0.35f), ExpectedMixamoFile = "Emote5_MMG.fbx", RecommendedMixamoSearch = "Pointing / Jazz Hands", Description = "Rakibi spot ışığına çıkarır gibi işaret eder." }
                    };
            }
        }
    }
}

