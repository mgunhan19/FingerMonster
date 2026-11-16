namespace FingerMonster.Core
{
    /// <summary>
    /// Ağ ve yerel girdi ayrımını sağlayan Komut deseni (Command Pattern) arabirimi.
    /// Tüm oyuncu eylemleri bir ICommand olarak paketlenip GameManager tarafından doğrulanır.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Komutun mevcut oyun kurallarına ve tur sırasına göre geçerli olup olmadığını sınar.
        /// </summary>
        bool Validate(GameManager gameManager);

        /// <summary>
        /// Komutu oyun mantığı üzerinde icra eder.
        /// </summary>
        void Execute(GameManager gameManager);
    }
}

