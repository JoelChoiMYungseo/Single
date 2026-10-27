using System.Collections.Generic;
using UnityEngine;

namespace VRGame.HoseCleaner
{
    /// <summary>살아있는(청소 안 된) Dirt Monster 목록. 호스가 분사 콘 안의 몬스터를 매 FixedUpdate
    /// 찾아야 하는데, 몬스터 수가 적어도(수십 마리 미만) 매번 FindObjectsByType으로 씬을 훑는 건
    /// Survivor의 EnemyRegistry가 이미 겪은 낭비다 — 같은 해법을 그대로 재사용한다.</summary>
    public static class DirtMonsterRegistry
    {
        private static readonly List<DirtMonster> alive = new List<DirtMonster>(32);

        public static IReadOnlyList<DirtMonster> Alive => alive;
        public static int AliveCount => alive.Count;

        public static void Register(DirtMonster monster)
        {
            alive.Add(monster);
        }

        public static void Unregister(DirtMonster monster)
        {
            alive.Remove(monster);
        }

        public static void Clear()
        {
            alive.Clear();
        }

        /// <summary>사거리 안에서 가장 가까운 몬스터. 없으면 null. Visor 근접 판정과 호스 조준 보조에 쓴다.</summary>
        public static DirtMonster FindNearest(Vector3 origin, float maxRange)
        {
            DirtMonster nearest = null;
            float nearestSqr = maxRange * maxRange;

            for (int i = 0; i < alive.Count; i++)
            {
                DirtMonster candidate = alive[i];
                if (candidate.IsCleaned)
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
