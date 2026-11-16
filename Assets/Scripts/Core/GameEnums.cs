namespace FingerMonster.Core
{
    /// <summary>
    /// Oyun durum makinesinin (FSM) ana aşamaları.
    /// </summary>
    public enum GameState
    {
        Lobby,              // Lobi / Bekleme aşaması
        RoundStart,         // Yeni tur hazırlığı ve tuzakların dağıtımı
        CardPlay,           // Stratejik yetenek kartı oynama aşaması
        CardSelection,      // 3 Kart seçimi (Push-Your-Luck mini-oyun: 1 Güvenli, 2 Canavar)
        PlayerTurn,         // Sırası gelen oyuncunun daktilo tuşuna basma anı (kartta canavar çıkarsa)
        KeyResolve,         // Tuşa basıldı, güvenlik/tuzak kontrolü
        HandLost,           // Tuzak tetiklendi, oyuncunun eli koptu efekti
        ButcherSequence,    // İki eli giden oyuncunun başında kasap belirir
        RoundEnd,           // Tur sonuçlandırılıyor
        GameOver            // Oyun bitti, kazanan belirlendi
    }

    /// <summary>
    /// Oyuncuların elinde tutabileceği ve sırası geldiğinde kullanabileceği 6 stratejik yetenek kartı türü.
    /// </summary>
    public enum AbilityCardType
    {
        None = -1,
        Shield = 0,          // Kalkan: 1 tuzak tuş cezasını engeller
        Possess = 1,         // Ele Geçir: 3 kart seçiminde 1 canavar kartının yerini gösterir
        XRay = 2,            // Röntgen: Seçilen 1 daktilo tuşunun güvenli/tuzak olduğunu gösterir
        SkipTurn = 3,        // Sıra Atlat: Bu turu tamamen pas geçer (üst üste 2 tur kullanılamaz)
        ReverseDirection = 4,// Ters Yön: Tur sonuna kadar sıra yönünü tersine çevirir
        Fog = 5              // Sis: Hedef rakibin bir sonraki 3 kart seçimini sisler ve Ele Geçir'i engeller
    }

    /// <summary>
    /// Oyuncunun ellerini temsil eden enum.
    /// </summary>
    public enum PlayerHand
    {
        None,
        Left,
        Right
    }

    /// <summary>
    /// Elin mevcut durumu.
    /// </summary>
    public enum HandState
    {
        Intact, // Sağlam / Yerinde
        Lost    // Kopmuş / Kaybedilmiş
    }

    /// <summary>
    /// Tuşa basılma sonucunun değerlendirilmesi.
    /// </summary>
    public enum KeyResult
    {
        Invalid,
        AlreadyPressed,
        Safe,
        Trap
    }

    /// <summary>
    /// Hayaletin (elenen oyuncunun) masa üzerinde tetikleyebileceği atmosferik etkiler.
    /// </summary>
    public enum GhostEffectType
    {
        FlickerLamp = 0, // Lambayı titret
        EerieLaugh = 1,  // Uğursuz kahkaha
        FlyPaper = 2,    // Kâğıdı uçur
        ColdBreeze = 3,  // Soğuk esinti
        Applause = 4,    // Alkış
        SadBoo = 5       // Hüzünlü "buuu"
    }

    /// <summary>
    /// Hayaletin (Kasabın Yardımcısı) yapabileceği aksiyon türleri.
    /// </summary>
    public enum GhostActionType
    {
        None = 0,
        CurseKey = 1,
        ChooseWeapon = 2,
        GhostEffect = 3
    }

    /// <summary>
    /// Hayaletin canlı oyunculara veya masaya fırlatabileceği çizgi film nesneleri.
    /// </summary>
    public enum ThrowableItemType
    {
        CreamPie = 0,
        Tomato = 1,
        PaperBall = 2,
        Sausage = 3
    }

    /// <summary>
    /// Hayalet kamerasının ve hareketinin çalışma modu.
    /// </summary>
    public enum GhostMovementMode
    {
        FreeFlight = 0,
        SpectatorOrbit = 1
    }
}

