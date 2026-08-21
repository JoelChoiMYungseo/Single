#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>여러 팩토리 스크립트가 공유하는 헬퍼. 폴더 보장, SerializedObject 필드 연결(null 가드
/// 포함 — FindProperty는 필드명이 틀리면 조용히 null을 반환한다), 프리팹 비주얼 자식 생성을 담당한다.</summary>
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
}
#endif
