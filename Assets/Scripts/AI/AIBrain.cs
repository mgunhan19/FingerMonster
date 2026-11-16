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
    /// Bot hayaletin aktif modda seçebileceği kişilik bazlı aksiyon türleri.
    /// </summary>
    public enum BotGhostActiveChoice
    {
        None = 0,
        CurseKey = 1,
        SelectWeapon = 2,
        TriggerEffect = 3,
        ScareTarget = 4,
        ThrowItem = 5
    }

    /// <summary>
    /// Bot hayaletin aktif karar çıktısı.
    /// </summary>
    public struct BotGhostDecision
    {
        public BotGhostActiveChoice Choice;
        public int TargetSeat;
        public int WeaponIndex;
        public GhostEffectType EffectType;
        public ThrowableItemType ThrowItem;
    }

    /// <summary>
    /// Bot oyuncuların karar verme, olasılık analizi ve görsel nişan alma (aim) simülasyonunu yöneten beyin sınıfı.
    /// Botlar tuzak yerlerini ASLA bilmez (Fair Play).
    /// Kişilik ve zorluk seviyesine göre 1-3 saniye tereddüt eder, tuşun üzerine fare imleci gibi gelip sarı renkle odaklar, sonra basar.
    /// </summary>
    public class AIBrain : MonoBehaviour
    {
        private readonly Dictionary<int, BotProfile> botProfiles = new Dictionary<int, BotProfile>();
        private Coroutine activeDecisionRoutine;
        private Coroutine activeCardDecisionCoroutine;

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
        /// Zorluk seviyesine göre botun emote tepkisi verme olasılığını döndürür.
        /// </summary>
        public float GetEmoteChanceForDifficulty(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Kolay: return 0.30f;
                case BotDifficulty.Zor:   return 0.85f;
                case BotDifficulty.Normal:
                default:                  return 0.60f;
            }
        }

        /// <summary>
        /// Kişilik ve olaya göre anlamlı 1..5 emote slotu seçer.
        /// </summary>
        public int PickReactionSlot(BotPersonality personality, bool isRivalFailure)
        {
            switch (personality)
            {
                case BotPersonality.Cesur: // Feg: 1=Gür Kahkaha, 2=Meydan Okuma, 3=Sıradaki Sensin, 4=Ağır Alkış, 5=Deli Bakışı
                    return isRivalFailure ? (UnityEngine.Random.value < 0.65f ? 1 : 4) : (UnityEngine.Random.value < 0.6f ? 2 : 3);
                case BotPersonality.Urkek: // Kenan: 1=Sahte Ağlama, 2=Panik Bakış, 3=Korkuyla Reddet, 4=Sinir Krizi, 5=Suçlama Parmağı
                    return isRivalFailure ? (UnityEngine.Random.value < 0.6f ? 2 : 5) : (UnityEngine.Random.value < 0.6f ? 1 : 3);
                case BotPersonality.Sovmen: // MMG: 1=Şovmen Dansı, 2=Alkış İsteme, 3=Sahne Kahkahası, 4=Artist Hareket, 5=Spot Işığı
                    return isRivalFailure ? (UnityEngine.Random.value < 0.6f ? 3 : 5) : (UnityEngine.Random.value < 0.6f ? 1 : 2);
                case BotPersonality.Sinsi: // Feyzi: 1=Yavaş Alkış, 2=Cık Cık Sitem, 3=Hedef İşareti, 4=Sinsi Alay, 5=Kıs Kıs Gülüş
                default:
                    return isRivalFailure ? (UnityEngine.Random.value < 0.6f ? 1 : 5) : (UnityEngine.Random.value < 0.6f ? 2 : 4);
            }
        }

        /// <summary>
        /// Botun maç içi olaylara (kendi güvenli hamlesi, rakibin el kaybetmesi vb.) kişiliğine göre komik emote tepkisi vermesi.
        /// Oynatılan slot (1..5) veya atlandıysa 0 döndürür.
        /// </summary>
        public int TriggerBotReactionEmote(int botSlotIndex, PlayerVisualAvatar avatar, bool isSafeKey, bool isRivalFailure, Vector3? targetPos = null, CharacterEmoteData emoteData = null)
        {
            if (avatar == null) return 0;
            BotProfile profile = GetProfile(botSlotIndex);
            float chance = GetEmoteChanceForDifficulty(profile.Difficulty);
            if (UnityEngine.Random.value > chance) return 0;

            int slot = PickReactionSlot(profile.Personality, isRivalFailure);
            avatar.TriggerEmoteSlot(slot, targetPos, emoteData);
            return slot;
        }

        /// <summary>
        /// Botun sırası geldiğinde kart mini-oyunu karar sürecini başlatır.
        /// </summary>
        public void DecideAndExecuteCard(int botPlayerIndex, FingerMonster.Gameplay.CardSelectionController cardController)
        {
            if (cardController == null) return;
            if (activeCardDecisionCoroutine != null)
            {
                StopCoroutine(activeCardDecisionCoroutine);
            }
            activeCardDecisionCoroutine = StartCoroutine(BotCardDecisionRoutine(botPlayerIndex, cardController));
        }

        private IEnumerator BotCardDecisionRoutine(int botPlayerIndex, FingerMonster.Gameplay.CardSelectionController cardController)
        {
            BotProfile profile = GetProfile(botPlayerIndex);

            // Kişiliğe göre 0.8 - 1.5 sn düşünme süresi
            float thinkDelay = UnityEngine.Random.Range(0.8f, 1.5f);
            yield return new WaitForSeconds(thinkDelay);

            activeCardDecisionCoroutine = null;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.CardSelection || gm.ActivePlayerIndex != botPlayerIndex)
            {
                yield break;
            }

            // Adil oyun: Bot kartların arkasını bilmez, varsayılan olarak 0, 1, 2 arasından adil rastgele seçim yapar
            int chosenCard = UnityEngine.Random.Range(0, 3);

            if (gm.Players != null && botPlayerIndex >= 0 && botPlayerIndex < gm.Players.Count)
            {
                PlayerData botPlayer = gm.Players[botPlayerIndex];
                if (botPlayer != null && !botPlayer.UsedAbilityThisTurn && botPlayer.AbilityCards != null && botPlayer.AbilityCards.Count > 0)
                {
                    if (UnityEngine.Random.value <= GetAbilityCardUseChance(profile.Difficulty))
                    {
                        int skipSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.SkipTurn);
                        int possessSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.Possess);
                        int fogSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.Fog);
                        int revSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.ReverseDirection);

                        if (botPlayer.RemainingHandsCount == 1 && skipSlot >= 0 && !botPlayer.UsedSkipLastTurn)
                        {
                            if (gm.TryPlayAbilityCard(botPlayerIndex, skipSlot))
                            {
                                yield break;
                            }
                        }
                        else if (possessSlot >= 0 && !botPlayer.IsFogged)
                        {
                            if (gm.TryPlayAbilityCard(botPlayerIndex, possessSlot))
                            {
                                yield return new WaitForSeconds(0.35f);

                                if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.CardSelection || GameManager.Instance.ActivePlayerIndex != botPlayerIndex)
                                {
                                    yield break;
                                }

                                int hintedMonster = cardController != null ? cardController.HintedMonsterCardIndex : -1;
                                if (hintedMonster >= 0 && hintedMonster < 3)
                                {
                                    List<int> remainingOptions = new List<int>(2);
                                    for (int c = 0; c < 3; c++)
                                    {
                                        if (c != hintedMonster) remainingOptions.Add(c);
                                    }
                                    if (remainingOptions.Count > 0)
                                    {
                                        chosenCard = remainingOptions[UnityEngine.Random.Range(0, remainingOptions.Count)];
                                    }
                                }
                            }
                        }
                        else if (fogSlot >= 0)
                        {
                            int targetSeat = SelectFogTargetSeat(gm, botPlayerIndex);
                            if (targetSeat >= 0 && gm.TryPlayAbilityCard(botPlayerIndex, fogSlot, targetSeat))
                            {
                                yield return new WaitForSeconds(0.3f);
                            }
                        }
                        else if (revSlot >= 0 && (profile.Difficulty == BotDifficulty.Zor || profile.Difficulty == BotDifficulty.Normal) && UnityEngine.Random.value < 0.60f)
                        {
                            if (gm.TryPlayAbilityCard(botPlayerIndex, revSlot))
                            {
                                yield return new WaitForSeconds(0.3f);
                            }
                        }
                    }
                }
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.CardSelection || GameManager.Instance.ActivePlayerIndex != botPlayerIndex)
            {
                yield break;
            }

            if (cardController != null)
            {
                cardController.SelectCard(chosenCard);
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

            int chosenKey = -1;

            // 2.5 Yerel oyunda daktilo aşaması yetenek kartı kararları
            if (onKeyChosenCallback == null && gameManager.Players != null && botPlayerIndex >= 0 && botPlayerIndex < gameManager.Players.Count)
            {
                PlayerData botPlayer = gameManager.Players[botPlayerIndex];
                if (botPlayer != null && !botPlayer.UsedAbilityThisTurn && botPlayer.AbilityCards != null && botPlayer.AbilityCards.Count > 0)
                {
                    if (UnityEngine.Random.value <= GetAbilityCardUseChance(profile.Difficulty))
                    {
                        int skipSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.SkipTurn);
                        int xraySlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.XRay);
                        int shieldSlot = FindAbilityCardSlot(botPlayer.AbilityCards, AbilityCardType.Shield);

                        float trapRatio = gameManager.Settings != null && availableKeys.Count > 0
                            ? (float)gameManager.Settings.trapCount / availableKeys.Count
                            : 0f;
                        bool shouldSkip = botPlayer.RemainingHandsCount == 1
                            || (profile.Difficulty == BotDifficulty.Zor && trapRatio >= 0.45f);

                        if (skipSlot >= 0 && !botPlayer.UsedSkipLastTurn && shouldSkip)
                        {
                            if (gameManager.TryPlayAbilityCard(botPlayerIndex, skipSlot))
                            {
                                yield break;
                            }
                        }
                        else if (xraySlot >= 0)
                        {
                            int inspectKey = SelectKeyByStrategy(availableKeys, profile, gameManager);
                            if (gameManager.TryPlayAbilityCard(botPlayerIndex, xraySlot, inspectKey))
                            {
                                yield return new WaitForSeconds(0.35f);

                                if (gameManager == null || gameManager.CurrentState != GameState.PlayerTurn || gameManager.ActivePlayerIndex != botPlayerIndex)
                                {
                                    yield break;
                                }

                                TypewriterKey inspectedComp = gameManager.Typewriter != null ? gameManager.Typewriter.GetKey(inspectKey) : null;
                                if (inspectedComp != null && inspectedComp.HasXRayReveal)
                                {
                                    if (!inspectedComp.XRayIsTrap)
                                    {
                                        chosenKey = inspectKey;
                                    }
                                    else
                                    {
                                        availableKeys.Remove(inspectKey);
                                        if (availableKeys.Count == 0) yield break;
                                    }
                                }
                            }
                        }
                        else if (shieldSlot >= 0 && botPlayer.RemainingHandsCount == 1)
                        {
                            if (gameManager.TryPlayAbilityCard(botPlayerIndex, shieldSlot))
                            {
                                yield return new WaitForSeconds(0.25f);

                                if (gameManager == null || gameManager.CurrentState != GameState.PlayerTurn || gameManager.ActivePlayerIndex != botPlayerIndex)
                                {
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }

            // 3. Zorluk ve kişiliğe göre adil tuş seçimi yap (Tuzakların yeri bilinmez!)
            if (chosenKey < 0)
            {
                chosenKey = SelectKeyByStrategy(availableKeys, profile, gameManager);
            }

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
                case BotPersonality.Sovmen:
                    // Şovmen: Teatral tempo
                    return UnityEngine.Random.Range(1.1f, 1.9f);
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

        /// <summary>
        /// Zorluk seviyesine göre botun elindeki yetenek kartlarını değerlendirme olasılığını döndürür.
        /// </summary>
        private float GetAbilityCardUseChance(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Kolay: return 0.40f;
                case BotDifficulty.Zor:   return 0.95f;
                case BotDifficulty.Normal:
                default:                  return 0.75f;
            }
        }

        private int FindAbilityCardSlot(IReadOnlyList<AbilityCardType> hand, AbilityCardType targetType)
        {
            if (hand == null) return -1;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == targetType) return i;
            }
            return -1;
        }

        private int SelectFogTargetSeat(GameManager gm, int botPlayerIndex)
        {
            if (gm == null || gm.Players == null) return -1;

            int bestSeat = -1;
            int bestPriority = int.MinValue;

            for (int i = 0; i < gm.Players.Count; i++)
            {
                if (i == botPlayerIndex) continue;
                PlayerData rival = gm.Players[i];
                if (rival == null || !rival.IsAlive) continue;

                // 2 eli olan rakiplere öncelik ver, ardından en yüksek skoru hedefle
                int priority = (rival.RemainingHandsCount >= 2 ? 100000 : 0) + rival.Score;
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestSeat = i;
                }
            }

            return bestSeat;
        }

        /// <summary>
        /// NetworkGameManager sunucu tarafı botları için kader (3 kart) seçimi öncesi oynanacak yetenek kartı slotunu belirler.
        /// </summary>
        public int DecideBotAbilityCardBeforeFate(IReadOnlyList<AbilityCardType> hand, BotDifficulty difficulty, int remainingHands, bool usedSkipLastTurn, bool isFogged, out AbilityCardType chosenCard)
        {
            chosenCard = AbilityCardType.None;
            if (hand == null || hand.Count == 0) return -1;
            if (UnityEngine.Random.value > GetAbilityCardUseChance(difficulty)) return -1;

            int skipSlot = FindAbilityCardSlot(hand, AbilityCardType.SkipTurn);
            if (remainingHands == 1 && !usedSkipLastTurn && skipSlot >= 0)
            {
                chosenCard = AbilityCardType.SkipTurn;
                return skipSlot;
            }

            int possessSlot = FindAbilityCardSlot(hand, AbilityCardType.Possess);
            if (!isFogged && possessSlot >= 0)
            {
                chosenCard = AbilityCardType.Possess;
                return possessSlot;
            }

            int fogSlot = FindAbilityCardSlot(hand, AbilityCardType.Fog);
            if (fogSlot >= 0)
            {
                chosenCard = AbilityCardType.Fog;
                return fogSlot;
            }

            int revSlot = FindAbilityCardSlot(hand, AbilityCardType.ReverseDirection);
            if (revSlot >= 0 && (difficulty == BotDifficulty.Zor || difficulty == BotDifficulty.Normal) && UnityEngine.Random.value < 0.60f)
            {
                chosenCard = AbilityCardType.ReverseDirection;
                return revSlot;
            }

            return -1;
        }

        /// <summary>
        /// NetworkGameManager sunucu tarafı botları için daktilo tuşu basımı öncesi oynanacak yetenek kartı slotunu belirler.
        /// </summary>
        public int DecideBotAbilityCardBeforeKey(IReadOnlyList<AbilityCardType> hand, BotDifficulty difficulty, int remainingHands, bool usedSkipLastTurn, out AbilityCardType chosenCard)
        {
            chosenCard = AbilityCardType.None;
            if (hand == null || hand.Count == 0) return -1;
            if (UnityEngine.Random.value > GetAbilityCardUseChance(difficulty)) return -1;

            int skipSlot = FindAbilityCardSlot(hand, AbilityCardType.SkipTurn);
            if (remainingHands == 1 && !usedSkipLastTurn && skipSlot >= 0)
            {
                chosenCard = AbilityCardType.SkipTurn;
                return skipSlot;
            }

            int xraySlot = FindAbilityCardSlot(hand, AbilityCardType.XRay);
            if (xraySlot >= 0)
            {
                chosenCard = AbilityCardType.XRay;
                return xraySlot;
            }

            int shieldSlot = FindAbilityCardSlot(hand, AbilityCardType.Shield);
            if (remainingHands == 1 && shieldSlot >= 0)
            {
                chosenCard = AbilityCardType.Shield;
                return shieldSlot;
            }

            return -1;
        }

        /// <summary>
        /// Hayalet (elenmiş bot) rolündeki yapay zekânın kişiliğine, ruh enerjisine ve geçerli tuşlara göre
        /// yapacağı hayalet aksiyonunu belirler. Tuzak yerlerini asla bilmez.
        /// </summary>
        public GhostActionType DecideGhostAction(
            int botSeatIndex,
            int currentSoulEnergy,
            IReadOnlyList<int> availableUnpressedKeys,
            IReadOnlyCollection<int> xrayExcludedKeys,
            out int targetKeyIndex,
            out int targetWeaponIndex,
            out GhostEffectType targetEffectType)
        {
            BotProfile profile = GetProfile(botSeatIndex);
            targetKeyIndex = -1;
            targetWeaponIndex = UnityEngine.Random.Range(0, 4);
            targetEffectType = PickGhostEffectForPersonality(profile.Personality);

            List<int> validKeys = FilterValidGhostKeys(availableUnpressedKeys, xrayExcludedKeys);
            bool canCurse = currentSoulEnergy >= 2 && validKeys.Count > 0;
            bool canEffect = currentSoulEnergy >= 1;

            GhostActionType desired = RollGhostActionByPersonality(profile.Personality);

            if (desired == GhostActionType.CurseKey)
            {
                if (canCurse)
                {
                    targetKeyIndex = validKeys[UnityEngine.Random.Range(0, validKeys.Count)];
                    return GhostActionType.CurseKey;
                }

                if (canEffect)
                {
                    return UnityEngine.Random.value < 0.5f
                        ? GhostActionType.GhostEffect
                        : GhostActionType.ChooseWeapon;
                }

                return GhostActionType.ChooseWeapon;
            }

            if (desired == GhostActionType.GhostEffect)
            {
                return canEffect ? GhostActionType.GhostEffect : GhostActionType.ChooseWeapon;
            }

            return GhostActionType.ChooseWeapon;
        }

        private static GhostActionType RollGhostActionByPersonality(BotPersonality personality)
        {
            float curseWeight;
            float effectWeight;

            switch (personality)
            {
                case BotPersonality.Sinsi: // Feyzi: %70 CurseKey, %20 GhostEffect, %10 ChooseWeapon
                    curseWeight = 0.70f;
                    effectWeight = 0.20f;
                    break;
                case BotPersonality.Sovmen: // MMG: %70 GhostEffect, %20 ChooseWeapon, %10 CurseKey
                    curseWeight = 0.10f;
                    effectWeight = 0.70f;
                    break;
                case BotPersonality.Cesur: // Feg: %50 ChooseWeapon, %30 CurseKey, %20 GhostEffect
                    curseWeight = 0.30f;
                    effectWeight = 0.20f;
                    break;
                case BotPersonality.Urkek: // Kenan: %40 GhostEffect, %35 CurseKey, %25 ChooseWeapon
                default:
                    curseWeight = 0.35f;
                    effectWeight = 0.40f;
                    break;
            }

            float roll = UnityEngine.Random.value;
            if (roll < curseWeight)
            {
                return GhostActionType.CurseKey;
            }

            if (roll < curseWeight + effectWeight)
            {
                return GhostActionType.GhostEffect;
            }

            return GhostActionType.ChooseWeapon;
        }

        private static List<int> FilterValidGhostKeys(IReadOnlyList<int> availableUnpressedKeys, IReadOnlyCollection<int> xrayExcludedKeys)
        {
            List<int> validKeys = new List<int>();
            if (availableUnpressedKeys == null || availableUnpressedKeys.Count == 0)
            {
                return validKeys;
            }

            HashSet<int> excludedSet = null;
            if (xrayExcludedKeys != null && xrayExcludedKeys.Count > 0)
            {
                excludedSet = new HashSet<int>(xrayExcludedKeys);
            }

            for (int i = 0; i < availableUnpressedKeys.Count; i++)
            {
                int keyIndex = availableUnpressedKeys[i];
                if (excludedSet == null || !excludedSet.Contains(keyIndex))
                {
                    validKeys.Add(keyIndex);
                }
            }

            return validKeys;
        }

        private static GhostEffectType PickGhostEffectForPersonality(BotPersonality personality)
        {
            // %25 ihtimalle tüm 0..5 arası efektlerden rastgele seç, %75 kişiliğe uygun komik tepki seç
            if (UnityEngine.Random.value < 0.25f)
            {
                return (GhostEffectType)UnityEngine.Random.Range(0, 6);
            }

            switch (personality)
            {
                case BotPersonality.Sinsi:
                {
                    GhostEffectType[] pool = { GhostEffectType.EerieLaugh, GhostEffectType.ColdBreeze, GhostEffectType.FlickerLamp };
                    return pool[UnityEngine.Random.Range(0, pool.Length)];
                }
                case BotPersonality.Sovmen:
                {
                    GhostEffectType[] pool = { GhostEffectType.Applause, GhostEffectType.FlyPaper, GhostEffectType.FlickerLamp };
                    return pool[UnityEngine.Random.Range(0, pool.Length)];
                }
                case BotPersonality.Cesur:
                {
                    GhostEffectType[] pool = { GhostEffectType.EerieLaugh, GhostEffectType.Applause, GhostEffectType.FlyPaper };
                    return pool[UnityEngine.Random.Range(0, pool.Length)];
                }
                case BotPersonality.Urkek:
                default:
                {
                    GhostEffectType[] pool = { GhostEffectType.ColdBreeze, GhostEffectType.SadBoo, GhostEffectType.FlickerLamp };
                    return pool[UnityEngine.Random.Range(0, pool.Length)];
                }
            }
        }

        /// <summary>
        /// Aktif bot hayaletin kişiliğine, enerjisine ve canlı hedeflere göre aksiyon kararını verir.
        /// Tuzak yerlerini asla bilmez (Fair Play).
        /// </summary>
        public static BotGhostDecision DecideBotGhostActiveAction(
            BotPersonality personality,
            int availableEnergy,
            List<int> aliveSeats,
            float[] targetScareImmunityRemaining,
            bool canScare,
            bool canThrow)
        {
            BotGhostDecision decision = new BotGhostDecision
            {
                Choice = BotGhostActiveChoice.None,
                TargetSeat = -1,
                WeaponIndex = 0,
                EffectType = GhostEffectType.FlickerLamp,
                ThrowItem = ThrowableItemType.PaperBall
            };

            bool hasAliveTargets = aliveSeats != null && aliveSeats.Count > 0;
            List<int> scareableSeats = CollectScareableSeats(aliveSeats, targetScareImmunityRemaining);

            bool allowScare = canScare && availableEnergy >= 1 && scareableSeats.Count > 0;
            bool allowThrow = canThrow && availableEnergy >= 1 && hasAliveTargets;
            bool allowCurse = availableEnergy >= 2;
            bool allowEffect = availableEnergy >= 1;

            switch (personality)
            {
                case BotPersonality.Sinsi:
                {
                    if (allowScare && allowCurse)
                    {
                        if (UnityEngine.Random.value < 0.55f)
                        {
                            decision.Choice = BotGhostActiveChoice.ScareTarget;
                            decision.TargetSeat = scareableSeats[UnityEngine.Random.Range(0, scareableSeats.Count)];
                            return decision;
                        }

                        decision.Choice = BotGhostActiveChoice.CurseKey;
                        return decision;
                    }

                    if (allowScare)
                    {
                        decision.Choice = BotGhostActiveChoice.ScareTarget;
                        decision.TargetSeat = scareableSeats[UnityEngine.Random.Range(0, scareableSeats.Count)];
                        return decision;
                    }

                    if (allowCurse)
                    {
                        decision.Choice = BotGhostActiveChoice.CurseKey;
                        return decision;
                    }

                    if (allowEffect)
                    {
                        decision.Choice = BotGhostActiveChoice.TriggerEffect;
                        decision.EffectType = UnityEngine.Random.value < 0.5f ? GhostEffectType.EerieLaugh : GhostEffectType.ColdBreeze;
                        return decision;
                    }

                    if (allowThrow)
                    {
                        decision.Choice = BotGhostActiveChoice.ThrowItem;
                        decision.TargetSeat = aliveSeats[UnityEngine.Random.Range(0, aliveSeats.Count)];
                        decision.ThrowItem = ThrowableItemType.PaperBall;
                        return decision;
                    }

                    decision.Choice = BotGhostActiveChoice.SelectWeapon;
                    decision.WeaponIndex = UnityEngine.Random.Range(0, 4);
                    return decision;
                }

                case BotPersonality.Sovmen:
                {
                    if (allowThrow && UnityEngine.Random.value < 0.65f)
                    {
                        decision.Choice = BotGhostActiveChoice.ThrowItem;
                        decision.TargetSeat = aliveSeats[UnityEngine.Random.Range(0, aliveSeats.Count)];
                        decision.ThrowItem = UnityEngine.Random.value < 0.5f ? ThrowableItemType.CreamPie : ThrowableItemType.Tomato;
                        return decision;
                    }

                    if (allowEffect && UnityEngine.Random.value < 0.35f)
                    {
                        decision.Choice = BotGhostActiveChoice.TriggerEffect;
                        decision.EffectType = UnityEngine.Random.value < 0.6f ? GhostEffectType.Applause : GhostEffectType.FlyPaper;
                        return decision;
                    }

                    decision.Choice = BotGhostActiveChoice.SelectWeapon;
                    decision.WeaponIndex = UnityEngine.Random.value < 0.5f ? 3 : 2; // CreamPieLauncher=3, SausageWhip=2
                    return decision;
                }

                case BotPersonality.Urkek:
                {
                    if (allowEffect && allowThrow)
                    {
                        if (UnityEngine.Random.value < 0.55f)
                        {
                            decision.Choice = BotGhostActiveChoice.TriggerEffect;
                            decision.EffectType = UnityEngine.Random.value < 0.5f ? GhostEffectType.Applause : GhostEffectType.ColdBreeze;
                            return decision;
                        }

                        decision.Choice = BotGhostActiveChoice.ThrowItem;
                        decision.TargetSeat = aliveSeats[UnityEngine.Random.Range(0, aliveSeats.Count)];
                        decision.ThrowItem = ThrowableItemType.PaperBall;
                        return decision;
                    }

                    if (allowEffect)
                    {
                        decision.Choice = BotGhostActiveChoice.TriggerEffect;
                        decision.EffectType = UnityEngine.Random.value < 0.5f ? GhostEffectType.Applause : GhostEffectType.ColdBreeze;
                        return decision;
                    }

                    if (allowThrow)
                    {
                        decision.Choice = BotGhostActiveChoice.ThrowItem;
                        decision.TargetSeat = aliveSeats[UnityEngine.Random.Range(0, aliveSeats.Count)];
                        decision.ThrowItem = ThrowableItemType.PaperBall;
                        return decision;
                    }

                    decision.Choice = BotGhostActiveChoice.SelectWeapon;
                    decision.WeaponIndex = UnityEngine.Random.Range(0, 4);
                    return decision;
                }

                case BotPersonality.Cesur:
                default:
                {
                    float roll = UnityEngine.Random.value;
                    if (allowCurse && roll < 0.40f)
                    {
                        decision.Choice = BotGhostActiveChoice.CurseKey;
                        return decision;
                    }

                    if (allowThrow && roll < 0.65f)
                    {
                        decision.Choice = BotGhostActiveChoice.ThrowItem;
                        decision.TargetSeat = aliveSeats[UnityEngine.Random.Range(0, aliveSeats.Count)];
                        decision.ThrowItem = ThrowableItemType.Sausage;
                        return decision;
                    }

                    if (allowScare && roll < 0.85f)
                    {
                        decision.Choice = BotGhostActiveChoice.ScareTarget;
                        decision.TargetSeat = scareableSeats[UnityEngine.Random.Range(0, scareableSeats.Count)];
                        return decision;
                    }

                    if (allowCurse)
                    {
                        decision.Choice = BotGhostActiveChoice.CurseKey;
                        return decision;
                    }

                    decision.Choice = BotGhostActiveChoice.SelectWeapon;
                    decision.WeaponIndex = UnityEngine.Random.value < 0.5f ? 0 : 1; // GiantMeatMallet=0, FlyingCastIronPan=1
                    return decision;
                }
            }
        }

        /// <summary>
        /// Bot hayaletlerin kişiliğine göre oda sınırları (roomBounds) içinde güvenli uçuş hedefi hesaplar.
        /// </summary>
        public static Vector3 ComputeBotGhostRoamTarget(
            BotPersonality personality,
            int seatIndex,
            Vector3[] seatPositions,
            Bounds roomBounds,
            float timeSeed)
        {
            if (roomBounds.size.sqrMagnitude < 0.01f)
            {
                roomBounds = new Bounds(Vector3.zero, new Vector3(8f, 3.5f, 8f));
            }

            Vector3 center = roomBounds.center;
            Vector3 extents = roomBounds.extents;
            float phase = timeSeed + seatIndex * 1.73f;
            Vector3 target;

            switch (personality)
            {
                case BotPersonality.Sinsi:
                {
                    Vector3 anchor = center;
                    if (seatPositions != null && seatPositions.Length > 0)
                    {
                        int targetIdx = Mathf.Abs(seatIndex + Mathf.FloorToInt(timeSeed * 0.15f)) % seatPositions.Length;
                        anchor = seatPositions[targetIdx];
                    }

                    Vector3 toCenter = center - anchor;
                    toCenter.y = 0f;
                    Vector3 approachDir = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector3.forward;
                    Vector3 sideOffset = new Vector3(Mathf.Sin(phase * 1.4f), 0f, Mathf.Cos(phase * 1.4f)) * 0.45f;
                    target = anchor + approachDir * 1.15f + sideOffset;
                    target.y = center.y + 0.35f + Mathf.Sin(phase * 1.8f) * 0.2f;
                    break;
                }

                case BotPersonality.Urkek:
                {
                    float angle = phase * 0.45f;
                    float rx = Mathf.Max(0.5f, extents.x - 0.6f);
                    float rz = Mathf.Max(0.5f, extents.z - 0.6f);
                    target = new Vector3(
                        center.x + Mathf.Cos(angle) * rx,
                        center.y + Mathf.Min(extents.y * 0.65f, 0.9f) + Mathf.Sin(phase * 1.1f) * 0.15f,
                        center.z + Mathf.Sin(angle) * rz);
                    break;
                }

                case BotPersonality.Sovmen:
                {
                    float angle = phase * 0.95f;
                    float rx = extents.x * 0.35f;
                    float rz = extents.z * 0.35f;
                    target = new Vector3(
                        center.x + Mathf.Sin(angle) * rx,
                        center.y + 0.45f + Mathf.Sin(phase * 2.1f) * 0.35f,
                        center.z + Mathf.Sin(angle * 2f) * rz * 0.6f);
                    break;
                }

                case BotPersonality.Cesur:
                default:
                {
                    float angle = phase * 1.15f;
                    float rx = extents.x * 0.55f;
                    float rz = extents.z * 0.55f;
                    target = new Vector3(
                        center.x + Mathf.Cos(angle) * rx,
                        center.y + 0.25f + Mathf.Cos(phase * 1.6f) * 0.25f,
                        center.z + Mathf.Sin(angle) * rz);
                    break;
                }
            }

            float marginX = Mathf.Min(0.4f, extents.x * 0.2f);
            float marginY = Mathf.Min(0.3f, extents.y * 0.2f);
            float marginZ = Mathf.Min(0.4f, extents.z * 0.2f);

            target.x = Mathf.Clamp(target.x, roomBounds.min.x + marginX, roomBounds.max.x - marginX);
            target.y = Mathf.Clamp(target.y, roomBounds.min.y + marginY, roomBounds.max.y - marginY);
            target.z = Mathf.Clamp(target.z, roomBounds.min.z + marginZ, roomBounds.max.z - marginZ);
            return target;
        }

        private static List<int> CollectScareableSeats(List<int> aliveSeats, float[] targetScareImmunityRemaining)
        {
            List<int> result = new List<int>();
            if (aliveSeats == null || aliveSeats.Count == 0)
            {
                return result;
            }

            for (int i = 0; i < aliveSeats.Count; i++)
            {
                int seat = aliveSeats[i];
                bool hasImmunity = targetScareImmunityRemaining != null
                    && seat >= 0
                    && seat < targetScareImmunityRemaining.Length
                    && targetScareImmunityRemaining[seat] > 0f;
                if (!hasImmunity)
                {
                    result.Add(seat);
                }
            }

            return result;
        }
    }
}
