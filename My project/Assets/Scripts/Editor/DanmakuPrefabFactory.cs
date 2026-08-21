#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRGame.Danmaku;

public static class DanmakuPrefabFactory
{
    public const string BulletPath = "Assets/Prefabs/Danmaku/DanmakuBullet.prefab";
    public const string PoolPath = "Assets/Prefabs/Danmaku/BulletPool.prefab";

    [MenuItem("Tools/Make Assets/Danmaku Bullet")]
    public static void CreateBullet() => CreateBulletInternal(force: false);

    [MenuItem("Tools/Make Assets/Danmaku Bullet (Force Recreate)")]
    public static void ForceRecreateBullet() => CreateBulletInternal(force: true);

    [MenuItem("Tools/Make Assets/Bullet Pool")]
    public static void CreatePool() => CreatePoolInternal(force: false);

    [MenuItem("Tools/Make Assets/Bullet Pool (Force Recreate)")]
    public static void ForceRecreatePool() => CreatePoolInternal(force: true);

    public static void CreateBulletInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {BulletPath}");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Danmaku");

        var root = new GameObject("DanmakuBullet");
        root.AddComponent<DanmakuBullet>();
        root.transform.localScale = Vector3.one * 0.08f;
        MakeAssetsUtil.AddVisual(root, PrimitiveType.Sphere);

        PrefabUtility.SaveAsPrefabAsset(root, BulletPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] DanmakuBullet 생성 완료: {BulletPath}");
    }

    public static void CreatePoolInternal(bool force)
    {
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PoolPath) != null)
        {
            Debug.LogWarning($"[MakeAssets] 이미 존재합니다: {PoolPath}");
            return;
        }

        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
        if (bulletPrefab == null)
        {
            Debug.LogError("[MakeAssets] DanmakuBullet 프리팹이 먼저 필요합니다 — Tools > Make Assets > Danmaku Bullet 먼저 실행하세요.");
            return;
        }

        MakeAssetsUtil.EnsureFolder("Assets/Prefabs/Danmaku");

        var root = new GameObject("BulletPool");
        var pool = root.AddComponent<BulletPool>();

        var so = new SerializedObject(pool);
        MakeAssetsUtil.SetRef(so, "bulletPrefab", bulletPrefab.GetComponent<DanmakuBullet>());
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PoolPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"[MakeAssets] BulletPool 생성 완료: {PoolPath}");
    }
}
#endif
