using UnityEngine;
using UnityEditor;

namespace FingerMonster.Editor
{
    [InitializeOnLoad]
    public static class ForgeAssetInspector
    {
        static ForgeAssetInspector()
        {
            EditorApplication.delayCall += Inspect;
        }

        [MenuItem("FingerMonster/Inspect Forge Assets")]
        public static void Inspect()
        {
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Walls/wall_01/fi_vil_wall01_01.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Walls/wall_01/fi_vil_wall01_02.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Walls/wall_01/fi_vil_wall01_04.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Floors/fi_vil_floor_stone03.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Floors/fi_vil_floor_wood04.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Pillars/fi_vil_pillar8_02.prefab");
            InspectPrefab("Assets/ThirdParty/3DForge/Fantasy_Interiors/Villages_&_Towns/Prefabs/Forge/fi_vil_forge_workbensh_large1.prefab");
        }

        private static void InspectPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Prefab bulunamadı: {path}");
                return;
            }

            var mf = prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                Debug.Log($"[FORGE_INSPECT] {prefab.name} -> Mesh: {mf.sharedMesh.name}, Size: {b.size}, Center: {b.center}");
            }
            else
            {
                Debug.Log($"[FORGE_INSPECT] {prefab.name} -> No MeshFilter");
            }
        }
    }
}
