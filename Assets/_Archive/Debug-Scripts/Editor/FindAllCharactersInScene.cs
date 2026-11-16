using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FingerMonster.Editor
{
    public static class FindAllCharactersInScene
    {
        [MenuItem("FingerMonster/Find All Characters In Scene")]
        public static void FindAll()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            Debug.Log("<color=yellow>=== [FIND ALL CHARACTERS IN GAME SCENE] ===</color>");

            var allSmrs = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"Total SkinnedMeshRenderers found in Game.unity: {allSmrs.Length}");
            foreach (var smr in allSmrs)
            {
                string path = GetHierarchyPath(smr.transform);
                var anim = smr.GetComponentInParent<Animator>();
                string animInfo = (anim != null) 
                    ? $"Animator on '{anim.gameObject.name}' (Controller: {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}, Avatar: {(anim.avatar != null ? anim.avatar.name : "NULL")})" 
                    : "NO ANIMATOR IN PARENTS";
                Debug.Log($"SMR: '{smr.name}' (RootBone: {(smr.rootBone != null ? smr.rootBone.name : "NULL")}) | Path: {path} | {animInfo}");
            }

            var allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"Total Animators found in Game.unity: {allAnimators.Length}");
            foreach (var anim in allAnimators)
            {
                string path = GetHierarchyPath(anim.transform);
                Debug.Log($"Animator: '{anim.name}' | Path: {path} | Controller: {(anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")} | Avatar: {(anim.avatar != null ? anim.avatar.name : "NULL")}");
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "";
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

