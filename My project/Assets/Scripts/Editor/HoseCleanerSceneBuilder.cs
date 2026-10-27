#if UNITY_EDITOR
using System.IO;
using Autohand;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VRGame.Core;
using VRGame.HoseCleaner;

/// <summary>"GAME6 hose" — 고압 호스 반동 로코모션 프로토타입을 통째로 구성한다. 씬 파일이 없으면
/// Tower처럼 새로 만든다. 다른 5개 게임과 달리 플레이어 리그는 직접 안 짓고 Auto Hand가 제공하는
/// XRPlayer 프리팹을 그대로 인스턴스화한다 — 그립·스퀴즈·손 IK를 처음부터 다시 만들 이유가 없다.</summary>
public static class HoseCleanerSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME6 hose.unity";
    private const string PrefabFolder = "Assets/Prefabs/HoseCleaner";
    private const string MaterialFolder = "Assets/Materials/HoseCleaner";
    private const string MonsterPrefabPath = PrefabFolder + "/DirtMonster.prefab";
    private const string HosePrefabPath = PrefabFolder + "/Hose.prefab";

    [MenuItem("Tools/HoseCleaner/Build Game")]
    public static void BuildGame()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene scene = OpenOrCreateScene();

        // 프리팹 생성은 임시 오브젝트를 만들므로 반드시 대상 씬을 연 뒤에 한다.
        GameObject monsterPrefab = BuildMonsterPrefab();
        GameObject hosePrefab = BuildHosePrefab();

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Ground", "NavMeshSurface", "XRPlayer", "HoseCleanerManager", "StartPoint", "HoseStand");

        GameObject arena = BuildArena();
        BakeNavMesh();

        GameObject player = BuildPlayer(out PlayerRig playerRig, out Transform head, out Transform startPoint);
        PlaceHoseNearPlayer(hosePrefab, startPoint);

        HoseCleanerHUD hud = BuildHud(head);
        BuildVisor(head);
        BuildManager(playerRig, hud, monsterPrefab, startPoint);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[HoseCleaner] 씬 구성 완료: {ScenePath} — 호스를 쥐고 트리거(Squeeze)를 당기면 " +
                   "반동으로 밀려납니다. 아래를 겨누면 뜹니다. ▶ Play 하세요.");
    }

    private static Scene OpenOrCreateScene()
    {
        if (File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(created, ScenePath);
        return created;
    }

    // ────────────────────────────── Environment ──────────────────────────────

    private static GameObject BuildArena()
    {
        GameObject arena = GameObject.CreatePrimitive(PrimitiveType.Plane);
        arena.name = "Ground";
        arena.transform.position = Vector3.zero;
        arena.transform.localScale = new Vector3(4f, 1f, 4f); // 기본 10x10 → 40x40
        arena.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.3f, 0.27f, 0.2f));
        return arena;
    }

    /// <summary>이 프로젝트에서 NavMeshAgent를 쓰는 게임은 Echo에 이어 두 번째다. 베이크 방식은
    /// 동일하게 유지한다.</summary>
    private static void BakeNavMesh()
    {
        var surfaceGo = new GameObject("NavMeshSurface");
        var surface = surfaceGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
    }

    // ────────────────────────────── Player (Auto Hand) ──────────────────────────────

    private static GameObject BuildPlayer(out PlayerRig playerRig, out Transform head, out Transform startPoint)
    {
        // 리그 스폰 + 팔스윙 배선(손 참조·환경 마스크·Ignore Raycast)을 공용 헬퍼로 처리한다 —
        // Survivor/Magnetic/Tower/Echo와 동일한 배선이라 AutoHandRigFactory로 승격했다. 2026-09-17
        // 세션에서 ObservedLocomotionDriver(호스 반동 관측)를 걷어내고 이걸로 교체했다 — 호스는
        // 이제 청소 도구로만 남는다(HoseRecoilLocomotion 참고).
        GameObject player = AutoHandRigFactory.SpawnWithGorillaLocomotion("HoseCleaner", "XRPlayer",
            new Vector3(0f, 0.1f, 0f), out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);

        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        // 게임 시스템(RunManager, 나중에 추가될 몬스터 예측 등)이 참조할 유일한 플레이어 창구.
        // root는 래퍼(player)가 아니라 autoHandPlayer.gameObject다 — 실제로 물리/로코모션이 그
        // 오브젝트를 움직이므로, 래퍼를 넣으면 Position/Teleport가 스폰 위치에 얼어붙는다.
        playerRig = player.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", autoHandPlayer.headCamera.transform);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        head = autoHandPlayer.headCamera.transform;

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = player.transform.position;
        startPoint = startGo.transform;

        return player;
    }

    /// <summary>손에 강제로 쥐여주지 않는다 — Auto Hand의 강제 그랩 파라미터를 잘못 짚으면 조용히
    /// 이상한 자세로 붙는 사고가 날 수 있다. 대신 플레이어 바로 앞 낮은 스탠드에 세워 둬서, 헤드셋
    /// 없이도(플레이스홀더 모드에서 손이 안 움직여도) 눈에 보이고, 실제 VR에서는 손을 뻗어 직접
    /// 쥐면 된다 — Auto Hand 데모들이 쓰는 표준적인 "집는" 온보딩과 동일하다.</summary>
    private static void PlaceHoseNearPlayer(GameObject hosePrefab, Transform startPoint)
    {
        var stand = new GameObject("HoseStand");
        stand.transform.position = startPoint.position + startPoint.forward * 0.4f + new Vector3(0f, 0.9f, 0f);

        GameObject hose = (GameObject)PrefabUtility.InstantiatePrefab(hosePrefab);
        hose.transform.SetParent(null);
        hose.transform.position = stand.transform.position;
        Object.DestroyImmediate(stand);
    }

    // ────────────────────────────── Hose Prefab ──────────────────────────────

    private static GameObject BuildHosePrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "Hose";
        root.transform.localScale = new Vector3(0.06f, 0.25f, 0.06f); // 세로로 얇고 긴 원통 — 실린더 기본 높이 2 × 0.25 = 0.5m

        root.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "HoseMat", new Color(0.82f, 0.72f, 0.12f));

        var body = root.AddComponent<Rigidbody>();
        body.mass = 1.2f;

        root.AddComponent<Grabbable>(); // handType 기본값(both)이면 양손 다 쥘 수 있어 그대로 둔다.

        // 분사구 — 원통 로컬 좌표계에서 위쪽 끝(스케일 적용 전 y=1)에 둔다.
        var nozzleGo = new GameObject("Nozzle");
        nozzleGo.transform.SetParent(root.transform, false);
        nozzleGo.transform.localPosition = new Vector3(0f, 1f, 0f);

        var particles = nozzleGo.AddComponent<ParticleSystem>();
        ConfigureWaterJet(particles);

        var hoseScript = root.AddComponent<HoseRecoilLocomotion>();
        var hoseSo = new SerializedObject(hoseScript);
        MakeAssetsUtil.SetRef(hoseSo, "nozzle", nozzleGo.transform);
        MakeAssetsUtil.SetRef(hoseSo, "waterJet", particles);
        hoseSo.ApplyModifiedPropertiesWithoutUndo();

        return MakeAssetsUtil.SavePrefab(root, HosePrefabPath);
    }

    private static void ConfigureWaterJet(ParticleSystem particles)
    {
        var main = particles.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 8f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.7f, 0.85f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.rateOverTime = 0f; // HoseRecoilLocomotion이 분사 중에만 올린다.

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.02f;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ────────────────────────────── Monster Prefab ──────────────────────────────

    private static GameObject BuildMonsterPrefab()
    {
        var root = new GameObject("DirtMonster");

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 1.8f;

        root.AddComponent<DirtMonster>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>()); // 충돌은 NavMeshAgent 이동만으로 충분.
        visual.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "DirtMonsterMat", new Color(0.35f, 0.24f, 0.08f));

        return MakeAssetsUtil.SavePrefab(root, MonsterPrefabPath);
    }

    // ────────────────────────────── HUD & Visor ──────────────────────────────

    private static HoseCleanerHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("HoseCleanerHUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.32f, 1.2f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.0015f;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800f, 300f);

        canvasGo.AddComponent<VRGame.Survivor.RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없다.

        var hud = canvasGo.AddComponent<HoseCleanerHUD>();

        var countLabel = MakeAssetsUtil.CreateText(rect, "CleanedCountLabel", "청소 0 / 0", 40, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -20f));
        var messageLabel = MakeAssetsUtil.CreateText(rect, "MessageLabel", string.Empty, 34, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -90f), color: new Color(0.4f, 0.95f, 0.6f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "cleanedCountLabel", countLabel);
        MakeAssetsUtil.SetRef(so, "messageLabel", messageLabel);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }

    /// <summary>화면 전체를 덮는 단색 오버레이 — Survivor의 월드스페이스 HUD와 같은 자리(Head 밑)에
    /// 두되, HUD보다 카메라에 더 가깝게(z를 더 작게) 둬서 항상 그 위에 그려지게 한다.</summary>
    private static void BuildVisor(Transform head)
    {
        var canvasGo = new GameObject("VisorCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, 0.15f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.001f;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 1000f);

        var imageGo = new GameObject("SplatterImage", typeof(UnityEngine.UI.Image));
        imageGo.transform.SetParent(canvasGo.transform, false);
        var imageRect = imageGo.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        var image = imageGo.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        Color c = new Color(0.28f, 0.2f, 0.08f, 0f);
        image.color = c;

        var visor = canvasGo.AddComponent<VisorBlindness>();
        var so = new SerializedObject(visor);
        MakeAssetsUtil.SetRef(so, "head", head);
        MakeAssetsUtil.SetRef(so, "splatterOverlay", image);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Manager ──────────────────────────────

    private static void BuildManager(PlayerRig playerRig, HoseCleanerHUD hud, GameObject monsterPrefab, Transform startPoint)
    {
        var managerGo = new GameObject("HoseCleanerManager");
        var manager = managerGo.AddComponent<HoseCleanerRunManager>();

        var so = new SerializedObject(manager);
        MakeAssetsUtil.SetRef(so, "player", playerRig);
        MakeAssetsUtil.SetRef(so, "hud", hud);
        MakeAssetsUtil.SetRef(so, "monsterPrefab", monsterPrefab.GetComponent<DirtMonster>());
        MakeAssetsUtil.SetRef(so, "startPoint", startPoint);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
