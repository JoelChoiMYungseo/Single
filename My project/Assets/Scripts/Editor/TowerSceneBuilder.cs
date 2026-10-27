#if UNITY_EDITOR
using System.IO;
using Autohand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame.Core;
using VRGame.Survivor;
using VRGame.Tower;

/// <summary>"Crumbling Tower" 씬을 통째로 구성한다. 씬 파일이 없으면 새로 만든다.</summary>
public static class TowerSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME3 tower.unity";
    private const string PrefabFolder = "Assets/Prefabs/Tower";
    private const string MaterialFolder = "Assets/Materials/Tower";
    private const string BlockPrefabPath = PrefabFolder + "/UnstableBlock.prefab";

    /// <summary>Quest 2/3 기준 동시 시뮬레이션 블록 상한. 근거는 로그에 남긴다.</summary>
    private const int SimulatedBlockBudget = 80; // 130층으로 늘어난 만큼 창 크기도 소폭 상향

    [MenuItem("Tools/Tower/Build Crumbling Tower Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene scene = OpenOrCreateScene();

        // 프리팹 생성은 임시 오브젝트를 만들므로 반드시 대상 씬을 연 뒤에 한다.
        GameObject blockPrefab = BuildBlockPrefab();

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Ground", "Pedestal", "PlayerRig", "Player", "XRPlayer", "TowerDirectors");
        BuildEnvironment();

        GameObject rig = BuildPlayerRig(out PlayerRig playerRig, out Transform head, out Transform startPoint);
        TowerHUD hud = BuildHud(head);
        BuildDirectors(rig, playerRig, startPoint, blockPrefab, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Tower] 씬 구성 완료: {ScenePath} — 동시 시뮬레이션 블록 상한 {SimulatedBlockBudget}개. ▶ Play 하세요.");
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


    // ────────────────────────────── Prefab ──────────────────────────────

    /// <summary>블록 하나. 콜라이더는 반드시 실체(비트리거)여야 한다 — 젠가처럼 서로 걸쳐 버티는
    /// 것이 이 게임의 물리 전부이기 때문이다.</summary>
    private static GameObject BuildBlockPrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "UnstableBlock";
        root.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "BlockMat", new Color(0.72f, 0.66f, 0.52f));

        var body = root.AddComponent<Rigidbody>();
        body.mass = 6f;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        root.AddComponent<UnstableBlock>();

        return MakeAssetsUtil.SavePrefab(root, BlockPrefabPath);
    }

    // ────────────────────────────── Environment ──────────────────────────────

    private static void BuildEnvironment()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -1f, 0f);
        ground.transform.localScale = new Vector3(40f, 2f, 40f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.18f, 0.19f, 0.22f));

        // 타워 밑동을 받쳐 주는 단단한 대. 여기가 무너지면 게임이 시작조차 안 된다.
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = "Pedestal";
        pedestal.transform.position = new Vector3(0f, -0.15f, 0f);
        pedestal.transform.localScale = new Vector3(2.4f, 0.3f, 2.4f);
        pedestal.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "PedestalMat", new Color(0.3f, 0.28f, 0.3f));

        if (GameObject.Find("Directional Light") == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(48f, 32f, 0f);
        }
    }

    // ────────────────────────────── Player ──────────────────────────────

    /// <summary>2026-09-17 세션에서 8개 게임 전체를 "진짜" 고릴라 태그 로코모션(Another Axiom MIT
    /// 패키지)으로 통일하기로 결정하면서 물리 손 등반(VRHandController + ConfigurableJoint)을
    /// 걷어냈다. **이 교체로 UnstableBlock의 붕괴 판정(하중 입력)이 죽는다** — 잡기(grab) 자체가
    /// 없어져서 `IVRGrabbable.ApplyLoad`를 호출할 손이 더는 없다. 블록은 물리적 장애물로는 남지만
    /// 더는 무너지지 않는다(사용자가 이 트레이드오프를 명시적으로 승인함).</summary>
    private static GameObject BuildPlayerRig(out PlayerRig playerRig, out Transform head, out Transform startPoint)
    {
        // 리그 스폰 + 고릴라 태그 로코모션 배선을 공용 헬퍼로 처리한다 —
        // Survivor/Magnetic/HoseCleaner/Echo와 동일한 배선이라 AutoHandRigFactory로 승격했다.
        var rig = AutoHandRigFactory.SpawnWithGorillaLocomotion("Tower", "Player", new Vector3(0f, 1.2f, 1.6f),
            out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);
        playerBody.mass = 70f;
        playerBody.constraints = RigidbodyConstraints.FreezeRotation;
        playerBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        head = autoHandPlayer.headCamera.transform;
        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        // 게임 시스템이 참조할 유일한 플레이어 창구. root는 래퍼(rig)가 아니라
        // autoHandPlayer.gameObject다 — 안 그러면 Position/Teleport가 스폰 위치에 얼어붙는다.
        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        return rig;
    }

    // ────────────────────────────── HUD ──────────────────────────────

    private static TowerHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("TowerHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.3f, 1.25f);
        canvasGo.transform.localScale = Vector3.one * 0.0015f;

        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 560f);

        canvasGo.AddComponent<RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없다

        var hud = canvasGo.AddComponent<TowerHUD>();

        Text height = MakeAssetsUtil.CreateText(rect, "HeightLabel", "0.0 m", 60, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -18f));
        Text best = MakeAssetsUtil.CreateText(rect, "BestLabel", "최고 0.0 m", 34, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -92f));
        Text state = MakeAssetsUtil.CreateText(rect, "StateLabel", string.Empty, 54, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 24f));
        Text budget = MakeAssetsUtil.CreateText(rect, "BudgetLabel", "물리 블록 0/0", 26, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-28f, -18f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "heightLabel", height);
        MakeAssetsUtil.SetRef(so, "bestLabel", best);
        MakeAssetsUtil.SetRef(so, "stateLabel", state);
        MakeAssetsUtil.SetRef(so, "budgetLabel", budget);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }


    // ────────────────────────────── Directors ──────────────────────────────

    private static void BuildDirectors(GameObject rig, PlayerRig playerRig, Transform startPoint,
        GameObject blockPrefab, TowerHUD hud)
    {
        var directors = new GameObject("TowerDirectors");

        var tower = directors.AddComponent<TowerManager>();
        var towerSo = new SerializedObject(tower);
        MakeAssetsUtil.SetRef(towerSo, "blockPrefab", blockPrefab.GetComponent<UnstableBlock>());
        MakeAssetsUtil.SetRef(towerSo, "player", rig.transform);
        MakeAssetsUtil.SetInt(towerSo, "maxSimulatedBlocks", SimulatedBlockBudget);
        towerSo.ApplyModifiedPropertiesWithoutUndo();

        var fall = directors.AddComponent<FallManager>();
        var fallSo = new SerializedObject(fall);
        MakeAssetsUtil.SetRef(fallSo, "player", playerRig);
        fallSo.ApplyModifiedPropertiesWithoutUndo();

        var run = directors.AddComponent<TowerRunManager>();
        var runSo = new SerializedObject(run);
        MakeAssetsUtil.SetRef(runSo, "player", playerRig);
        MakeAssetsUtil.SetRef(runSo, "startPoint", startPoint);
        MakeAssetsUtil.SetRef(runSo, "tower", tower);
        MakeAssetsUtil.SetRef(runSo, "fallManager", fall);
        MakeAssetsUtil.SetRef(runSo, "hud", hud);
        MakeAssetsUtil.SetInt(runSo, "simulatedBudgetForDisplay", SimulatedBlockBudget);
        runSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────







}
#endif
