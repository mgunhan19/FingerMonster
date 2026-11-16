MIXAMO EMOTE & ANIMASYON KILAVUZU
=================================
Sistem şu klasör yapısını otomatik olarak tarar:
- Assets/Characters/Animations/*.fbx            (Genel/ortak Mixamo klipleri: Sitting, Looking, Pointing vb.)
- Assets/Characters/Animations/Mixamo/Feg/      (Feg'e özel Emote1_Feg.fbx .. Emote5_Feg.fbx)
- Assets/Characters/Animations/Mixamo/Kenan/    (Kenan'a özel Emote1_Kenan.fbx .. Emote5_Kenan.fbx)
- Assets/Characters/Animations/Mixamo/Feyzi/    (Feyzi'ye özel Emote1_Feyzi.fbx .. Emote5_Feyzi.fbx)
- Assets/Characters/Animations/Mixamo/MMG/      (MMG'ye özel Emote1_MMG.fbx .. Emote5_MMG.fbx)

MIXAMO INDIRME AYARLARI:
- Format: FBX for Unity (.fbx)
- Skin: Without Skin
- Frame Rate: 30 fps

KARAKTER BASINA BEKLENEN 5 DOSYA VE ONERILEN ARAMA KELIMELERI:
1) FEG (Cesur / Kabadayı):
   - Emote1_Feg.fbx : Sitting Laughing, Laughing
   - Emote2_Feg.fbx : Taunt Gesture, Chest Pound
   - Emote3_Feg.fbx : Pointing, Threaten
   - Emote4_Feg.fbx : Sitting Clap, Sarcastic Clap
   - Emote5_Feg.fbx : Crazy Gesture, Flexing

2) KENAN (Ürkek / Panikleyen Protokol):
   - Emote1_Kenan.fbx : Crying, Sad Idle
   - Emote2_Kenan.fbx : Looking, Terrified
   - Emote3_Kenan.fbx : Sitting Disapproval, Shaking Head No
   - Emote4_Kenan.fbx : Crazy Gesture, Nervous
   - Emote5_Kenan.fbx : Pointing, Blaming

3) FEYZI (Sinsi / Kurnaz Fedai):
   - Emote1_Feyzi.fbx : Sitting Clap, Slow Clap
   - Emote2_Feyzi.fbx : Sitting Disapproval, Finger Wag
   - Emote3_Feyzi.fbx : Pointing, Sneaky Point
   - Emote4_Feyzi.fbx : Taunt Gesture, Mock
   - Emote5_Feyzi.fbx : Sitting Laughing, Evil Laugh

4) MMG (Şovmen / VIP Dansçı):
   - Emote1_MMG.fbx : Crazy Gesture, Hip Hop Dancing
   - Emote2_MMG.fbx : Sitting Clap, Cheering
   - Emote3_MMG.fbx : Sitting Laughing, Victory
   - Emote4_MMG.fbx : Taunt Gesture, Bow
   - Emote5_MMG.fbx : Pointing, Jazz Hands

NOT: Özel Emote1..5_<Ad>.fbx dosyaları henüz eklenmemişse sistem hata vermez;
öncelikle Assets/Characters/Animations/ içindeki mevcut Mixamo FBX'lerini
(Sitting, Sitting Clap, Sitting Disapproval, Sitting Laughing, Pointing, Crying,
Taunt Gesture, Crazy Gesture, Looking), onlar da yoksa prosedürel klipleri kullanır.

KURULUM:
Dosyaları ilgili klasörlere attıktan sonra Unity menüsünden:
'FingerMonster -> 2. Import & Remap Mixamo Animations' seçeneğine tıklayın.
Tüm FBX'ler otomatik Humanoid + Bake Into Pose yapılır, bacaklar UpperBodyEmoteMask ile
sandalyede sabitlenir ve eksik dosya varsa Mixamo_Import_Report.txt dosyasına yazılır.
