using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using FingerMonster.Audio;
using FingerMonster.Core;
using FingerMonster.Player;
using FingerMonster.Gameplay;

namespace FingerMonster.UI
{
    /// <summary>
    /// Ana Menü sahnesindeki Başla, Oyun Modu (Solo vs Bot / Hotseat),
    /// Zorluk (Tuzak Sayısı), Ayarlar, Çıkış, Karakter Seçim Kartları,
    /// Oyun İçi Poster Görselleri ve Canlı 3B Karakter/Kasap Vitrinini yöneten kontrolcü.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menü Butonları")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button lanMultiplayerButton;
        [SerializeField] private Button modeButton;
        [SerializeField] private TextMeshProUGUI modeButtonText;
        [SerializeField] private Button difficultyButton;
        [SerializeField] private TextMeshProUGUI difficultyButtonText;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Karakter Seçimi (Feg, Kenan, Feyzi, MMG)")]
        [SerializeField] private Button[] characterSelectButtons = new Button[4];
        [SerializeField] private Image[] characterCardBackgrounds = new Image[4];
        [SerializeField] private TextMeshProUGUI selectedCharacterInfoText;

        [Header("Lobi ve Ayarlar Panelleri")]
        [SerializeField] private NetworkLobbyUI networkLobbyUI;
        [SerializeField] private SettingsMenuController settingsController;

        // 3B Vitrin Referansları
        private GameObject showcaseRoot;
        private readonly Transform[] showcaseCharacters = new Transform[4];
        private readonly Vector3[] showcaseBasePositions = new Vector3[4];
        private readonly Quaternion[] showcaseBaseRotations = new Quaternion[4];
        private Light characterHighlightLight;
        private Transform showcaseButcherRoot;

        private void Awake()
        {
            Application.runInBackground = true;

            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (lanMultiplayerButton != null) lanMultiplayerButton.onClick.AddListener(OnLanMultiplayerClicked);
            if (modeButton != null) modeButton.onClick.AddListener(OnModeToggled);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(OnDifficultyToggled);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (characterSelectButtons != null)
            {
                for (int i = 0; i < characterSelectButtons.Length; i++)
                {
                    int idx = i;
                    if (characterSelectButtons[i] != null)
                    {
                        characterSelectButtons[i].onClick.AddListener(() => SelectCharacter(idx));
                    }
                }
            }

            UpdateModeUI();
            UpdateDifficultyUI();
            RefreshCharacterCards();
        }

        private void Start()
        {
            EnhanceMainMenuLayoutAndShowcase();
            RefreshCharacterCards();
        }

        private void Update()
        {
            UpdateShowcaseAnimation();
        }

        public void SelectCharacter(int index)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            GameSessionData.SetSelectedCharacter(index);
            RefreshCharacterCards();
        }

        private void UpdateCharacterSelectionUI()
        {
            RefreshCharacterCards();
        }

        public void RefreshCharacterCards()
        {
            int selected = Mathf.Clamp(GameSessionData.SelectedCharacterIndex, 0, 3);
            CharacterArchetype selectedArchetype = (CharacterArchetype)selected;
            var selDef = CharacterDefinition.GetDefinition(selectedArchetype);

            if (selectedCharacterInfoText != null && selDef != null)
            {
                string hex = ColorUtility.ToHtmlStringRGB(selDef.PrimaryColor);
                string accentHex = ColorUtility.ToHtmlStringRGB(selDef.AccentColor);
                selectedCharacterInfoText.text =
                    $"<b><size=115%><color=#{hex}>{selDef.DisplayName.ToUpperInvariant()}</color></size></b>  " +
                    $"<color=#{accentHex}>[{selDef.PersonalityTag}]</color> — <i>{selDef.Title}</i>\n" +
                    $"<size=86%><color=#D8D2C8>{selDef.Description}</color></size>";
            }

            for (int i = 0; i < 4; i++)
            {
                var def = CharacterDefinition.GetDefinition((CharacterArchetype)i);
                bool isSel = (i == selected);

                Button btn = (characterSelectButtons != null && i < characterSelectButtons.Length) ? characterSelectButtons[i] : null;
                Image bg = (characterCardBackgrounds != null && i < characterCardBackgrounds.Length) ? characterCardBackgrounds[i] : null;
                if (bg == null && btn != null)
                {
                    bg = btn.GetComponent<Image>();
                }

                if (bg != null)
                {
                    Color selectedBgColor = Color.Lerp(def.PrimaryColor, new Color(0.22f, 0.18f, 0.14f), 0.35f);
                    selectedBgColor.a = 0.98f;
                    bg.color = isSel
                        ? selectedBgColor
                        : new Color(0.12f, 0.11f, 0.13f, 0.94f);

                    Outline outline = bg.GetComponent<Outline>();
                    if (outline == null)
                    {
                        outline = bg.gameObject.AddComponent<Outline>();
                    }
                    outline.effectColor = isSel
                        ? new Color(1f, 0.78f, 0.28f, 1f)
                        : new Color(def.PrimaryColor.r, def.PrimaryColor.g, def.PrimaryColor.b, 0.45f);
                    outline.effectDistance = isSel ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
                }

                if (btn != null)
                {
                    btn.transform.localScale = isSel ? Vector3.one * 1.06f : Vector3.one;

                    TextMeshProUGUI btnTmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnTmp != null)
                    {
                        btnTmp.text =
                            $"<b><size=112%>{def.DisplayName}</size></b>\n" +
                            $"<size=80%><color=#{ColorUtility.ToHtmlStringRGB(def.AccentColor)}>[{def.PersonalityTag}]</color></size>\n" +
                            $"<size=72%><color=#CFC8BC>{def.Title}</color></size>";
                    }
                }
            }

            if (characterHighlightLight != null && selDef != null)
            {
                characterHighlightLight.color = Color.Lerp(selDef.PrimaryColor, new Color(1f, 0.92f, 0.75f), 0.25f);
            }
        }

        /// <summary>
        /// Giriş sahnesinin UI düzenini, poster rozetlerini ve arka plandaki canlı 3B karakter/Kasap vitrinini kurar.
        /// </summary>
        public void EnhanceMainMenuLayoutAndShowcase()
        {
            EnhanceUILayoutAndPosters();
            EnsureMainMenu3DShowcase();
        }

        private void EnhanceUILayoutAndPosters()
        {
            Transform mainPanel = transform.Find("MainPanel");
            if (mainPanel == null && playButton != null)
            {
                mainPanel = playButton.transform.parent;
            }
            if (mainPanel == null) return;

            RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(960f, 820f);
            }

            Image panelImg = mainPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0.07f, 0.06f, 0.08f, 0.84f);
                Outline panelOutline = panelImg.GetComponent<Outline>();
                if (panelOutline == null) panelOutline = panelImg.gameObject.AddComponent<Outline>();
                panelOutline.effectColor = new Color(0.85f, 0.55f, 0.20f, 0.65f);
                panelOutline.effectDistance = new Vector2(2f, -2f);
            }

            // Başlık ve Alt Başlık stil iyileştirmesi
            Transform titleTr = mainPanel.Find("TitleText");
            if (titleTr != null)
            {
                RectTransform rt = titleTr.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, 330f);
                TextMeshProUGUI tmp = titleTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.color = new Color(0.98f, 0.24f, 0.18f);
                    tmp.fontSize = 66f;
                }
            }

            Transform subTr = mainPanel.Find("SubtitleText");
            if (subTr != null)
            {
                RectTransform rt = subTr.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, 274f);
                TextMeshProUGUI tmp = subTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.color = new Color(0.92f, 0.86f, 0.76f);
                    tmp.fontSize = 21f;
                }
            }

            Transform charTitleTr = mainPanel.Find("CharSelectTitle");
            if (charTitleTr != null)
            {
                RectTransform rt = charTitleTr.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, 232f);
            }

            // 4 Karakter kartını ferah boyutlara (205x96) hizala
            if (characterSelectButtons != null)
            {
                float startX = -330f;
                float stepX = 220f;
                for (int i = 0; i < characterSelectButtons.Length && i < 4; i++)
                {
                    if (characterSelectButtons[i] == null) continue;
                    RectTransform crt = characterSelectButtons[i].GetComponent<RectTransform>();
                    if (crt != null)
                    {
                        crt.anchoredPosition = new Vector2(startX + (i * stepX), 162f);
                        crt.sizeDelta = new Vector2(205f, 96f);
                    }
                }
            }

            // Seçili karakter bilgi kutusu
            if (selectedCharacterInfoText != null)
            {
                RectTransform infoRt = selectedCharacterInfoText.GetComponent<RectTransform>();
                if (infoRt != null)
                {
                    infoRt.anchoredPosition = new Vector2(0f, 76f);
                    infoRt.sizeDelta = new Vector2(760f, 64f);
                }
            }

            // Mod ve Zorluk butonları
            if (modeButton != null)
            {
                RectTransform rt = modeButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(-190f, 4f);
                    rt.sizeDelta = new Vector2(360f, 50f);
                }
                AddSubtleButtonOutline(modeButton.gameObject, new Color(0.45f, 0.65f, 0.95f, 0.6f));
            }

            if (difficultyButton != null)
            {
                RectTransform rt = difficultyButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(190f, 4f);
                    rt.sizeDelta = new Vector2(360f, 50f);
                }
                AddSubtleButtonOutline(difficultyButton.gameObject, new Color(0.85f, 0.55f, 0.95f, 0.6f));
            }

            // PlayButton (Kehribar / Kırmızı vurgu)
            if (playButton != null)
            {
                RectTransform rt = playButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0f, -68f);
                    rt.sizeDelta = new Vector2(540f, 62f);
                }
                Image img = playButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.78f, 0.24f, 0.16f, 0.98f);
                AddSubtleButtonOutline(playButton.gameObject, new Color(1f, 0.78f, 0.28f, 0.9f), 2.5f);
            }

            // LanButton (Mavi / Turkuaz vurgu)
            if (lanMultiplayerButton != null)
            {
                RectTransform rt = lanMultiplayerButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0f, -142f);
                    rt.sizeDelta = new Vector2(540f, 60f);
                }
                Image img = lanMultiplayerButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.16f, 0.46f, 0.76f, 0.98f);
                AddSubtleButtonOutline(lanMultiplayerButton.gameObject, new Color(0.45f, 0.85f, 1f, 0.85f), 2f);
            }

            // SettingsButton & QuitButton yan yana yerleşim
            if (settingsButton != null)
            {
                RectTransform rt = settingsButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(-140f, -218f);
                    rt.sizeDelta = new Vector2(260f, 54f);
                }
                AddSubtleButtonOutline(settingsButton.gameObject, new Color(0.7f, 0.7f, 0.8f, 0.5f));
            }

            if (quitButton != null)
            {
                RectTransform rt = quitButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(140f, -218f);
                    rt.sizeDelta = new Vector2(260f, 54f);
                }
                AddSubtleButtonOutline(quitButton.gameObject, new Color(0.9f, 0.35f, 0.35f, 0.6f));
            }

            // Oyun İçi Poster Rozetleri (Sol ve Sağ Üst Köşeler)
            CreateOrUpdatePosterBadge(
                mainPanel,
                "PosterBadge_WatchFingers",
                "Assets/_Project/Art/Textures/Environment/Tex_Poster_WatchFingers.png",
                "Assets/_Project/Art/Materials/Mat_Poster_WatchFingers.mat",
                new Vector2(-385f, 305f),
                6f,
                "PARMAKLARINA DİKKAT!");

            CreateOrUpdatePosterBadge(
                mainPanel,
                "PosterBadge_ButcherWatching",
                "Assets/_Project/Art/Textures/Environment/Tex_Poster_ButcherWatching.png",
                "Assets/_Project/Art/Materials/Mat_Poster_ButcherWatching.mat",
                new Vector2(385f, 305f),
                -6f,
                "KASAP SENİ İZLİYOR!");
        }

        private static void AddSubtleButtonOutline(GameObject target, Color color, float thickness = 1.5f)
        {
            if (target == null) return;
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
        }

        private static void CreateOrUpdatePosterBadge(
            Transform parent,
            string badgeName,
            string textureAssetPath,
            string fallbackMaterialPath,
            Vector2 anchoredPos,
            float zRotation,
            string caption)
        {
            if (parent == null) return;

            Transform existing = parent.Find(badgeName);
            GameObject badgeObj = existing != null ? existing.gameObject : new GameObject(badgeName);
            badgeObj.transform.SetParent(parent, false);

            RectTransform rt = badgeObj.GetComponent<RectTransform>();
            if (rt == null) rt = badgeObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(132f, 156f);
            rt.localRotation = Quaternion.Euler(0f, 0f, zRotation);

            Image frameImg = badgeObj.GetComponent<Image>();
            if (frameImg == null) frameImg = badgeObj.AddComponent<Image>();
            frameImg.color = new Color(0.16f, 0.13f, 0.10f, 0.96f);
            frameImg.raycastTarget = false;

            Outline frameOutline = badgeObj.GetComponent<Outline>();
            if (frameOutline == null) frameOutline = badgeObj.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.92f, 0.68f, 0.24f, 0.9f);
            frameOutline.effectDistance = new Vector2(2f, -2f);

            Transform rawTr = badgeObj.transform.Find("PosterArt");
            GameObject rawObj = rawTr != null ? rawTr.gameObject : new GameObject("PosterArt");
            rawObj.transform.SetParent(badgeObj.transform, false);

            RectTransform rawRt = rawObj.GetComponent<RectTransform>();
            if (rawRt == null) rawRt = rawObj.AddComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.06f, 0.18f);
            rawRt.anchorMax = new Vector2(0.94f, 0.94f);
            rawRt.offsetMin = Vector2.zero;
            rawRt.offsetMax = Vector2.zero;

            RawImage rawImg = rawObj.GetComponent<RawImage>();
            if (rawImg == null) rawImg = rawObj.AddComponent<RawImage>();
            rawImg.raycastTarget = false;

            Texture2D loadedTex = LoadPosterTexture(textureAssetPath, fallbackMaterialPath);
            if (loadedTex != null)
            {
                rawImg.texture = loadedTex;
                rawImg.color = Color.white;
            }
            else
            {
                rawImg.color = new Color(0.55f, 0.18f, 0.14f, 1f);
            }

            Transform capTr = badgeObj.transform.Find("Caption");
            GameObject capObj = capTr != null ? capTr.gameObject : new GameObject("Caption");
            capObj.transform.SetParent(badgeObj.transform, false);
            RectTransform capRt = capObj.GetComponent<RectTransform>();
            if (capRt == null) capRt = capObj.AddComponent<RectTransform>();
            capRt.anchorMin = new Vector2(0.04f, 0.02f);
            capRt.anchorMax = new Vector2(0.96f, 0.18f);
            capRt.offsetMin = Vector2.zero;
            capRt.offsetMax = Vector2.zero;

            TextMeshProUGUI capTmp = capObj.GetComponent<TextMeshProUGUI>();
            if (capTmp == null) capTmp = capObj.AddComponent<TextMeshProUGUI>();
            capTmp.text = caption;
            capTmp.fontSize = 10.5f;
            capTmp.fontStyle = FontStyles.Bold;
            capTmp.color = new Color(0.96f, 0.84f, 0.55f);
            capTmp.alignment = TextAlignmentOptions.Center;
            capTmp.raycastTarget = false;
        }

        private static Texture2D LoadPosterTexture(string textureAssetPath, string fallbackMaterialPath)
        {
#if UNITY_EDITOR
            Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (tex != null) return tex;

            Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(fallbackMaterialPath);
            if (mat != null)
            {
                if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") is Texture2D baseTex)
                    return baseTex;
                if (mat.mainTexture is Texture2D mainTex)
                    return mainTex;
            }
#endif
            return null;
        }

        /// <summary>
        /// Giriş sahnesinin (MainMenu) arka planında 4 oynanabilir karakteri (Feg, Kenan, Feyzi, MMG)
        /// ve arkada tehditkar Kasap (kasap.fbx) modelini atmosferik ışıklandırmayla sergiler.
        /// </summary>
        public void EnsureMainMenu3DShowcase()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" && Object.FindFirstObjectByType<MainMenuController>() == null)
            {
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0f, 1.55f, -3.10f);
                mainCam.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
                mainCam.fieldOfView = 58f;
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.backgroundColor = new Color(0.05f, 0.04f, 0.06f);
            }

            GameObject existing = GameObject.Find("MainMenu_3DShowcase");
            if (existing != null)
            {
                showcaseRoot = existing;
                BindExistingShowcaseReferences();
                return;
            }

            showcaseRoot = new GameObject("MainMenu_3DShowcase");

            // 1. Atmosferik Zemin ve Arka Duvar
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ShowcaseFloor";
            floor.transform.SetParent(showcaseRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0f, 0f);
            floor.transform.localScale = new Vector3(1.6f, 1f, 1.4f);
            if (floor.GetComponent<Collider>() != null) DestroyImmediate(floor.GetComponent<Collider>());
            Renderer floorRen = floor.GetComponent<Renderer>();
            if (floorRen != null && urpLit != null)
            {
                Material fMat = new Material(urpLit);
                fMat.color = new Color(0.14f, 0.12f, 0.11f);
                if (fMat.HasProperty("_BaseColor")) fMat.SetColor("_BaseColor", new Color(0.14f, 0.12f, 0.11f));
                if (fMat.HasProperty("_Smoothness")) fMat.SetFloat("_Smoothness", 0.25f);
                Texture2D floorTex = LoadPosterTexture("Assets/_Project/Art/Textures/Environment/Tex_Floor_Tiles.png", "Assets/_Project/Art/Materials/Mat_Floor.mat");
                if (floorTex != null)
                {
                    fMat.mainTexture = floorTex;
                    if (fMat.HasProperty("_BaseMap")) fMat.SetTexture("_BaseMap", floorTex);
                }
                floorRen.sharedMaterial = fMat;
            }

            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "ShowcaseBackWall";
            backWall.transform.SetParent(showcaseRoot.transform, false);
            backWall.transform.localPosition = new Vector3(0f, 2.0f, 3.2f);
            backWall.transform.localScale = new Vector3(14f, 5f, 0.25f);
            if (backWall.GetComponent<Collider>() != null) DestroyImmediate(backWall.GetComponent<Collider>());
            Renderer wallRen = backWall.GetComponent<Renderer>();
            if (wallRen != null && urpLit != null)
            {
                Material wMat = new Material(urpLit);
                wMat.color = new Color(0.18f, 0.16f, 0.16f);
                if (wMat.HasProperty("_BaseColor")) wMat.SetColor("_BaseColor", new Color(0.18f, 0.16f, 0.16f));
                if (wMat.HasProperty("_Smoothness")) wMat.SetFloat("_Smoothness", 0.12f);
                Texture2D wallTex = LoadPosterTexture("Assets/_Project/Art/Textures/Environment/Tex_Wall_DirtyConcrete.png", "Assets/_Project/Art/Materials/Mat_Wall_Concrete.mat");
                if (wallTex != null)
                {
                    wMat.mainTexture = wallTex;
                    if (wMat.HasProperty("_BaseMap")) wMat.SetTexture("_BaseMap", wallTex);
                }
                wallRen.sharedMaterial = wMat;
            }

            // 2. Işıklandırma (Sıcak Kehribar Masa Spotu + Seçili Karakter Vurgu Işığı + Kırmızı Kasap Kontur Işığı)
            GameObject keySpotObj = new GameObject("ShowcaseKeySpotLight");
            keySpotObj.transform.SetParent(showcaseRoot.transform, false);
            keySpotObj.transform.localPosition = new Vector3(0f, 3.8f, -1.2f);
            keySpotObj.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
            Light keySpot = keySpotObj.AddComponent<Light>();
            keySpot.type = LightType.Spot;
            keySpot.range = 11f;
            keySpot.spotAngle = 78f;
            keySpot.intensity = 3.2f;
            keySpot.color = new Color(1.0f, 0.88f, 0.68f);

            GameObject charLightObj = new GameObject("CharacterHighlightLight");
            charLightObj.transform.SetParent(showcaseRoot.transform, false);
            charLightObj.transform.localPosition = new Vector3(-2.1f, 1.6f, -0.9f);
            characterHighlightLight = charLightObj.AddComponent<Light>();
            characterHighlightLight.type = LightType.Point;
            characterHighlightLight.range = 4.5f;
            characterHighlightLight.intensity = 2.6f;
            characterHighlightLight.color = new Color(0.95f, 0.35f, 0.25f);

            GameObject butcherRimObj = new GameObject("ButcherRimLight");
            butcherRimObj.transform.SetParent(showcaseRoot.transform, false);
            butcherRimObj.transform.localPosition = new Vector3(0f, 2.5f, 1.5f);
            butcherRimObj.transform.localRotation = Quaternion.Euler(35f, 180f, 0f);
            Light butcherRim = butcherRimObj.AddComponent<Light>();
            butcherRim.type = LightType.Spot;
            butcherRim.range = 7f;
            butcherRim.spotAngle = 62f;
            butcherRim.intensity = 3.4f;
            butcherRim.color = new Color(0.95f, 0.16f, 0.14f);

            // 3. 4 Oynanabilir Karakter Vitrini (Yarım Ay Dizilimi: Sol ve Sağ Kanatlarda Net Görünür)
            Vector3[] charPositions = new Vector3[]
            {
                new Vector3(-2.25f, 0f, -0.65f), // Feg (Sol Ön)
                new Vector3(-1.55f, 0f,  0.35f), // Kenan (Sol İç)
                new Vector3( 1.55f, 0f,  0.35f), // Feyzi (Sağ İç)
                new Vector3( 2.25f, 0f, -0.65f)  // MMG (Sağ Ön)
            };
            Quaternion[] charRotations = new Quaternion[]
            {
                Quaternion.Euler(0f, 148f, 0f),
                Quaternion.Euler(0f, 162f, 0f),
                Quaternion.Euler(0f, 198f, 0f),
                Quaternion.Euler(0f, 212f, 0f)
            };

            for (int i = 0; i < 4; i++)
            {
                showcaseBasePositions[i] = charPositions[i];
                showcaseBaseRotations[i] = charRotations[i];

                var def = CharacterDefinition.GetDefinition((CharacterArchetype)i);
                GameObject charInstance = SpawnShowcaseCharacter(def, showcaseRoot.transform, charPositions[i], charRotations[i]);
                if (charInstance != null)
                {
                    showcaseCharacters[i] = charInstance.transform;
                }
            }

            // 4. Arka Planda Tehditkar Kasap (kasap.fbx) Vitrini
            SpawnShowcaseButcher(showcaseRoot.transform);
        }

        private void BindExistingShowcaseReferences()
        {
            if (showcaseRoot == null) return;

            Transform hl = showcaseRoot.transform.Find("CharacterHighlightLight");
            if (hl != null) characterHighlightLight = hl.GetComponent<Light>();

            for (int i = 0; i < 4; i++)
            {
                var def = CharacterDefinition.GetDefinition((CharacterArchetype)i);
                Transform ch = showcaseRoot.transform.Find($"ShowcaseChar_{def.DisplayName}");
                if (ch != null)
                {
                    showcaseCharacters[i] = ch;
                    showcaseBasePositions[i] = ch.localPosition;
                    showcaseBaseRotations[i] = ch.localRotation;
                }
            }

            showcaseButcherRoot = showcaseRoot.transform.Find("Showcase_Butcher");
        }

        private static GameObject SpawnShowcaseCharacter(CharacterDefinition def, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            if (def == null) return null;

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabAssetPath);
#endif
            GameObject inst;
            if (prefab != null)
            {
                inst = Instantiate(prefab, parent);
            }
            else
            {
                inst = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                inst.transform.SetParent(parent, false);
            }

            inst.name = $"ShowcaseChar_{def.DisplayName}";
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = localRot;
            inst.transform.localScale = Vector3.one * 1.05f;

            PlayerVisualAvatar avatar = inst.GetComponent<PlayerVisualAvatar>();
            if (avatar != null)
            {
                avatar.SetPlayerColor(def.PrimaryColor);
            }

            return inst;
        }

        private void SpawnShowcaseButcher(Transform parent)
        {
            GameObject butcherHolder = new GameObject("Showcase_Butcher");
            butcherHolder.transform.SetParent(parent, false);
            butcherHolder.transform.localPosition = new Vector3(0f, 0f, 1.35f);
            butcherHolder.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            ButcherPlaceholder butcherComp = butcherHolder.AddComponent<ButcherPlaceholder>();
#if UNITY_EDITOR
            GameObject kasapPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Butcher/kasap.fbx");
            if (kasapPrefab != null)
            {
                butcherComp.SetCustomButcherModelPrefab(kasapPrefab);
            }
#endif
            butcherComp.EnsureKasapModelSetup();
            showcaseButcherRoot = butcherHolder.transform;
        }

        private void UpdateShowcaseAnimation()
        {
            if (showcaseRoot == null) return;

            int selected = Mathf.Clamp(GameSessionData.SelectedCharacterIndex, 0, 3);
            float t = Time.unscaledTime;

            for (int i = 0; i < 4; i++)
            {
                Transform ch = showcaseCharacters[i];
                if (ch == null) continue;

                bool isSel = (i == selected);
                Vector3 targetPos = showcaseBasePositions[i] + (isSel ? new Vector3(0f, 0f, -0.28f) : Vector3.zero);
                float targetScale = isSel ? 1.16f : 1.0f;
                float swayYaw = Mathf.Sin(t * 1.8f + i * 1.2f) * (isSel ? 6f : 2.5f);
                Quaternion targetRot = showcaseBaseRotations[i] * Quaternion.Euler(0f, swayYaw, 0f);

                ch.localPosition = Vector3.Lerp(ch.localPosition, targetPos, Time.unscaledDeltaTime * 6f);
                ch.localScale = Vector3.Lerp(ch.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 6f);
                ch.localRotation = Quaternion.Slerp(ch.localRotation, targetRot, Time.unscaledDeltaTime * 6f);

                if (isSel && characterHighlightLight != null)
                {
                    Vector3 lightTarget = ch.localPosition + new Vector3(0f, 1.45f, -0.55f);
                    characterHighlightLight.transform.localPosition = Vector3.Lerp(
                        characterHighlightLight.transform.localPosition,
                        lightTarget,
                        Time.unscaledDeltaTime * 7f);
                }
            }
        }

        private void OnLanMultiplayerClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (networkLobbyUI != null)
            {
                networkLobbyUI.OpenLobby();
            }
        }

        private void OnPlayClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            Time.timeScale = 1f;
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
            SceneManager.LoadScene("Game");
        }

        private void OnModeToggled()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            if (GameSessionData.SelectedMode == GameSessionData.GameMode.SoloVsBots)
            {
                GameSessionData.SetMode(GameSessionData.GameMode.HotseatParty);
            }
            else
            {
                GameSessionData.SetMode(GameSessionData.GameMode.SoloVsBots);
            }

            UpdateModeUI();
        }

        private void UpdateModeUI()
        {
            if (modeButtonText != null)
            {
                if (GameSessionData.SelectedMode == GameSessionData.GameMode.SoloVsBots)
                {
                    modeButtonText.text = "MOD: 1 OYUNCU (VS 3 BOT)";
                }
                else
                {
                    modeButtonText.text = "MOD: 4 KİŞİLİK HOTSEAT";
                }
            }
        }

        private void OnDifficultyToggled()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();

            switch (GameSessionData.SelectedDifficulty)
            {
                case GameSessionData.Difficulty.Easy:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Normal;
                    break;
                case GameSessionData.Difficulty.Normal:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Nightmare;
                    break;
                case GameSessionData.Difficulty.Nightmare:
                    GameSessionData.SelectedDifficulty = GameSessionData.Difficulty.Easy;
                    break;
            }

            UpdateDifficultyUI();
        }

        private void UpdateDifficultyUI()
        {
            if (difficultyButtonText != null)
            {
                switch (GameSessionData.SelectedDifficulty)
                {
                    case GameSessionData.Difficulty.Easy:
                        difficultyButtonText.text = "ZORLUK: KOLAY (4 TUZAK)";
                        break;
                    case GameSessionData.Difficulty.Normal:
                        difficultyButtonText.text = "ZORLUK: NORMAL (6 TUZAK)";
                        break;
                    case GameSessionData.Difficulty.Nightmare:
                        difficultyButtonText.text = "ZORLUK: KÂBUS (8 TUZAK)";
                        break;
                }
            }
        }

        private void OnSettingsClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            if (settingsController != null)
            {
                settingsController.OpenSettings();
            }
        }

        private void OnQuitClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
            Debug.Log("[MainMenu] Oyundan çıkılıyor...");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
