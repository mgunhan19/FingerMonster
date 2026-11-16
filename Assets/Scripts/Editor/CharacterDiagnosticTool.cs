using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FingerMonster.Editor
{
    public static class CharacterDiagnosticTool
    {
        [MenuItem("FingerMonster/Diagnostic/Find All Characters In Scene (Edit Mode)")]
        public static void FindAllCharactersEditMode()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            Debug.Log($"<color=yellow>=== [EDIT MODE KARAKTER VE ANIMATOR TARAMASI] Sahne: {scene.name} ===</color>");

            // 1. Sahnedeki tüm Animator bileşenlerini bul
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"<color=cyan>Toplam Animator sayısı: {animators.Length}</color>");
            foreach (var a in animators)
            {
                string ctrlName = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "(NULL - CONTROLLER YOK)";
                string avatarName = a.avatar != null ? a.avatar.name : "(NULL AVATAR)";
                string path = GetHierarchyPath(a.transform);
                Debug.Log($"[ANIMATOR] Yol: '{path}' | Controller: {ctrlName} | Avatar: {avatarName} | Active: {a.gameObject.activeInHierarchy}");
            }

            // 2. Sahnedeki tüm SkinnedMeshRenderer bileşenlerini bul
            var smrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"<color=cyan>Toplam SkinnedMeshRenderer sayısı: {smrs.Length}</color>");
            foreach (var s in smrs)
            {
                string meshName = s.sharedMesh != null ? s.sharedMesh.name : "(NULL MESH)";
                string path = GetHierarchyPath(s.transform);
                Debug.Log($"[SMR] Yol: '{path}' | Mesh: {meshName} | Active: {s.gameObject.activeInHierarchy}");
            }

            // 3. Sahnedeki tüm PlayerVisualAvatar bileşenlerini bul
            var avatars = Object.FindObjectsByType<Player.PlayerVisualAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"<color=cyan>Toplam PlayerVisualAvatar sayısı: {avatars.Length}</color>");
            foreach (var av in avatars)
            {
                string path = GetHierarchyPath(av.transform);
                Debug.Log($"[AVATAR COMPONENT] Yol: '{path}' | Arketip: {av.Archetype} | Active: {av.gameObject.activeInHierarchy}");
            }

            // 4. Sahne kökündeki tüm objeleri tara
            var rootObjects = scene.GetRootGameObjects();
            Debug.Log($"<color=cyan>Sahne Kök Obje Sayısı: {rootObjects.Length}</color>");
            foreach (var root in rootObjects)
            {
                CheckRecursivelyForCharacterNames(root.transform);
            }
        }

        private static void CheckRecursivelyForCharacterNames(Transform t)
        {
            string nameLower = t.name.ToLower();
            if (nameLower.Contains("bodyguard") || nameLower.Contains("feg") || nameLower.Contains("kenan") ||
                nameLower.Contains("feyzi") || nameLower.Contains("mmg") || nameLower.Contains("avatar"))
            {
                Debug.Log($"[NAME MATCH] Bulunan: '{GetHierarchyPath(t)}' | Active: {t.gameObject.activeInHierarchy}");
            }

            for (int i = 0; i < t.childCount; i++)
            {
                CheckRecursivelyForCharacterNames(t.GetChild(i));
            }
        }

        public static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "(null)";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}

