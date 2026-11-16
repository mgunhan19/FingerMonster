using UnityEngine;

namespace FingerMonster.Core
{
    /// <summary>
    /// Bir oyuncunun belirli bir tuşa basma eylemini temsil eden komut sınıfı.
    /// Aşama 2'de doğrudan serileştirilip ServerRpc ile sunucuya yollanabilir.
    /// </summary>
    public class PressKeyCommand : ICommand
    {
        public int PlayerIndex { get; }
        public int KeyIndex { get; }

        public PressKeyCommand(int playerIndex, int keyIndex)
        {
            PlayerIndex = playerIndex;
            KeyIndex = keyIndex;
        }

        public bool Validate(GameManager gameManager)
        {
            if (gameManager == null)
            {
                Debug.LogWarning("[PressKeyCommand] GameManager referansı boş!");
                return false;
            }

            // Oyun şu an oyuncu hamlesi bekleyen durumda olmalıdır
            if (gameManager.CurrentState != GameState.PlayerTurn)
            {
                Debug.LogWarning($"[PressKeyCommand] Geçersiz durum: {gameManager.CurrentState}. Yalnızca PlayerTurn anında basılabilir.");
                return false;
            }

            // Hamleyi yapan oyuncu mevcut sırada olan oyuncu olmalıdır
            if (gameManager.ActivePlayerIndex != PlayerIndex)
            {
                Debug.LogWarning($"[PressKeyCommand] Sıra hatası! Sıra Oyuncu {gameManager.ActivePlayerIndex + 1}'de ancak Oyuncu {PlayerIndex + 1} komut gönderdi.");
                return false;
            }

            // Tuş geçerli aralıkta olmalıdır
            if (KeyIndex < 0 || KeyIndex >= gameManager.Settings.totalKeys)
            {
                Debug.LogWarning($"[PressKeyCommand] Geçersiz tuş indeksi: {KeyIndex}.");
                return false;
            }

            // Tuşa daha önce basılmamış olmalıdır
            if (gameManager.IsKeyPressed(KeyIndex))
            {
                Debug.LogWarning($"[PressKeyCommand] {KeyIndex} nolu tuşa daha önce basılmış!");
                return false;
            }

            return true;
        }

        public void Execute(GameManager gameManager)
        {
            gameManager.ResolveKey(PlayerIndex, KeyIndex);
        }
    }
}

