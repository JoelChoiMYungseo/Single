#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRGame.AI;
using VRGame.Game;

public static class WaveSpawnManagerFactory
{
    public const string PrefabPath = "Assets/Prefabs/Game/WaveSpawnManager.prefab";

    [MenuItem("Tools/Make Assets/Wave Spawn Manager")]
    public static void Create() => CreateInternal(force: false);

    [MenuItem("Tools/Make Assets/Wave Spawn Manager (Force Recreate)")]
    public static void ForceRecreate() => CreateInternal(force: true);

    public static void CreateInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {PrefabPath}");
            return;
        }

        var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFactory.ZombiePath);
        if (zombiePrefab == null)
        {
            Debug.LogError("[MakeAssets] Zombie 프리팹이 먼저 필요합니다 — Tools > Make Assets > Zombie 먼저 실행하세요.");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Game");

        var root = new GameObject("WaveSpawnManager");
        var manager = root.AddComponent<WaveSpawnManager>();

        var so = new SerializedObject(manager);
        MakeAssetsUtil.SetRef(so, "zombiePrefab", zombiePrefab.GetComponent<ZombieAI>());
        so.ApplyModifiedPropertiesWithoutUndo();
        // spawnPoints, playerTarget은 씬에 배치할 때 연결됩니다 (프리팹은 씬 오브젝트를 참조할 수 없음).

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] WaveSpawnManager 생성 완료: {PrefabPath}");
    }
}
#endif
