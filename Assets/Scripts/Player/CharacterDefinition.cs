using System;
using UnityEngine;

namespace FingerMonster.Player
{
    /// <summary>
    /// Masadaki 4 benzersiz çizgi film karakterinin kimlik türü.
    /// </summary>
    public enum CharacterArchetype
    {
        Kabadayi, // Karakter 1: İri yarı, geniş omuzlu, çatık kaşlı kabadayı
        Korkak,   // Karakter 2: Kambur, sürekli titreyen, pörtlek gözlü çelimsiz
        Sinsi,    // Karakter 3: Sıska, kıvrak, sivri hatlı, sinsi sırıtan düzenbaz
        Sovmen    // Karakter 4: Silindir şapkalı, papyonlu, artistik tiyatrocu/şovmen
    }

    /// <summary>
    /// Karakterin yapabileceği komik jest ve alay hareketleri (Emote).
    /// </summary>
    public enum EmoteType
    {
        // Kabadayı Emoteleri
        Kabadayi_ChestThump = 0,    // Göğüs yumruklama ("Gel buraya!")
        Kabadayi_BicepFlex = 1,     // Pazu şişirme pozu
        Kabadayi_PointThreat = 2,   // Parmakla işaret etme ("Sıradaki sensin!")
        Kabadayi_BoomingLaugh = 3,  // Başını geriye atıp kahkaha savurma

        // Korkak Emoteleri
        Korkak_TrembleBite = 4,     // Tir tir titreyip tırnak yeme
        Korkak_DuckTable = 5,       // Masanın altına saklanma hareketi
        Korkak_FakeCry = 6,         // Kollarıyla gözünü ovuşturup sahte ağlama
        Korkak_ChickenMock = 7,     // Tavuk taklidi kanat çırpma

        // Sinsi Emoteleri
        Sinsi_FingerWag = 8,        // Sinsi sırıtıp "cık cık" parmak sallama
        Sinsi_SlowClap = 9,         // Yavaş ve alaycı el çırpma
        Sinsi_TongueOut = 10,       // Kulaklarını tutup dil çıkarma
        Sinsi_HandInspect = 11,     // Rakibin kopan eline dürbün gibi bakma

        // Şovmen Emoteleri
        Sovmen_HatBow = 12,         // Şapka çıkarıp teatral reverans yapma
        Sovmen_JazzHands = 13,      // Caz elleri sallayarak dans etme
        Sovmen_ApplausePlease = 14, // Havaya alkış işareti yapma
        Sovmen_VictoryPose = 15     // Ayak atıp artistik zafer pozu verme
    }

    /// <summary>
    /// Karakterin görsel kimlik, renk, takma ad ve emote yapılandırmasını barındıran veri sınıfı.
    /// </summary>
    [Serializable]
    public class CharacterDefinition
    {
        public CharacterArchetype Archetype;
        public string DisplayName;
        public string Title;
        public string Description;
        public Color PrimaryColor;
        public Color AccentColor;
        public EmoteType[] AvailableEmotes;

        public static CharacterDefinition GetDefinition(CharacterArchetype archetype)
        {
            switch (archetype)
            {
                case CharacterArchetype.Kabadayi:
                    return new CharacterDefinition
                    {
                        Archetype = CharacterArchetype.Kabadayi,
                        DisplayName = "Kabadayı Kazım",
                        Title = "Masaların Belalısı",
                        Description = "Geniş omuzlu, çatık kaşlı ve korkusuz. Rakiplerine gözdağı vermeye bayılır.",
                        PrimaryColor = new Color(0.85f, 0.2f, 0.2f),
                        AccentColor = new Color(0.95f, 0.75f, 0.2f),
                        AvailableEmotes = new EmoteType[]
                        {
                            EmoteType.Kabadayi_ChestThump,
                            EmoteType.Kabadayi_BicepFlex,
                            EmoteType.Kabadayi_PointThreat,
                            EmoteType.Kabadayi_BoomingLaugh
                        }
                    };

                case CharacterArchetype.Korkak:
                    return new CharacterDefinition
                    {
                        Archetype = CharacterArchetype.Korkak,
                        DisplayName = "Tereddütlü Toni",
                        Title = "Korkudan Titreyen",
                        Description = "Kambur, pörtlek gözlü ve sürekli terleyen. Her tuşta ecel terleri döker.",
                        PrimaryColor = new Color(0.2f, 0.55f, 0.9f),
                        AccentColor = new Color(0.7f, 0.85f, 1.0f),
                        AvailableEmotes = new EmoteType[]
                        {
                            EmoteType.Korkak_TrembleBite,
                            EmoteType.Korkak_DuckTable,
                            EmoteType.Korkak_FakeCry,
                            EmoteType.Korkak_ChickenMock
                        }
                    };

                case CharacterArchetype.Sinsi:
                    return new CharacterDefinition
                    {
                        Archetype = CharacterArchetype.Sinsi,
                        DisplayName = "Sinsi Salih",
                        Title = "Tuzakların Efendisi",
                        Description = "Sivri burunlu, kıvrak ve hesapçı. Başkalarının elleri koptuğunda kıkırdar.",
                        PrimaryColor = new Color(0.25f, 0.75f, 0.35f),
                        AccentColor = new Color(0.6f, 0.2f, 0.7f),
                        AvailableEmotes = new EmoteType[]
                        {
                            EmoteType.Sinsi_FingerWag,
                            EmoteType.Sinsi_SlowClap,
                            EmoteType.Sinsi_TongueOut,
                            EmoteType.Sinsi_HandInspect
                        }
                    };

                case CharacterArchetype.Sovmen:
                default:
                    return new CharacterDefinition
                    {
                        Archetype = CharacterArchetype.Sovmen,
                        DisplayName = "Şovmen Şinasi",
                        Title = "Sahne Işıklarının Çocuğu",
                        Description = "Silindir şapkalı, papyonlu ve abartılı. Ölürken bile son reveransını yapar.",
                        PrimaryColor = new Color(0.95f, 0.65f, 0.15f),
                        AccentColor = new Color(0.9f, 0.9f, 0.95f),
                        AvailableEmotes = new EmoteType[]
                        {
                            EmoteType.Sovmen_HatBow,
                            EmoteType.Sovmen_JazzHands,
                            EmoteType.Sovmen_ApplausePlease,
                            EmoteType.Sovmen_VictoryPose
                        }
                    };
            }
        }
    }
}

