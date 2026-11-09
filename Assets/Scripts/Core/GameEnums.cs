namespace FingerMonster.Core
{
    /// <summary>
    /// Oyun durum makinesinin (FSM) ana aşamaları.
    /// </summary>
    public enum GameState
    {
        Lobby,              // Lobi / Bekleme aşaması
        RoundStart,         // Yeni tur hazırlığı ve tuzakların dağıtımı
        PlayerTurn,         // Sırası gelen oyuncunun karar verme anı
        KeyResolve,         // Tuşa basıldı, güvenlik/tuzak kontrolü
        HandLost,           // Tuzak tetiklendi, oyuncunun eli koptu efekti
        ButcherSequence,    // İki eli giden oyuncunun başında kasap belirir
        RoundEnd,           // Tur sonuçlandırılıyor
        GameOver            // Oyun bitti, kazanan belirlendi
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
}

