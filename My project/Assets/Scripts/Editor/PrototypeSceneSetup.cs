#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRGame.Danmaku;
using VRGame.Game;

/// <summary>SampleScene에 지금까지 만든 프리팹들을 배치하고 씬 전용 참조(스폰 포인트, 플레이어
/// 트랜스폼, BulletPool)를 연결한다. 프리팹은 다른 프리팹을 참조할 수 없어서 이 작업만은 씬
/// 레벨에서 해야 한다. Unity API(EditorSceneManager)로만 다루므로 RULE-02 위반이 아니다.</summary>
public static class PrototypeSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const int SpawnPointCount = 6;
    private const float SpawnRadius = 8f;

    [MenuItem("Tools/Legacy VR Prototype/Build Full Prototype")]
    public static void BuildAll()
    {
        DanmakuPrefabFactory.CreateBulletInternal(force: false);
        DanmakuPrefabFactory.CreatePoolInternal(force: false);
        EnemyPrefabFactory.CreateZombieInternal(force: false);
        EnemyPrefabFactory.CreateBossInternal(force: false);
        PlayerRigFactory.CreateInternal(force: false);
        WaveSpawnManagerFactory.CreateInternal(force: false);
        SetupScene();
    }

    [MenuItem("Tools/Legacy VR Prototype/Setup Scene Only")]
    public static void SetupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject ground = EnsureGround();
        GameObject playerRig = EnsureInstance("PlayerRig", PlayerRigFactory.PrefabPath, new Vector3(0f, 0f, 0f));
        GameObject bulletPoolGo = EnsureInstance("BulletPool", DanmakuPrefabFactory.PoolPath, Vector3.zero);
        GameObject waveMgrGo = EnsureInstance("WaveSpawnManager", WaveSpawnManagerFactory.PrefabPath, Vector3.zero);
        GameObject bossGo = EnsureInstance("ZombieBoss", EnemyPrefabFactory.BossPath, new Vector3(0f, 0f, 10f));

        if (playerRig == null || bulletPoolGo == null || waveMgrGo == null || bossGo == null)
        {
            Debug.LogError("[MakeAssets] 프리팹이 하나 이상 없어 씬 구성을 중단합니다. Tools > Make Assets > Build Full Prototype을 실행하세요.");
            return;
        }

        Transform[] spawnPoints = EnsureSpawnPoints();
        AttachMainCameraToRig(playerRig);

        var waveMgr = waveMgrGo.GetComponent<WaveSpawnManager>();
        var waveMgrSo = new SerializedObject(waveMgr);
        MakeAssetsUtil.SetRef(waveMgrSo, "playerTarget", playerRig.transform);
        WriteTransformArray(waveMgrSo, "spawnPoints", spawnPoints);
        waveMgrSo.ApplyModifiedPropertiesWithoutUndo();

        var boss = bossGo.GetComponent<ZombieDanmakuAI>();
        var bossSo = new SerializedObject(boss);
        MakeAssetsUtil.SetRef(bossSo, "bulletPool", bulletPoolGo.GetComponent<BulletPool>());
        MakeAssetsUtil.SetRef(bossSo, "playerTarget", playerRig.transform);
        bossSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MakeAssets] 씬 구성 완료 및 저장: {ScenePath} (Ground: {ground.name})");
    }

    private static GameObject EnsureGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            return ground;
        }

        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);

        var surface = ground.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();

        return ground;
    }

    private static GameObject EnsureInstance(string name, string prefabPath, Vector3 position)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[MakeAssets] 프리팹을 찾을 수 없습니다: {prefabPath}. 먼저 해당 팩토리를 실행하세요.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.position = position;
        return instance;
    }

    private static Transform[] EnsureSpawnPoints()
    {
        GameObject root = GameObject.Find("SpawnPoints");
        if (root == null)
        {
            root = new GameObject("SpawnPoints");
        }

        var points = new Transform[SpawnPointCount];
        for (int i = 0; i < SpawnPointCount; i++)
        {
            string pointName = $"SpawnPoint_{i}";
            Transform point = root.transform.Find(pointName);
            if (point == null)
            {
                var go = new GameObject(pointName);
                go.transform.SetParent(root.transform);
                point = go.transform;
            }

            float angle = i * (360f / SpawnPointCount) * Mathf.Deg2Rad;
            point.position = new Vector3(Mathf.Sin(angle) * SpawnRadius, 0f, Mathf.Cos(angle) * SpawnRadius);
            points[i] = point;
        }

        return points;
    }

    private static void AttachMainCameraToRig(GameObject playerRig)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || playerRig == null || mainCamera.transform.parent == playerRig.transform)
        {
            return;
        }

        mainCamera.transform.SetParent(playerRig.transform, false);
        mainCamera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        mainCamera.transform.localRotation = Quaternion.identity;
    }

    private static void WriteTransformArray(SerializedObject so, string fieldName, Transform[] values)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
#endif
