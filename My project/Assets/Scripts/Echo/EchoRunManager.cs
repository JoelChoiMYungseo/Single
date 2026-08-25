using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Echo
{
    /// <summary>런 관리 허브. 값을 폴링해 HUD로 밀어주고(Push), 안전망 리스폰과 재시작을 담당한다.</summary>
    public class EchoRunManager : MonoBehaviour
    {
        [SerializeField] private PlayerRig player;
        [SerializeField] private MicInputListener micListener;
        [SerializeField] private SoundAttractedAI monster;
        [SerializeField] private Transform startPoint;
        [SerializeField] private EchoHUD hud;

        [Tooltip("이 높이 아래로 떨어지면 시작 지점으로 되돌린다.")]
        [SerializeField] private float killPlaneY = -20f;
        [SerializeField] private Key restartKey = Key.R;

        private void Update()
        {
            hud.SetLoudness(micListener.Loudness);
            hud.SetHeardWarning(monster != null && monster.IsAlerted);

            if (player.Position.y < killPlaneY)
            {
                ResetRun();
            }

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                ResetRun();
            }
        }

        public void ResetRun()
        {
            player.Teleport(startPoint.position);
        }
    }
}
