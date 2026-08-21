#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRGame.AI;
using VRGame.Danmaku;

public static class EnemyPrefabFactory
{
    public const string ZombiePath = "Assets/Prefabs/Enemies/Zombie.prefab";
    public const string BossPath = "Assets/Prefabs/Enemies/ZombieBoss.prefab";

    [MenuItem("Tools/Make Assets/Zombie")]
    public static void CreateZombie() => CreateZombieInternal(force: false);

    [MenuItem("Tools/Make Assets/Zombie (Force Recreate)")]
    public static void ForceRecreateZombie() => CreateZombieInternal(force: true);

    [MenuItem("Tools/Make Assets/Zombie Boss")]
    public static void CreateBoss() => CreateBossInternal(force: false);

    [MenuItem("Tools/Make Assets/Zombie Boss (Force Recreate)")]
    public static void ForceRecreateBoss() => CreateBossInternal(force: true);

    public static void CreateZombieInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {ZombiePath}");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Enemies");

        var root = new GameObject("Zombie");
        root.AddComponent<ZombieAI>(); // [RequireComponent(NavMeshAgent)]로 자동 부착됨

        var collider = root.AddComponent<CapsuleCollider>();
        collider.radius = 0.4f;
        collider.height = 1.8f;
        collider.center = new Vector3(0f, 0.9f, 0f);

        MakeAssetsUtil.AddVisual(root, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f));

        PrefabUtility.SaveAsPrefabAsset(root, ZombiePath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] Zombie 생성 완료: {ZombiePath}");
    }

    public static void CreateBossInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(BossPath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {BossPath}");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Enemies");

        var root = new GameObject("ZombieBoss");
        root.transform.localScale = Vector3.one * 1.4f; // 일반 좀비보다 큼 — 콜라이더도 같이 커짐
        var boss = root.AddComponent<ZombieDanmakuAI>();

        var collider = root.AddComponent<CapsuleCollider>();
        collider.radius = 0.4f;
        collider.height = 1.8f;
        collider.center = new Vector3(0f, 0.9f, 0f);

        MakeAssetsUtil.AddVisual(root, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f));

        var firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(root.transform, false);
        firePoint.transform.localPosition = new Vector3(0f, 0.9f, 0.5f);

        var so = new SerializedObject(boss);
        MakeAssetsUtil.SetRef(so, "firePoint", firePoint.transform);
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] ZombieBoss 생성 완료: {BossPath} (bulletPool/playerTarget은 씬에 배치할 때 연결됩니다)");
    }
}
#endif
