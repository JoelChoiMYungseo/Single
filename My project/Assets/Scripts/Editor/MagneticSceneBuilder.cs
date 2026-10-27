#if UNITY_EDITOR
using Autohand;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame.Core;
using VRGame.Magnetic;

/// <summary>"Magnetic Panic: Polar Crash"를 GAME4 씬에 통째로 구성한다. 자력 놀이터(금속 벽 여러 개
/// + 가벼운 금속 하자드)와 플레이어 리그, HUD, 런 매니저를 코드로 만들고 연결한 뒤 씬을 저장한다.</summary>
public static class MagneticSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME4 Mag.unity";
    private const string PrefabFolder = "Assets/Prefabs/Magnetic";
    private const string MaterialFolder = "Assets/Materials/Magnetic";
    private const string HazardPrefabPath = PrefabFolder + "/MagneticHazard.prefab";

    [MenuItem("Tools/Magnetic/Build GAME4 Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        int metalLayer = MakeAssetsUtil.ResolveLayer("Magnetic", "Metal");

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject hazardPrefab = BuildHazardPrefab(metalLayer);

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Ground", "MetalWalls", "Hazards", "PlayerRig", "Player", "XRPlayer", "MagneticRunManager");
        BuildArena(metalLayer);
        BuildHazards(hazardPrefab);

        GameObject rig = BuildPlayerRig(out PlayerRig playerRig, out Transform head,
            out CrashHandler crashHandler, out Transform startPoint);
        MagneticHUD hud = BuildHud(head);
        BuildRunManager(playerRig, crashHandler, startPoint, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Magnetic] GAME4 씬 구성 완료 ({rig.name}). Metal 레이어={metalLayer}. ▶ Play 하세요.");
    }


    // ────────────────────────────── Prefab ──────────────────────────────

    private static GameObject BuildHazardPrefab(int metalLayer)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = "MagneticHazard";
        root.layer = metalLayer;
        root.transform.localScale = Vector3.one * 0.5f;
        root.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "HazardMat", new Color(0.2f, 0.2f, 0.24f));

        var body = root.AddComponent<Rigidbody>();
        body.mass = 15f; // 플레이어(70kg) 대비 가벼워야 IsLighterThan에서 항상 끌려온다.

        root.AddComponent<MagneticHazard>();

        return MakeAssetsUtil.SavePrefab(root, HazardPrefabPath);
    }

    // ────────────────────────────── Environment ──────────────────────────────

    /// <summary>사방에 큼직한 금속 벽을 세운 개방형 아레나. 손이 어디를 겨눠도 맞을 표면이 있어야
    /// "쏘고 날아가기"가 끊기지 않는다.</summary>
    private static void BuildArena(int metalLayer)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = metalLayer;
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(84f, 1f, 84f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.16f, 0.17f, 0.2f));

        var wallsRoot = new GameObject("MetalWalls").transform;
        Material wallMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "WallMat", new Color(0.42f, 0.45f, 0.52f));
        Material vaultMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "VaultMat", new Color(0.55f, 0.22f, 0.2f));

        // 정사각형으로 4면 + 위아래 대각선 벽 2개를 더 둬서, 수직/사선 이동도 시험할 수 있게 한다.
        // 아레나를 ±28 → ±38로 넓혀서 더 멀리, 더 빠르게 날아갈 여지를 늘렸다.
        BuildWall(wallsRoot, new Vector3(0f, 10f, 38f), Vector3.zero, new Vector3(76f, 20f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 10f, -38f), Vector3.zero, new Vector3(76f, 20f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(38f, 10f, 0f), new Vector3(0f, 90f, 0f), new Vector3(76f, 20f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(-38f, 10f, 0f), new Vector3(0f, 90f, 0f), new Vector3(76f, 20f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 28f, 0f), new Vector3(90f, 0f, 0f), new Vector3(50f, 50f, 1f), wallMaterial, metalLayer);

        // 중앙 상공에 비스듬한 벽들 — 좌우로 튕겨다니며 극을 급하게 바꿔야 하는 구간. 벽을 2개 더 늘려
        // 상공 전체를 지그재그로 가로질러야 하게 만들었다.
        BuildWall(wallsRoot, new Vector3(14f, 14f, 8f), new Vector3(0f, 35f, 0f), new Vector3(16f, 11f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(-14f, 14f, -8f), new Vector3(0f, 35f, 0f), new Vector3(16f, 11f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 20f, 16f), new Vector3(0f, -20f, 0f), new Vector3(16f, 11f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 20f, -16f), new Vector3(0f, -20f, 0f), new Vector3(16f, 11f, 1f), wallMaterial, metalLayer);

        // 위험 구역 — 좁은 4면 방(빨간 벽) 안에 하자드를 잔뜩 몰아넣는다. 고득점/고위험 지역.
        Vector3 vaultCenter = new Vector3(24f, 5f, -24f);
        BuildWall(wallsRoot, vaultCenter + new Vector3(0f, 0f, 6f), Vector3.zero, new Vector3(12f, 10f, 1f), vaultMaterial, metalLayer);
        BuildWall(wallsRoot, vaultCenter + new Vector3(0f, 0f, -6f), Vector3.zero, new Vector3(12f, 10f, 1f), vaultMaterial, metalLayer);
        BuildWall(wallsRoot, vaultCenter + new Vector3(6f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector3(12f, 10f, 1f), vaultMaterial, metalLayer);
        BuildWall(wallsRoot, vaultCenter + new Vector3(-6f, 0f, 0f), new Vector3(0f, 90f, 0f), new Vector3(12f, 10f, 1f), vaultMaterial, metalLayer);

        if (GameObject.Find("Directional Light") == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }
    }

    private static void BuildWall(Transform parent, Vector3 position, Vector3 eulerRotation, Vector3 scale,
        Material material, int metalLayer)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.layer = metalLayer;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.Euler(eulerRotation);
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildHazards(GameObject hazardPrefab)
    {
        var root = new GameObject("Hazards").transform;
        Vector3[] positions =
        {
            new Vector3(6f, 3f, 0f),
            new Vector3(-5f, 5f, 4f),
            new Vector3(3f, 7f, -6f),
            new Vector3(-4f, 2f, -3f),
            new Vector3(16f, 10f, 12f),
            new Vector3(-16f, 8f, -10f),
            new Vector3(0f, 24f, 4f),
            // 위험 구역(빨간 벽 안, 24,-,-24 중심) — 하자드를 몰아넣어 고위험 지대로 만든다.
            new Vector3(22f, 4f, -22f),
            new Vector3(26f, 6f, -26f),
            new Vector3(24f, 3f, -24f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(hazardPrefab);
            instance.name = $"MagneticHazard_{i}";
            instance.transform.SetParent(root, false);
            instance.transform.position = positions[i];
        }
    }

    // ────────────────────────────── Player ──────────────────────────────

    /// <summary>자체 XRNodeTracker 리그 대신 Auto Hand의 XRPlayer 프리팹을 그대로 쓴다 — Survivor/
    /// Samurai와 동일한 전환. 로코모션은 2026-09-17 세션에서 8개 게임 전체를 "진짜" 고릴라 태그
    /// 로코모션(Another Axiom MIT 패키지)으로 통일하기로 결정하면서 MagneticLocomotion(자력 발사)을
    /// 걷어냈다 — 자력 발사/하자드 견인 메커닉은 이 교체로 사실상 죽었다(하자드 오브젝트 자체는
    /// 물리적 장애물로 남지만 더는 자력으로 상호작용하지 않는다).</summary>
    private static GameObject BuildPlayerRig(out PlayerRig playerRig, out Transform head,
        out CrashHandler crashHandler, out Transform startPoint)
    {
        // 리그 스폰 + 고릴라 태그 로코모션 배선을 공용 헬퍼로 처리한다 —
        // Survivor/Tower/HoseCleaner/Echo와 동일한 배선이라 AutoHandRigFactory로 승격했다.
        var rig = AutoHandRigFactory.SpawnWithGorillaLocomotion("Magnetic", "Player", new Vector3(0f, 1.2f, 0f),
            out AutoHandPlayer autoHandPlayer, out Rigidbody body);
        body.mass = 70f; // 사람 무게 — MagneticHazard.IsLighterThan 비교 기준(하자드는 여전히 물리 장애물로 남는다).

        // [RequireComponent(typeof(Rigidbody))] — Rigidbody는 래퍼(rig)가 아니라
        // autoHandPlayer.gameObject에 있으므로 반드시 거기 붙여야 그 Rigidbody를 재사용한다.
        crashHandler = autoHandPlayer.gameObject.AddComponent<CrashHandler>();

        head = autoHandPlayer.headCamera.transform;
        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        // PlayerRig가 참조할 창구. root는 래퍼(rig)가 아니라 autoHandPlayer.gameObject다 —
        // 안 그러면 Position/Teleport가 스폰 위치에 얼어붙는다.
        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", body);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return rig;
    }

    // ────────────────────────────── HUD ──────────────────────────────

    private static MagneticHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("MagneticHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.3f, 1.25f);
        canvasGo.transform.localScale = Vector3.one * 0.0015f;

        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 560f);

        canvasGo.AddComponent<VRGame.Survivor.RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없다

        var hud = canvasGo.AddComponent<MagneticHUD>();

        Text speed = MakeAssetsUtil.CreateText(rect, "SpeedLabel", "0.0 m/s", 54, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -18f));
        Text left = MakeAssetsUtil.CreateText(rect, "LeftPolarityLabel", "N (당김)", 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(28f, -90f));
        Text right = MakeAssetsUtil.CreateText(rect, "RightPolarityLabel", "N (당김)", 30, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-28f, -90f));
        Text crash = MakeAssetsUtil.CreateText(rect, "CrashLabel", "충돌 0", 30, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-28f, -18f));
        Text warning = MakeAssetsUtil.CreateText(rect, "WarningLabel", string.Empty, 44, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 24f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "speedLabel", speed);
        MakeAssetsUtil.SetRef(so, "leftPolarityLabel", left);
        MakeAssetsUtil.SetRef(so, "rightPolarityLabel", right);
        MakeAssetsUtil.SetRef(so, "crashLabel", crash);
        MakeAssetsUtil.SetRef(so, "warningLabel", warning);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }


    // ────────────────────────────── Run manager ──────────────────────────────

    private static void BuildRunManager(PlayerRig playerRig, CrashHandler crashHandler,
        Transform startPoint, MagneticHUD hud)
    {
        var managerGo = new GameObject("MagneticRunManager");
        var run = managerGo.AddComponent<MagneticRunManager>();

        var so = new SerializedObject(run);
        MakeAssetsUtil.SetRef(so, "player", playerRig);
        MakeAssetsUtil.SetRef(so, "crashHandler", crashHandler);
        MakeAssetsUtil.SetRef(so, "startPoint", startPoint);
        MakeAssetsUtil.SetRef(so, "hud", hud);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────






}
#endif
