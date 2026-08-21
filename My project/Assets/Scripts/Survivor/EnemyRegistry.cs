using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>살아있는 적 목록을 중앙에서 들고 있는다. 자동조준이 매 발마다 FindObjectsByType으로
    /// 씬 전체를 훑으면 적이 수십~수백일 때 그대로 프레임을 잡아먹기 때문에, 활성/비활성 시점에만
    /// 목록을 갱신하고 조준은 이 목록만 순회한다.</summary>
    public static class EnemyRegistry
    {
        private static readonly List<EnemyTarget> alive = new List<EnemyTarget>(256);

        public static IReadOnlyList<EnemyTarget> Alive => alive;
        public static int AliveCount => alive.Count;

        public static void Register(EnemyTarget target)
        {
            alive.Add(target);
        }

        public static void Unregister(EnemyTarget target)
        {
            alive.Remove(target);
        }

        /// <summary>런을 새로 시작할 때 목록을 비운다. static 상태라 씬을 다시 로드해도 남아 있을 수 있다.</summary>
        public static void Clear()
        {
            alive.Clear();
        }

        /// <summary>사거리 안에서 가장 가까운 적. 없으면 null.</summary>
        public static EnemyTarget FindNearest(Vector3 origin, float maxRange)
        {
            EnemyTarget nearest = null;
            float nearestSqr = maxRange * maxRange;

            for (int i = 0; i < alive.Count; i++)
            {
                EnemyTarget candidate = alive[i];
                if (candidate.IsDead)
                {
                    continue;
                }

                float sqr = (candidate.transform.position - origin).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
