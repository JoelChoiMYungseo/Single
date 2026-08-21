using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Survivor
{
    /// <summary>Unity 내장 폰트(LegacyRuntime.ttf)에는 한글 글리프가 없어 한글이 네모로 깨진다.
    /// 런타임에 OS 폰트를 동적으로 만들어 하위 Text에 전부 적용한다. 프로토타입용 임시 처리이며,
    /// Quest 등 한글 시스템 폰트가 없는 플랫폼으로 빌드할 때는 한글 폰트 에셋을 임포트해 교체해야 한다.</summary>
    public class RuntimeKoreanFont : MonoBehaviour
    {
        [SerializeField] private string[] candidateFontNames = { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Gulim", "Arial Unicode MS" };

        private void Awake()
        {
            Font font = ResolveFont();
            if (font == null)
            {
                return;
            }

            var texts = GetComponentsInChildren<Text>(includeInactive: true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }

        private Font ResolveFont()
        {
            string[] installed = Font.GetOSInstalledFontNames();

            for (int i = 0; i < candidateFontNames.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (!string.Equals(installed[j], candidateFontNames[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Font font = Font.CreateDynamicFontFromOSFont(installed[j], 32);
                    if (font != null)
                    {
                        return font;
                    }
                }
            }

            Debug.LogWarning("[Survivor] 한글 글리프를 가진 OS 폰트를 찾지 못했습니다. UI 한글이 깨질 수 있습니다.");
            return null;
        }
    }
}
