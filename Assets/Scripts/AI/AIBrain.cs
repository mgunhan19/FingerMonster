using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FingerMonster.Core;
using FingerMonster.Gameplay;
using FingerMonster.Player;

namespace FingerMonster.AI
{
    /// <summary>
    /// Bot oyuncuların karar verme, olasılık analizi ve görsel nişan alma (aim) simülasyonunu yöneten beyin sınıfı.
    /// Botlar tuzak yerlerini ASLA bilmez (Fair Play).
    /// Kişilik ve zorluk seviyesine göre 1-3 saniye tereddüt eder, tuşun üzerine fare imleci gibi gelip sarı renkle odaklar, sonra basar.
    /// </summary>
    public class AIBrain : MonoBehaviour
    {
        private readonly Dictionary<int, BotProfile> botProfiles = new Dictionary<int, BotProfile>();
        private Coroutine activeDecisionRoutine;

        public void RegisterBotProfile(int slotIndex, BotProfile profile)
        {
            botProfiles[slotIndex] = profile;
        }

        public BotProfile GetProfile(int slotIndex)
        {
            if (botProfiles.TryGetValue(slotIndex, out var profile))
            {
                return profile;
            }
            return BotProfile.GenerateRandom(slotIndex);
        }

        /// <summary>
        /// Botun maç içi olaylara (kendi güvenli hamlesi, rakibin el kaybetmesi vb.) kişiliğine göre komik emote tepkisi vermesi.
        /// </summary>
        public void TriggerBotReactionEmote(int botSlotIndex, PlayerVisualAvatar avatar, bool isSafeKey, bool isRivalFailure)
        {
            if (avatar == null) return;
            BotProfile profile = GetProfile(botSlotIndex);

            EmoteType chosen;
            switch (profile.Personality)
            {
                case BotPersonality.Cesur:
                    chosen = isRivalFailure ? EmoteType.Kabadayi_BoomingLaugh : EmoteType.Kabadayi_ChestThump;
                    break;
                case BotPersonality.Urkek:
                    chosen = isRivalFailure ? EmoteType.Korkak_ChickenMock : EmoteType.Korkak_TrembleBite;
                    break;
                case BotPersonality.Sinsi:
                default:
                    chosen = isRivalFailure ? EmoteType.Sinsi_SlowClap : EmoteType.Sinsi_FingerWag;
                    break;
            }

            if (UnityEngine.Random.value < 0.75f)
            {
                avatar.TriggerEmote(chosen);
            }
        }

        /// <summary>
        /// Botun sırası geldiğinde karar verme ve tuşa basma sürecini başlatır.
        /// </summary>
        public void DecideAndExecuteTurn(int botPlayerIndex, GameManager gameManager, Action<int, int> onKeyChosenCallback = null)
        {
            if (activeDecisionRoutine != null) StopCoroutine(activeDecisionRoutine);
            activeDecisionRoutine = StartCoroutine(DecisionProcessRoutine(botPlayerIndex, gameManager, onKeyChosenCallback));
        }

        private IEnumerator DecisionProcessRoutine(int botPlayerIndex, GameManager gameManager, Action<int, int> onKeyChosenCallback)
        {
            BotProfile profile = GetProfile(botPlayerIndex);

            // 1. Kişiliğe göre insan benzeri düşünme süresi hesapla (1 - 3 sn)
            float thinkDelay = GetThinkDelay(profile.Personality);
            yield return new WaitForSeconds(thinkDelay);

            if (gameManager == null || gameManager.CurrentState != GameState.PlayerTurn || gameManager.ActivePlayerIndex != botPlayerIndex)
            {
                yield break;
            }

            // 2. Basılmamış olan geçerli tuşları topla
            List<int> availableKeys = new List<int>();
            int total = gameManager.Settings.totalKeys;

            for (int i = 0; i < total; i++)
            {
                if (!gameManager.IsKeyPressed(i))
                {
                    availableKeys.Add(i);
                }
            }

            if (availableKeys.Count == 0) yield break;

            // 3. Zorluk ve kişiliğe göre adil tuş seçimi yap (Tuzakların yeri bilinmez!)
            int chosenKey = SelectKeyByStrategy(availableKeys, profile, gameManager);

            // 4. İnsan gibi tuşa nişan alma / imleç simülasyonu (Tuş 0.35 - 0.5 sn sarı yanarak odaklanır)
            TypewriterMachine tw = gameManager.Typewriter;
            if (tw != null)
            {
                TypewriterKey keyComponent = tw.GetKey(chosenKey);
                if (keyComponent != null)
                {
                    keyComponent.SetHovered(true);
                    yield return new WaitForSeconds(0.4f);
                    keyComponent.SetHovered(false);
                }
            }

            if (gameManager.CurrentState != GameState.PlayerTurn || gameManager.ActivePlayerIndex != botPlayerIndex)
            {
                yield break;
            }

            // 5. Hamleyi yürüt (Network veya Local)
            if (onKeyChosenCallback != null)
            {
                onKeyChosenCallback.Invoke(botPlayerIndex, chosenKey);
            }
            else
            {
                PressKeyCommand cmd = new PressKeyCommand(botPlayerIndex, chosenKey);
                gameManager.ProcessCommand(cmd);
            }
        }

        private float GetThinkDelay(BotPersonality personality)
        {
            switch (personality)
            {
                case BotPersonality.Cesur:
                    // Cesur: Hızlı ve gözü pek
                    return UnityEngine.Random.Range(1.0f, 1.6f);
                case BotPersonality.Urkek:
                    // Ürkek: Titrek, uzun tereddütlü
                    return UnityEngine.Random.Range(2.0f, 3.0f);
                case BotPersonality.Sinsi:
                    // Sinsi: Bekler, ölçer biçer
                    return UnityEngine.Random.Range(1.4f, 2.3f);
                default:
                    return UnityEngine.Random.Range(1.2f, 2.0f);
            }
        }

        private int SelectKeyByStrategy(List<int> availableKeys, BotProfile profile, GameManager gm)
        {
            if (availableKeys.Count == 1) return availableKeys[0];

            switch (profile.Difficulty)
            {
                case BotDifficulty.Kolay:
                    // Kolay: Tamamen rastgele
                    return availableKeys[UnityEngine.Random.Range(0, availableKeys.Count)];

                case BotDifficulty.Normal:
                    // Normal: Kişilik tercihine göre (Cesur orta tuşları, Ürkek kenarları tercih eder)
                    return SelectWithPersonalityBias(availableKeys, profile.Personality);

                case BotDifficulty.Zor:
                    // Zor: Olasılık ve risk analizi
                    return SelectWithProbabilityAnalysis(availableKeys, profile, gm);

                default:
                    return availableKeys[UnityEngine.Random.Range(0, availableKeys.Count)];
            }
        }

        private int SelectWithPersonalityBias(List<int> availableKeys, BotPersonality personality)
        {
            // Orta tuşlar (Daktilo merkezinde) vs Kenar tuşlar
            List<int> centerKeys = new List<int>();
            List<int> edgeKeys = new List<int>();

            foreach (int k in availableKeys)
            {
                int col = (k < 10) ? k : (k - 10);
                if (col >= 3 && col <= 6) centerKeys.Add(k);
                else edgeKeys.Add(k);
            }

            if (personality == BotPersonality.Cesur && centerKeys.Count > 0 && UnityEngine.Random.value < 0.7f)
            {
                return centerKeys[UnityEngine.Random.Range(0, centerKeys.Count)];
            }

            if (personality == BotPersonality.Urkek && edgeKeys.Count > 0 && UnityEngine.Random.value < 0.75f)
            {
                return edgeKeys[UnityEngine.Random.Range(0, edgeKeys.Count)];
            }

            return availableKeys[UnityEngine.Random.Range(0, availableKeys.Count)];
        }

        private int SelectWithProbabilityAnalysis(List<int> availableKeys, BotProfile profile, GameManager gm)
        {
            // Zor seviye: Kalan güvenli tuş / toplam tuş oranını analiz eder
            return SelectWithPersonalityBias(availableKeys, profile.Personality);
        }
    }
}
