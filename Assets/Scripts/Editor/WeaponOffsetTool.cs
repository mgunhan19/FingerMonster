using UnityEditor;
using UnityEngine;
using FingerMonster.Data;
using FingerMonster.Gameplay;

namespace FingerMonster.Editor
{
    /// <summary>
    /// Kasap silahlarının WeaponSocket altındaki yerleşim ofsetlerini (pozisyon, rotasyon, ölçek)
    /// sahnede canlı önizleyip WeaponData ScriptableObject varlıklarına kaydeden Editor penceresi.
    /// </summary>
    public class WeaponOffsetTool : EditorWindow
    {
        private WeaponData selectedWeaponData;
        private ExecutionWeaponType selectedWeaponType = ExecutionWeaponType.GiantMeatMallet;
        private ButcherPlaceholder targetButcher;

        private Vector3 localPositionOffset;
        private Vector3 localEulerOffset;
        private Vector3 localScale = Vector3.one;

        [MenuItem("Window/FingerMonster/Weapon Offset Tool")]
        public static void ShowWindow()
        {
            WeaponOffsetTool window = GetWindow<WeaponOffsetTool>("Weapon Offset Tool");
            window.minSize = new Vector2(360f, 290f);
            window.Show();
        }

        private void OnEnable()
        {
            targetButcher = Object.FindFirstObjectByType<ButcherPlaceholder>();
            LoadValuesFromSelection();
        }

        private void LoadValuesFromSelection()
        {
            WeaponData source = selectedWeaponData != null
                ? selectedWeaponData
                : ExecutionWeaponData.GetWeaponData(selectedWeaponType);

            if (source != null)
            {
                localPositionOffset = source.localPositionOffset;
                localEulerOffset = source.localEulerOffset;
                localScale = source.localScale == Vector3.zero ? Vector3.one : source.localScale;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Kasap Silahı Yerleşim Kalibrasyonu", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            WeaponData newData = (WeaponData)EditorGUILayout.ObjectField(
                "WeaponData Asset",
                selectedWeaponData,
                typeof(WeaponData),
                false
            );
            if (EditorGUI.EndChangeCheck())
            {
                selectedWeaponData = newData;
                if (selectedWeaponData != null)
                {
                    selectedWeaponType = selectedWeaponData.weaponType;
                }
                LoadValuesFromSelection();
            }

            using (new EditorGUI.DisabledScope(selectedWeaponData != null))
            {
                EditorGUI.BeginChangeCheck();
                ExecutionWeaponType newType = (ExecutionWeaponType)EditorGUILayout.EnumPopup(
                    "Silah Türü",
                    selectedWeaponType
                );
                if (EditorGUI.EndChangeCheck())
                {
                    selectedWeaponType = newType;
                    LoadValuesFromSelection();
                }
            }

            targetButcher = (ButcherPlaceholder)EditorGUILayout.ObjectField(
                "Sahnedeki Kasap",
                targetButcher,
                typeof(ButcherPlaceholder),
                true
            );

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("WeaponSocket Altı Ofsetler", EditorStyles.boldLabel);

            localPositionOffset = EditorGUILayout.Vector3Field("Local Position Offset", localPositionOffset);
            localEulerOffset = EditorGUILayout.Vector3Field("Local Euler Offset", localEulerOffset);
            localScale = EditorGUILayout.Vector3Field("Local Scale", localScale);

            EditorGUILayout.Space(12f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sahnede Canlı Önizle", GUILayout.Height(30f)))
                {
                    ApplyLivePreview();
                }

                if (GUILayout.Button("WeaponData'ya Kaydet", GUILayout.Height(30f)))
                {
                    SaveToWeaponData();
                }
            }

            if (selectedWeaponData == null)
            {
                EditorGUILayout.HelpBox(
                    "Doğrudan kalıcı kaydetmek için bir WeaponData ScriptableObject atayın. " +
                    "Atanmazsa kaydetme sırasında çalışma zamanı varsayılanı veya yeni asset seçeneği kullanılır.",
                    MessageType.Info
                );
            }
        }

        private void ApplyLivePreview()
        {
            if (targetButcher == null)
            {
                targetButcher = Object.FindFirstObjectByType<ButcherPlaceholder>();
            }

            if (targetButcher == null)
            {
                EditorUtility.DisplayDialog(
                    "Kasap Bulunamadı",
                    "Aktif sahnede ButcherPlaceholder bileşenine sahip bir nesne bulunamadı.",
                    "Tamam"
                );
                return;
            }

            WeaponData previewData = selectedWeaponData != null
                ? Instantiate(selectedWeaponData)
                : WeaponData.CreateRuntimeDefault(selectedWeaponType);

            previewData.weaponType = selectedWeaponData != null ? selectedWeaponData.weaponType : selectedWeaponType;
            previewData.localPositionOffset = localPositionOffset;
            previewData.localEulerOffset = localEulerOffset;
            previewData.localScale = localScale == Vector3.zero ? Vector3.one : localScale;

            targetButcher.EquipWeaponData(previewData);
            SceneView.RepaintAll();
        }

        private void SaveToWeaponData()
        {
            WeaponData assetToSave = selectedWeaponData;
            if (assetToSave == null)
            {
                string defaultPath = $"Assets/Settings/WeaponData_{selectedWeaponType}.asset";
                assetToSave = AssetDatabase.LoadAssetAtPath<WeaponData>(defaultPath);
                if (assetToSave == null)
                {
                    assetToSave = WeaponData.CreateRuntimeDefault(selectedWeaponType);
                    if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                    {
                        AssetDatabase.CreateFolder("Assets", "Settings");
                    }
                    AssetDatabase.CreateAsset(assetToSave, defaultPath);
                }
                selectedWeaponData = assetToSave;
            }

            Undo.RecordObject(assetToSave, "Update WeaponData Offsets");
            assetToSave.localPositionOffset = localPositionOffset;
            assetToSave.localEulerOffset = localEulerOffset;
            assetToSave.localScale = localScale == Vector3.zero ? Vector3.one : localScale;

            EditorUtility.SetDirty(assetToSave);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (targetButcher != null)
            {
                targetButcher.EquipWeaponData(assetToSave);
            }

            Debug.Log($"[WeaponOffsetTool] {assetToSave.name} ofsetleri kaydedildi.");
        }
    }
}
