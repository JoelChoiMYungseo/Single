#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using VRGame.Core;
using VRGame.Locomotion;
using VRGame.Magnetic;

/// <summary>"Magnetic Panic: Polar Crash"를 GAME4 씬에 통째로 구성한다. 자력 놀이터(금속 벽 여러 개
/// + 가벼운 금속 하자드)와 플레이어 리그, HUD, 런 매니저를 코드로 만들고 연결한 뒤 씬을 저장한다.</summary>
public static class MagneticSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME4 Mag.unity";
    private const string PrefabFolder = "Assets/Prefabs/Magnetic";
    private const string MaterialFolder = "Assets/Materials/Magnetic";
    private const string HazardPrefabPath = PrefabFolder + "/MagneticHazard.prefab";

    /// <summary>플레이어 리그를 Ignore Raycast(내장 레이어 2)에 두고 자력 조준에서 제외한다 —
    /// 안 그러면 손이 자기 몸이나 다른 손을 겨눠버린다.</summary>
    private const int IgnoreRaycastLayer = 2;

    [MenuItem("Tools/Magnetic/Build GAME4 Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        int metalLayer = ResolveMetalLayer();

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject hazardPrefab = BuildHazardPrefab(metalLayer);

        MakeAssetsUtil.RemoveDefaultCamera();
        BuildArena(metalLayer);
        BuildHazards(hazardPrefab);

        GameObject rig = BuildPlayerRig(metalLayer, out PlayerRig playerRig, out Transform head,
            out MagneticLocomotion locomotion, out CrashHandler crashHandler, out Transform startPoint);
        MagneticHUD hud = BuildHud(head);
        BuildRunManager(playerRig, locomotion, crashHandler, startPoint, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Magnetic] GAME4 씬 구성 완료 ({rig.name}). Metal 레이어={metalLayer}. ▶ Play 하세요.");
    }

    /// <summary>TagManager.asset에 이미 추가해 둔 "Metal" 레이어를 찾는다. 못 찾으면(예: 에디터가
    /// 아직 태그 매니저 변경을 반영하지 못한 경우) Default로 폴백하고 경고를 남긴다.</summary>
    private static int ResolveMetalLayer()
    {
        int layer = LayerMask.NameToLayer("Metal");
        if (layer < 0)
        {
            Debug.LogWarning("[Magnetic] \"Metal\" 레이어를 찾지 못했습니다. ProjectSettings/TagManager.asset을 " +
                              "확인하거나 Unity를 한 번 포커스해 재로드한 뒤 다시 실행하세요. 임시로 Default 레이어를 씁니다.");
            return 0;
        }
        return layer;
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
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.16f, 0.17f, 0.2f));

        var wallsRoot = new GameObject("MetalWalls").transform;
        Material wallMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "WallMat", new Color(0.42f, 0.45f, 0.52f));

        // 정사각형으로 4면 + 위아래 대각선 벽 2개를 더 둬서, 수직/사선 이동도 시험할 수 있게 한다.
        BuildWall(wallsRoot, new Vector3(0f, 8f, 28f), Vector3.zero, new Vector3(56f, 16f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 8f, -28f), Vector3.zero, new Vector3(56f, 16f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(28f, 8f, 0f), new Vector3(0f, 90f, 0f), new Vector3(56f, 16f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(-28f, 8f, 0f), new Vector3(0f, 90f, 0f), new Vector3(56f, 16f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(0f, 22f, 0f), new Vector3(90f, 0f, 0f), new Vector3(40f, 40f, 1f), wallMaterial, metalLayer);

        // 중앙 상공에 비스듬한 벽 두 개 — 좌우로 튕겨다니며 극을 급하게 바꿔야 하는 구간.
        BuildWall(wallsRoot, new Vector3(10f, 12f, 6f), new Vector3(0f, 35f, 0f), new Vector3(14f, 10f, 1f), wallMaterial, metalLayer);
        BuildWall(wallsRoot, new Vector3(-10f, 12f, -6f), new Vector3(0f, 35f, 0f), new Vector3(14f, 10f, 1f), wallMaterial, metalLayer);

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

    private static GameObject BuildPlayerRig(int metalLayer, out PlayerRig playerRig, out Transform head,
        out MagneticLocomotion locomotion, out CrashHandler crashHandler, out Transform startPoint)
    {
        var rig = new GameObject("PlayerRig");
        rig.transform.position = new Vector3(0f, 1.2f, 0f);

        locomotion = rig.AddComponent<MagneticLocomotion>(); // Rigidbody 자동 부착
        crashHandler = rig.AddComponent<CrashHandler>();

        var bodyCollider = rig.AddComponent<CapsuleCollider>();
        bodyCollider.radius = 0.3f;
        bodyCollider.height = 1.7f;
        bodyCollider.center = new Vector3(0f, 0.85f, 0f);

        var body = rig.GetComponent<Rigidbody>();
        body.mass = 70f; // 사람 무게 — MagneticHazard.IsLighterThan 비교 기준.

        head = BuildHead(rig.transform);
        Transform leftHand = BuildHandOrigin(rig.transform, "LeftHandOrigin", new Vector3(-0.25f, 1.1f, 0.25f), XRNode.LeftHand);
        Transform rightHand = BuildHandOrigin(rig.transform, "RightHandOrigin", new Vector3(0.25f, 1.1f, 0.25f), XRNode.RightHand);

        var locomotionSo = new SerializedObject(locomotion);
        MakeAssetsUtil.SetRef(locomotionSo, "leftHandOrigin", leftHand);
        MakeAssetsUtil.SetRef(locomotionSo, "rightHandOrigin", rightHand);
        MakeAssetsUtil.SetRef(locomotionSo, "crashHandler", crashHandler);
        MakeAssetsUtil.SetLayerMask(locomotionSo, "metalMask", 1 << metalLayer);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();

        BuildRigModes(rig, head, leftHand, rightHand, locomotion);

        // 리그 전체(카메라·손 포함)를 자력 조준 대상에서 제외한다. 자식을 다 만든 뒤에 적용해야 한다.
        MakeAssetsUtil.SetLayerRecursively(rig, IgnoreRaycastLayer);

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        // PlayerRig가 참조할 창구. Rigidbody 기반이라 body를 채운다.
        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", rig.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", body);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return rig;
    }

    private static Transform BuildHead(Transform parent)
    {
        var head = new GameObject("Head") { tag = "MainCamera" };
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var camera = head.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 500f;
        head.AddComponent<AudioListener>();

        var tracker = head.AddComponent<XRNodeTracker>();
        MakeAssetsUtil.SetEnum(new SerializedObject(tracker), "node", (int)XRNode.Head);
        return head.transform;
    }

    private static Transform BuildHandOrigin(Transform parent, string name, Vector3 localPosition, XRNode node)
    {
        var hand = new GameObject(name);
        hand.transform.SetParent(parent, false);
        hand.transform.localPosition = localPosition;

        // 조준 방향을 눈으로 확인할 수 있는 작은 구체. 콜라이더는 없다 — 물리 대상이 아니라 표식이다.
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(hand.transform, false);
        visual.transform.localScale = Vector3.one * 0.09f;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "HandMat", new Color(0.85f, 0.8f, 0.3f));

        var tracker = hand.AddComponent<XRNodeTracker>();
        MakeAssetsUtil.SetEnum(new SerializedObject(tracker), "node", (int)node);
        return hand.transform;
    }

    /// <summary>VR 추적과 마우스 임시 조작을 둘 다 붙여두고, 실행 시 헤드셋 유무에 따라
    /// XRModeSwitcher가 알아서 하나만 켠다.</summary>
    private static void BuildRigModes(GameObject rig, Transform head, Transform leftHand, Transform rightHand,
        MagneticLocomotion locomotion)
    {
        var placeholder = rig.AddComponent<PlaceholderMagneticRig>();
        var placeholderSo = new SerializedObject(placeholder);
        MakeAssetsUtil.SetRef(placeholderSo, "head", head);
        MakeAssetsUtil.SetRef(placeholderSo, "locomotion", locomotion);
        MakeAssetsUtil.SetRef(placeholderSo, "leftHandOrigin", leftHand);
        MakeAssetsUtil.SetRef(placeholderSo, "rightHandOrigin", rightHand);
        placeholderSo.ApplyModifiedPropertiesWithoutUndo();

        var trackers = rig.GetComponentsInChildren<XRNodeTracker>(true);

        var switcher = rig.AddComponent<XRModeSwitcher>();
        var switcherSo = new SerializedObject(switcher);
        MakeAssetsUtil.SetObjectArray(switcherSo, "vrOnly", trackers);
        MakeAssetsUtil.SetObjectArray(switcherSo, "placeholderOnly", new Object[] { placeholder });
        switcherSo.ApplyModifiedPropertiesWithoutUndo();
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

    private static void BuildRunManager(PlayerRig playerRig, MagneticLocomotion locomotion, CrashHandler crashHandler,
        Transform startPoint, MagneticHUD hud)
    {
        var managerGo = new GameObject("MagneticRunManager");
        var run = managerGo.AddComponent<MagneticRunManager>();

        var so = new SerializedObject(run);
        MakeAssetsUtil.SetRef(so, "player", playerRig);
        MakeAssetsUtil.SetRef(so, "locomotion", locomotion);
        MakeAssetsUtil.SetRef(so, "crashHandler", crashHandler);
        MakeAssetsUtil.SetRef(so, "startPoint", startPoint);
        MakeAssetsUtil.SetRef(so, "hud", hud);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────






}
#endif
