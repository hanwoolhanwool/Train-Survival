using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 오브젝트 밑에 실물을 붙일 때 쓰는 스케일 보정 앵커 (건축 개편 1차 — 계획서 §2.6).
    /// 칸 오브젝트(Car_N)는 비균등 스케일(폭 × 높이 × 길이)의 보정 홀더라, 실물을 칸에 직접 붙이면
    /// 90° 회전 시 부모 축 스케일이 실물의 다른 축에 걸려 길게 늘어난다. 월드 스케일 (1,1,1)·무회전으로
    /// 되돌린 자식을 하나 두고 그 밑에 스폰한다 — 이탈 이동은 부모를 따라 그대로 따라간다.
    /// 건축물 뷰(<see cref="StructureViewSpawner"/>)와 판자 뷰(<see cref="PlankViewSpawner"/>)가 공유한다.
    /// </summary>
    public static class CarViewAnchor
    {
        /// <summary>런타임 전용 앵커 오브젝트 이름 — 이름으로 찾아 재사용하므로 스포너마다 따로 생기지 않는다.</summary>
        public const string AnchorName = "StructureAnchor";

        /// <summary>칸 밑의 스케일 보정 앵커를 돌려준다 — 없으면 만든다. 칸이 null이면 null.</summary>
        public static Transform Resolve(Transform car)
        {
            if (car == null)
            {
                return null;
            }

            Transform anchor = car.Find(AnchorName);
            if (anchor != null)
            {
                return anchor;
            }

            anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(car, worldPositionStays: false);
            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;

            Vector3 lossy = car.lossyScale;
            anchor.localScale = new Vector3(
                lossy.x != 0f ? 1f / lossy.x : 1f,
                lossy.y != 0f ? 1f / lossy.y : 1f,
                lossy.z != 0f ? 1f / lossy.z : 1f);

            return anchor;
        }
    }
}
