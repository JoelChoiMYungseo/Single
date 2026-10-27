#if UNITY_EDITOR
using Autohand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame.Core;
using VRGame.Locomotion;
using VRGame.Samurai;
using VRGame.Survivor;

/// <summary>수직 대나무 숲(Samu 씬)을 통째로 구성한다. 프리팹·지형·VR 리그·HUD를 코드로 만들고
/// 서로 연결한 뒤 씬을 저장한다.</summary>
public static class SamuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Game2 samu.unity";
    private const string PrefabFolder = "Assets/Prefabs/Samurai";
    private const string MaterialFolder = "Assets/Materials/Samurai";

    private const string BambooPrefabPath = PrefabFolder + "/Bamboo.prefab";
    private const string BambooPiecePrefabPath = PrefabFolder + "/BambooPiece.prefab";
    private const string EnemyPrefabPath = PrefabFolder + "/Oni.prefab";
    private const string EnemyPiecePrefabPath = PrefabFolder + "/OniPiece.prefab";
    private const string KatanaPrefabPath = PrefabFolder + "/Katana.prefab";

    /// <summary>플레이어를 Ignore Raycast(내장 레이어 2)에 두고 모든 물리 쿼리에서 제외한다.
    /// 이렇게 하지 않으면 칼끝 SphereCast가 플레이어 자신의 콜라이더에 걸려 허공에서 발사된다.</summary>
    private const int IgnoreRaycastLayer = 2;
    private static int EnvironmentMask => ~(1 << IgnoreRaycastLayer);

    private const int GroundBambooCount = 72;
    private const int PlatformCount = 42;
    private const float TowerRadius = 16f;
    private const float TowerBaseY = 6f;
    private const float TowerTopY = 150f; // 6구역(정상 140m) 위로 여유를 두고 정점을 배치한다.

    [MenuItem("Tools/Samurai/Build Samu Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        // 작업 중이던 씬이 날아가지 않게 먼저 저장한다. 프리팹 생성이 임시 오브젝트를 만들기 때문에
        // 반드시 Samu 씬을 연 뒤에 만든다 — 안 그러면 이전 씬이 더럽혀진다.
        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject bambooPiecePrefab = BuildBambooPiecePrefab();
        GameObject bambooPrefab = BuildBambooPrefab(bambooPiecePrefab);
        GameObject enemyPiecePrefab = BuildEnemyPiecePrefab();
        GameObject enemyPrefab = BuildEnemyPrefab(enemyPiecePrefab);
        GameObject katanaPrefab = BuildKatanaPrefab();

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Ground", "RisingHazardVisual", "BambooForest", "AscentTower",
            "PlayerRig", "Player", "XRPlayer", "SamuDirectors", "Katana", "KatanaStand");
        BuildGround();
        BuildBambooForest(bambooPrefab);
        Transform hazardVisual = BuildHazardVisual();
        BuildAscentTower(bambooPrefab, enemyPrefab);

        GameObject rig = BuildPlayerRig(out PlayerRig playerRig, out Transform startPoint,
            out SwordGorillaLocomotion locomotion, out Transform head);

        // 카타나를 손에 강제로 쥐여주지 않는다 — HoseCleaner와 동일한 관례. 플레이어 바로 앞
        // 스탠드에 세워 둬서 직접 손을 뻗어 쥐게 한다. 쥐고 있는 동안만 칼끝이 손을 따라 움직이므로
        // 팔스윙(SwordGorillaLocomotion)도 카타나를 쥐고 있을 때만 작동한다.
        KatanaSlicer slicer = PlaceKatanaNearPlayer(katanaPrefab, rig.transform.position, out Transform bladeTip);
        var locomotionSo = new SerializedObject(locomotion);
        MakeAssetsUtil.SetRef(locomotionSo, "katanaTip", bladeTip);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();

        SamuHUD hud = BuildHud(head);
        BuildDirectors(rig, playerRig, startPoint, slicer, hazardVisual, enemyPrefab, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Samurai] Samu 씬 구성 완료 — 구역 6개, 발판 {PlatformCount}개, 대나무 {GroundBambooCount}그루. ▶ Play 하세요.");
    }

    /// <summary>빈 씬에 있던 기본 카메라를 없앤다. 리그에 카메라를 새로 달기 때문에 그대로 두면
    /// MainCamera 태그와 AudioListener가 중복된다.</summary>

    // ────────────────────────────── Prefabs ──────────────────────────────

    private static GameObject BuildBambooPiecePrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "BambooPiece";
        root.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "BambooMat", new Color(0.42f, 0.6f, 0.28f));

        var body = root.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        return MakeAssetsUtil.SavePrefab(root, BambooPiecePrefabPath);
    }

    private static GameObject BuildBambooPrefab(GameObject piecePrefab)
    {
        var root = new GameObject("Bamboo");
        var bamboo = root.AddComponent<BreakableBamboo>();

        var collider = root.AddComponent<CapsuleCollider>();
        collider.direction = 1;

        var stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stalk.name = "Stalk";
        stalk.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(stalk.GetComponent<Collider>()); // 충돌은 루트 캡슐만 담당
        stalk.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "BambooMat", new Color(0.42f, 0.6f, 0.28f));

        var so = new SerializedObject(bamboo);
        MakeAssetsUtil.SetRef(so, "stalk", stalk.transform);
        MakeAssetsUtil.SetRef(so, "piecePrefab", piecePrefab);
        so.ApplyModifiedPropertiesWithoutUndo();

        return MakeAssetsUtil.SavePrefab(root, BambooPrefabPath);
    }

    private static GameObject BuildEnemyPiecePrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "OniPiece";
        root.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "OniMat", new Color(0.62f, 0.14f, 0.2f));

        var body = root.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        return MakeAssetsUtil.SavePrefab(root, EnemyPiecePrefabPath);
    }

    /// <summary>떠다니는 오니. 콜라이더는 트리거로 둔다 — 슬라이서의 OverlapCapsule은 트리거도
    /// 잡아내지만(QueryTriggerInteraction.Collide) 로코모션의 칼끝 캐스트는 트리거를 무시하므로
    /// "베일 수는 있지만 밟고 뛸 수는 없는" 대상이 된다. 물리적으로 길을 막지도 않는다.</summary>
    private static GameObject BuildEnemyPrefab(GameObject piecePrefab)
    {
        var root = new GameObject("Oni");
        var enemy = root.AddComponent<SliceableEnemy>();

        var collider = root.AddComponent<SphereCollider>();
        collider.radius = 0.55f;
        collider.isTrigger = true;

        Material oniMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "OniMat", new Color(0.62f, 0.14f, 0.2f));
        AddChildPrimitive(root.transform, "Body", PrimitiveType.Capsule, Vector3.zero, Vector3.one * 0.55f, oniMaterial);
        AddChildPrimitive(root.transform, "HornL", PrimitiveType.Cube, new Vector3(-0.16f, 0.42f, 0f),
            new Vector3(0.07f, 0.28f, 0.07f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "HornMat", new Color(0.92f, 0.9f, 0.82f)));
        AddChildPrimitive(root.transform, "HornR", PrimitiveType.Cube, new Vector3(0.16f, 0.42f, 0f),
            new Vector3(0.07f, 0.28f, 0.07f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "HornMat", new Color(0.92f, 0.9f, 0.82f)));

        var so = new SerializedObject(enemy);
        MakeAssetsUtil.SetRef(so, "piecePrefab", piecePrefab);
        so.ApplyModifiedPropertiesWithoutUndo();

        return MakeAssetsUtil.SavePrefab(root, EnemyPrefabPath);
    }

    // ────────────────────────────── Level ──────────────────────────────

    private static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(80f, 1f, 80f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.2f, 0.24f, 0.19f));
    }

    /// <summary>아래에서 올라오는 위협의 시각 표현. 충돌 판정은 RisingHazard가 높이 비교로 하므로
    /// 콜라이더는 달지 않는다.</summary>
    private static Transform BuildHazardVisual()
    {
        var hazard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazard.name = "RisingHazardVisual";
        hazard.transform.position = new Vector3(0f, -8f, 0f);
        hazard.transform.localScale = new Vector3(120f, 0.6f, 120f);
        Object.DestroyImmediate(hazard.GetComponent<Collider>());
        hazard.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "HazardMat", new Color(0.55f, 0.12f, 0.16f));
        return hazard.transform;
    }

    /// <summary>바닥에서 위로 솟은 대나무 숲. 벨 수 있는 대상이자 칼을 박고 튀어오를 발판이다.</summary>
    private static void BuildBambooForest(GameObject bambooPrefab)
    {
        var forestRoot = new GameObject("BambooForest").transform;
        var random = new System.Random(20260821); // 씬을 다시 만들어도 같은 배치가 나오게 고정 시드

        for (int i = 0; i < GroundBambooCount; i++)
        {
            float angle = (float)(random.NextDouble() * System.Math.PI * 2.0);
            float distance = 4f + (float)random.NextDouble() * 30f;
            float height = 5f + (float)random.NextDouble() * 10f;

            var position = new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);
            SpawnBamboo(bambooPrefab, forestRoot, $"Bamboo_{i:00}", position,
                (float)random.NextDouble() * 360f, height);
        }
    }

    /// <summary>나선형으로 올라가는 등반 타워. 발판마다 대나무를 몇 그루 세워 두어, 올라가는 도중에도
    /// 벨 것과 칼을 박을 곳이 있게 한다 — 이동과 전투가 같은 동작으로 이어지는 지점이다.</summary>
    private static void BuildAscentTower(GameObject bambooPrefab, GameObject enemyPrefab)
    {
        var towerRoot = new GameObject("AscentTower").transform;
        var random = new System.Random(77281);
        Material platformMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "PlatformMat", new Color(0.35f, 0.3f, 0.26f));

        for (int i = 0; i < PlatformCount; i++)
        {
            float t = (float)i / (PlatformCount - 1);
            float angle = t * Mathf.PI * 6f; // 세 바퀴 돌며 상승
            float y = Mathf.Lerp(TowerBaseY, TowerTopY, t);
            float radius = Mathf.Lerp(TowerRadius, TowerRadius * 0.4f, t);
            float size = Mathf.Lerp(6.5f, 3f, t); // 위로 갈수록 좁아져 난이도가 오른다

            var position = new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);

            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = $"Platform_{i:00}";
            platform.transform.SetParent(towerRoot, false);
            platform.transform.position = position;
            platform.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            platform.transform.localScale = new Vector3(size, 0.4f, size);
            platform.GetComponent<MeshRenderer>().sharedMaterial = platformMaterial;

            SpawnPlatformBamboo(bambooPrefab, towerRoot, i, position, size, random);
        }

        BuildSummit(towerRoot, platformMaterial);
        BuildSummitGuardian(towerRoot, enemyPrefab);
    }

    /// <summary>정점을 지키는 큰 오니. SliceableEnemy는 체력이 없어 한 번 제대로 베면 죽는
    /// 설계라 — "가장 큰 놈도 제대로 맞추면 한 번에 간다"는 통쾌함을 그대로 유지하면서, 크기와
    /// 몸통박치기 위력만 키워 정점의 마지막 위협으로 세운다.</summary>
    private static void BuildSummitGuardian(Transform towerRoot, GameObject enemyPrefab)
    {
        var guardian = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
        guardian.name = "SummitGuardian";
        guardian.transform.SetParent(towerRoot, false);
        guardian.transform.position = new Vector3(0f, TowerTopY + 9f, 0f);
        guardian.transform.localScale = Vector3.one * 2.2f;

        var enemySo = new SerializedObject(guardian.GetComponent<SliceableEnemy>());
        MakeAssetsUtil.SetFloat(enemySo, "knockbackForce", 18f);
        MakeAssetsUtil.SetFloat(enemySo, "knockbackLift", 7f);
        MakeAssetsUtil.SetInt(enemySo, "scoreValue", 400);
        enemySo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SpawnPlatformBamboo(GameObject bambooPrefab, Transform parent, int platformIndex,
        Vector3 platformPosition, float platformSize, System.Random random)
    {
        int count = random.Next(1, 4);
        for (int j = 0; j < count; j++)
        {
            float offsetX = ((float)random.NextDouble() - 0.5f) * platformSize * 0.6f;
            float offsetZ = ((float)random.NextDouble() - 0.5f) * platformSize * 0.6f;
            var position = platformPosition + new Vector3(offsetX, 0.2f, offsetZ);

            SpawnBamboo(bambooPrefab, parent, $"TowerBamboo_{platformIndex:00}_{j}", position,
                (float)random.NextDouble() * 360f, 2.5f + (float)random.NextDouble() * 3.5f);
        }
    }

    private static void BuildSummit(Transform parent, Material material)
    {
        var summit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        summit.name = "Summit";
        summit.transform.SetParent(parent, false);
        summit.transform.position = new Vector3(0f, TowerTopY + 8f, 0f);
        summit.transform.localScale = new Vector3(7f, 0.3f, 7f);
        summit.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void SpawnBamboo(GameObject prefab, Transform parent, string name, Vector3 position,
        float yawDegrees, float height)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        ApplyBambooHeight(instance, height);
    }

    /// <summary>BreakableBamboo.Awake는 플레이 중에만 돌기 때문에, 에디터에서도 제 높이로 보이도록
    /// 같은 계산을 빌드 시점에 한 번 적용해 준다.</summary>
    private static void ApplyBambooHeight(GameObject instance, float height)
    {
        var bamboo = instance.GetComponent<BreakableBamboo>();
        var so = new SerializedObject(bamboo);
        MakeAssetsUtil.SetFloat(so, "height", height);
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedProperty radiusProp = so.FindProperty("radius");
        float radius = radiusProp != null ? radiusProp.floatValue : 0.14f;

        var collider = instance.GetComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.height = height;
        collider.radius = radius;
        collider.center = new Vector3(0f, height * 0.5f, 0f);

        Transform stalk = instance.transform.Find("Stalk");
        if (stalk != null)
        {
            stalk.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            stalk.localPosition = new Vector3(0f, height * 0.5f, 0f);
        }
    }

    // ────────────────────────────── Player ──────────────────────────────

    /// <summary>자체 XRNodeTracker 리그 대신 Auto Hand의 XRPlayer 프리팹을 그대로 쓴다 — Survivor와
    /// 동일한 전환. 카타나는 이제 실제 Grabbable이라(2026-09-17 세션 변경) 여기선 로코모션과 리그만
    /// 배선하고, 카타나 배치·katanaTip 배선은 BuildScene()에서 리그가 만들어진 뒤에 한다.</summary>
    private static GameObject BuildPlayerRig(out PlayerRig playerRig, out Transform startPoint,
        out SwordGorillaLocomotion locomotion, out Transform head)
    {
        // 고릴라 스윙(칼끝으로 지형을 찍고 도약)이 유일한 이동 수단이라 Auto Hand의 기본 스틱
        // 이동은 꺼야 한다(disableBuiltinMovement: true) — 안 끄면 스윙 반동이 그 즉시 지워진다.
        var rig = AutoHandRigFactory.Spawn("Samurai", "Player", new Vector3(0f, 1f, 0f), disableBuiltinMovement: true,
            out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);

        // Rigidbody는 래퍼(rig)가 아니라 autoHandPlayer.gameObject에 있다 — 반드시 거기 붙여야
        // 그 Rigidbody를 재사용한다(안 그러면 래퍼에 새 빈 Rigidbody가 생겨 로코모션이 무효화된다).
        locomotion = autoHandPlayer.gameObject.AddComponent<SwordGorillaLocomotion>();

        head = autoHandPlayer.headCamera.transform;
        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        var locomotionSo = new SerializedObject(locomotion);
        MakeAssetsUtil.SetRef(locomotionSo, "freeHand", leftHand);
        MakeAssetsUtil.SetLayerMask(locomotionSo, "environmentMask", EnvironmentMask);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();

        // 게임 시스템이 참조할 유일한 플레이어 창구. 이 리그엔 ILocomotionDriver 구현이
        // SwordGorillaLocomotion 하나뿐이라 PlayerRig가 알아서 정확히 찾는다. root는 래퍼(rig)가
        // 아니라 autoHandPlayer.gameObject다 — 안 그러면 Position/Teleport가 스폰 위치에 얼어붙는다.
        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        // 칼끝 SphereCast가 자기 몸통 콜라이더를 "지형"으로 잘못 감지하지 않도록 몸통만
        // Ignore Raycast로 뺀다 — 손·헤드는 Auto Hand의 그립 판정을 건드리지 않게 그대로 둔다.
        // 실제 CapsuleCollider는 래퍼(rig)가 아니라 autoHandPlayer.gameObject 쪽에 있다.
        autoHandPlayer.gameObject.layer = IgnoreRaycastLayer;

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        return rig;
    }

    /// <summary>카타나는 칼날 축(base→tip)이 명확해야 절단 평면을 계산할 수 있다.
    /// 칼날에 콜라이더를 달지 않는 이유는 KatanaSlicer가 OverlapCapsule로 직접 판정하기 때문이다 —
    /// 콜라이더를 매 프레임 손 속도로 움직이면 물리 엔진이 계속 재계산해서 비싸다. 2026-09-17
    /// 세션부터 카타나는 손에 고정 부착되지 않고 실제 Grabbable이다 — 손잡이 위치에 별도 그립
    /// 콜라이더(BoxCollider)를 뒀다. HoseCleaner의 호스와 동일한 패턴(프리팹으로 저장 → 플레이어
    /// 근처 스탠드에 배치 → 직접 손을 뻗어 쥠).</summary>
    private static GameObject BuildKatanaPrefab()
    {
        var katana = new GameObject("Katana");

        AddChildPrimitive(katana.transform, "Handle", PrimitiveType.Cube, new Vector3(0f, 0f, -0.12f),
            new Vector3(0.035f, 0.035f, 0.24f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "HandleMat", new Color(0.1f, 0.1f, 0.12f)));
        AddChildPrimitive(katana.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 0f, 0.42f),
            new Vector3(0.012f, 0.05f, 0.84f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "BladeMat", new Color(0.85f, 0.87f, 0.92f)));

        Transform bladeBase = AddMarker(katana.transform, "BladeBase", new Vector3(0f, 0f, 0.02f));
        Transform bladeTip = AddMarker(katana.transform, "BladeTip", new Vector3(0f, 0f, 0.86f));

        var body = katana.AddComponent<Rigidbody>();
        body.mass = 0.8f;

        // 그립 콜라이더 — 손잡이 크기에 맞춰 뒀다. Blade 쪽엔 일부러 콜라이더를 안 둔다(위 요약 참고).
        var grip = katana.AddComponent<BoxCollider>();
        grip.center = new Vector3(0f, 0f, -0.12f);
        grip.size = new Vector3(0.07f, 0.07f, 0.3f);

        katana.AddComponent<Grabbable>(); // handType 기본값(both)이면 양손 다 쥘 수 있어 그대로 둔다.

        var slicer = katana.AddComponent<KatanaSlicer>();
        var slicerSo = new SerializedObject(slicer);
        MakeAssetsUtil.SetRef(slicerSo, "bladeBase", bladeBase);
        MakeAssetsUtil.SetRef(slicerSo, "bladeTip", bladeTip);
        MakeAssetsUtil.SetLayerMask(slicerSo, "sliceableMask", EnvironmentMask);
        slicerSo.ApplyModifiedPropertiesWithoutUndo();

        return MakeAssetsUtil.SavePrefab(katana, KatanaPrefabPath);
    }

    /// <summary>HoseCleaner의 PlaceHoseNearPlayer와 동일한 관례 — 강제로 쥐여주지 않고 손 뻗으면
    /// 닿는 스탠드에 세워둔다.</summary>
    private static KatanaSlicer PlaceKatanaNearPlayer(GameObject katanaPrefab, Vector3 playerPosition, out Transform bladeTip)
    {
        // HoseCleaner는 리그 원점이 거의 바닥(y=0.1)이라 +0.9f를 더해야 손 높이가 나오지만, Samurai
        // 리그 원점은 이미 y=1f(대략 허리~손 높이)라 그대로 더하면 1.9m — 머리 위로 붕 떠서 안 보이고
        // 손이 안 닿는다. 절대 높이 기준으로 맞춘다(약 1.1~1.3m).
        Vector3 standPosition = playerPosition + new Vector3(0.3f, 0.2f, 0.3f);

        GameObject katana = (GameObject)PrefabUtility.InstantiatePrefab(katanaPrefab);
        katana.transform.SetParent(null);
        katana.transform.position = standPosition;
        katana.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // 손잡이가 위로 오게 세워 둔다.

        bladeTip = katana.transform.Find("BladeTip");
        return katana.GetComponent<KatanaSlicer>();
    }

    // ────────────────────────────── HUD ──────────────────────────────

    /// <summary>VR에서도 읽히도록 화면 오버레이가 아니라 머리에 붙은 월드 스페이스 캔버스를 쓴다.</summary>
    private static SamuHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("SamuHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.32f, 1.3f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.0016f;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1100f, 620f);

        canvasGo.AddComponent<RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없어 OS 폰트로 교체

        var hud = canvasGo.AddComponent<SamuHUD>();

        Text zone = MakeAssetsUtil.CreateText(rect, "ZoneLabel", "구역 1/4", 44, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -20f));
        Text height = MakeAssetsUtil.CreateText(rect, "HeightLabel", "고도 0.0 m", 40, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-30f, -20f));
        Text score = MakeAssetsUtil.CreateText(rect, "ScoreLabel", "0 점", 52, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-30f, -80f));
        Text combo = MakeAssetsUtil.CreateText(rect, "ComboLabel", string.Empty, 46, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(30f, -80f));
        Text falls = MakeAssetsUtil.CreateText(rect, "FallLabel", "추락 0", 34, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(30f, 20f));
        Text warning = MakeAssetsUtil.CreateText(rect, "WarningLabel", string.Empty, 46, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 20f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "zoneLabel", zone);
        MakeAssetsUtil.SetRef(so, "heightLabel", height);
        MakeAssetsUtil.SetRef(so, "comboLabel", combo);
        MakeAssetsUtil.SetRef(so, "scoreLabel", score);
        MakeAssetsUtil.SetRef(so, "warningLabel", warning);
        MakeAssetsUtil.SetRef(so, "fallLabel", falls);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }


    // ────────────────────────────── Directors ──────────────────────────────

    private static void BuildDirectors(GameObject rig, PlayerRig playerRig, Transform startPoint,
        KatanaSlicer slicer, Transform hazardVisual, GameObject enemyPrefab, SamuHUD hud)
    {
        var managerGo = new GameObject("SamuDirectors");

        var exploration = managerGo.AddComponent<BambooExploration>();
        var explorationSo = new SerializedObject(exploration);
        MakeAssetsUtil.SetRef(explorationSo, "player", playerRig);
        MakeAssetsUtil.SetRef(explorationSo, "startPoint", startPoint);
        MakeAssetsUtil.SetLayerMask(explorationSo, "groundMask", EnvironmentMask);
        explorationSo.ApplyModifiedPropertiesWithoutUndo();

        var zones = managerGo.AddComponent<AscentZoneManager>();
        var zonesSo = new SerializedObject(zones);
        MakeAssetsUtil.SetRef(zonesSo, "player", rig.transform);
        WriteZones(zonesSo, "zones", AscentZoneManager.CreateDefaultZones());
        zonesSo.ApplyModifiedPropertiesWithoutUndo();

        var hazard = managerGo.AddComponent<RisingHazard>();
        var hazardSo = new SerializedObject(hazard);
        MakeAssetsUtil.SetRef(hazardSo, "player", rig.transform);
        MakeAssetsUtil.SetRef(hazardSo, "exploration", exploration);
        MakeAssetsUtil.SetRef(hazardSo, "zones", zones);
        MakeAssetsUtil.SetRef(hazardSo, "hazardVisual", hazardVisual);
        hazardSo.ApplyModifiedPropertiesWithoutUndo();

        var enemies = managerGo.AddComponent<EnemySpawnDirector>();
        var enemiesSo = new SerializedObject(enemies);
        MakeAssetsUtil.SetRef(enemiesSo, "enemyPrefab", enemyPrefab.GetComponent<SliceableEnemy>());
        MakeAssetsUtil.SetRef(enemiesSo, "player", rig.transform);
        MakeAssetsUtil.SetRef(enemiesSo, "zones", zones);
        enemiesSo.ApplyModifiedPropertiesWithoutUndo();

        var combo = managerGo.AddComponent<ComboTracker>();
        var comboSo = new SerializedObject(combo);
        MakeAssetsUtil.SetRef(comboSo, "slicer", slicer);
        comboSo.ApplyModifiedPropertiesWithoutUndo();

        var run = managerGo.AddComponent<SamuRunManager>();
        var runSo = new SerializedObject(run);
        MakeAssetsUtil.SetRef(runSo, "player", rig.transform);
        MakeAssetsUtil.SetRef(runSo, "exploration", exploration);
        MakeAssetsUtil.SetRef(runSo, "zones", zones);
        MakeAssetsUtil.SetRef(runSo, "hazard", hazard);
        MakeAssetsUtil.SetRef(runSo, "combo", combo);
        MakeAssetsUtil.SetRef(runSo, "enemies", enemies);
        MakeAssetsUtil.SetRef(runSo, "hud", hud);
        runSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>AscentZone은 구조체 배열이라 objectReferenceValue로 넣을 수 없다. 필드별로 직접 쓴다.</summary>
    private static void WriteZones(SerializedObject so, string field, AscentZone[] zones)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Samurai] 필드 없음: {field}");
            return;
        }

        prop.arraySize = zones.Length;
        for (int i = 0; i < zones.Length; i++)
        {
            SerializedProperty element = prop.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Name").stringValue = zones[i].Name;
            element.FindPropertyRelative("StartY").floatValue = zones[i].StartY;
            element.FindPropertyRelative("MaxEnemies").intValue = zones[i].MaxEnemies;
            element.FindPropertyRelative("EnemySpawnInterval").floatValue = zones[i].EnemySpawnInterval;
            element.FindPropertyRelative("EnemySpeedMultiplier").floatValue = zones[i].EnemySpeedMultiplier;
            element.FindPropertyRelative("HazardRiseSpeed").floatValue = zones[i].HazardRiseSpeed;
        }
    }

    // ────────────────────────────── Helpers ──────────────────────────────

    private static GameObject AddChildPrimitive(Transform parent, string name, PrimitiveType shape,
        Vector3 localPosition, Vector3 localScale, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private static Transform AddMarker(Transform parent, string name, Vector3 localPosition)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        return marker.transform;
    }





    /// <summary>enumValueIndex는 표시 순서 인덱스라 값이 불연속인 enum에서 어긋날 수 있다.
    /// 실제 저장값인 intValue로 넣는다.</summary>


}
#endif
