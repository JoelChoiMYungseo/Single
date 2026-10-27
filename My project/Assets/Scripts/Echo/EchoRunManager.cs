using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Echo
{
    /// <summary>런 관리 허브. 값을 폴링해 HUD로 밀어주고(Push), 안전망 리스폰과 재시작, 그리고
    /// "탈출구까지 도달하면 끝난다"는 최소한의 목표 조건을 담당한다. 목표가 없으면 미로를
    /// 무한히 배회하기만 해서 진행감이 없다.</summary>
    public class EchoRunManager : MonoBehaviour
    {
        [SerializeField] private PlayerRig player;
        [SerializeField] private MicInputListener micListener;
        [SerializeField] private SoundAttractedAI[] monsters;
        [SerializeField] private Transform startPoint;
        [SerializeField] private EchoHUD hud;

        [Header("Goal")]
        [SerializeField] private Transform goalPoint;
        [SerializeField] private float goalRadius = 2.2f;

        [Tooltip("이 높이 아래로 떨어지면 시작 지점으로 되돌린다.")]
        [SerializeField] private float killPlaneY = -20f;
        [SerializeField] private Key restartKey = Key.R;

        private bool goalReached;

        private void Update()
        {
            hud.SetLoudness(micListener.Loudness);
            hud.SetHeardWarning(IsAnyMonsterAlerted());

            if (!goalReached && goalPoint != null)
            {
                float distance = Vector3.Distance(player.Position, goalPoint.position);
                if (distance <= goalRadius)
                {
                    goalReached = true;
                    hud.SetGoalReached();
                }
            }

            if (player.Position.y < killPlaneY)
            {
                ResetRun();
            }

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                ResetRun();
            }
        }

        private bool IsAnyMonsterAlerted()
        {
            if (monsters == null)
            {
                return false;
            }

            for (int i = 0; i < monsters.Length; i++)
            {
                if (monsters[i] != null && monsters[i].IsAlerted)
                {
                    return true;
                }
            }
            return false;
        }

        public void ResetRun()
        {
            goalReached = false;
            hud.SetGoalReset();
            player.Teleport(startPoint.position);
        }
    }
}
