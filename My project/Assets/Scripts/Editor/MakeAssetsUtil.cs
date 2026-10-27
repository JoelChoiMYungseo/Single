#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>여러 씬 빌더가 공유하는 헬퍼. 5개 게임(Survivor/Samurai/Tower/Magnetic/Echo)의
/// SceneBuilder가 처음엔 각자 이 메서드들을 복사해 갖고 있었다 — 3개를 훌쩍 넘겨 반복됐길래
/// (R4) 여기로 승격했다. 폴더 보장, SerializedObject 필드 연결(null 가드 포함 — FindProperty는
/// 필드명이 틀리면 조용히 null을 반환한다), 프리팹/머티리얼/월드스페이스 텍스트 생성을 담당한다.</summary>
public static class MakeAssetsUtil
{
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string leaf = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }
        AssetDatabase.CreateFolder(parent, leaf);
    }

    public static void SetRef(SerializedObject so, string fieldName, Object value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }
        prop.objectReferenceValue = value;
    }

    public static void SetObjectArray(SerializedObject so, string fieldName, Object[] values)
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

    public static void SetLayerMask(SerializedObject so, string fieldName, int mask)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }
        prop.intValue = mask;
    }

    public static void SetFloat(SerializedObject so, string fieldName, float value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }
        prop.floatValue = value;
    }

    public static void SetInt(SerializedObject so, string fieldName, int value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }
        prop.intValue = value;
    }

    /// <summary>enumValueIndex는 enumNames 배열 내 표시 순서 인덱스라 값이 불연속인 enum(XRNode 등)에서
    /// 어긋난다. 실제 저장값인 intValue로 넣어야 정확하다. Enum 필드는 다른 필드와 달리 항상 즉시
    /// ApplyModifiedPropertiesWithoutUndo까지 호출한다 — 호출부에서 빠뜨리기 쉬운 실수였다.</summary>
    public static void SetEnum(SerializedObject so, string fieldName, int value)
    {
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[MakeAssets] 필드를 찾을 수 없습니다: {fieldName} ({so.targetObject.GetType().Name})");
            return;
        }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>비주얼 자식은 항상 localScale = Vector3.one으로 둔다. 크기를 키우려면 부모(루트)의
    /// transform.localScale을 조정한다 — 그래야 콜라이더도 같은 비율로 같이 커진다.</summary>
    public static GameObject AddVisual(GameObject parent, PrimitiveType shape, Vector3 localPosition = default)
    {
        GameObject visual = GameObject.CreatePrimitive(shape);
        visual.name = "Visual";
        visual.transform.SetParent(parent.transform, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = Vector3.one;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        return visual;
    }

    /// <summary>재실행 시 이전에 만든 루트 오브젝트들을 지우고 새로 짓는다. Samurai/Tower/Magnetic/
    /// Echo 빌더는 "찾아서 재사용"이 아니라 항상 새로 만드는 방식이라, 이게 없으면 빌더를 다시
    /// 돌릴 때마다(난이도 조정 후 재빌드 등) 씬 오브젝트가 두 배로 불어난다.</summary>
    public static void ClearPreviousBuild(params string[] rootNames)
    {
        foreach (string name in rootNames)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }

    /// <summary>빈 씬에 기본으로 들어있는 카메라를 없앤다. 각 게임의 리그가 자기 카메라를 새로
    /// 달기 때문에, 그대로 두면 MainCamera 태그와 AudioListener가 중복된다.</summary>
    public static void RemoveDefaultCamera()
    {
        GameObject existing = GameObject.Find("Main Camera");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    /// <summary>같은 이름의 머티리얼 애셋이 이미 있으면 색만 갱신해 재사용한다 — 빌더를 여러 번
    /// 돌려도 애셋이 중복 생성되지 않는다.</summary>
    public static Material MakeMaterial(string materialFolder, string name, Color color)
    {
        string path = $"{materialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    public static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>TagManager.asset에 이미 추가해 둔 레이어를 이름으로 찾는다. 못 찾으면(예: 에디터가
    /// 아직 태그 매니저 변경을 반영하지 못한 경우) Default(0)로 폴백하고 경고를 남긴다.
    /// Magnetic/Cart/Plunger 3개 SceneBuilder가 각자 복사해 갖고 있던 ResolveXLayer를 승격한 것.</summary>
    public static int ResolveLayer(string gameTag, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[{gameTag}] \"{layerName}\" 레이어를 찾지 못했습니다. ProjectSettings/TagManager.asset을 " +
                              "확인하거나 Unity를 한 번 포커스해 재로드한 뒤 다시 실행하세요. 임시로 Default 레이어를 씁니다.");
            return 0;
        }
        return layer;
    }

    public static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>머리에 붙는 월드스페이스 HUD용 UGUI Text. size/color는 게임마다 다르게 튜닝할 수
    /// 있게 선택 인자로 열어뒀다 — 기본값(560×80, 흰색)은 지금까지 게임들이 공통으로 쓰던 값이다.</summary>
    public static Text CreateText(Transform parent, string name, string content, int fontSize,
        TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2? size = null, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size ?? new Vector2(560f, 80f);

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color ?? Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return text;
    }
}
#endif
