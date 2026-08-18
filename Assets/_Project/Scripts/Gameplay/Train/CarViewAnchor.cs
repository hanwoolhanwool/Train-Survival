using System.Collections.Generic;
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
        private const string AnchorName = "StructureAnchor";

        /// <summary>
        /// 칸 인덱스의 앵커 — 칸 트랜스폼 캐시는 <b>호출부가 들고</b>, 비었을 때만 씬을 훑는다
        /// (증설 예비 슬롯 포함 정적 배치라 편성이 바뀔 때 <see cref="CollectCars"/>로 다시 모으면 충분하다).
        /// 두 뷰 스포너가 같은 조회를 각자 구현하지 않게 하는 지점이다.
        /// </summary>
        public static Transform ResolveForCar(int carIndex, Dictionary<int, Transform> carCache)
        {
            if (carCache == null)
            {
                return null;
            }

            if (carCache.Count == 0)
            {
                CollectCars(carCache);
            }

            return carCache.TryGetValue(carIndex, out Transform car) ? Resolve(car) : null;
        }

        /// <summary>씬의 칸 트랜스폼을 캐시에 다시 모은다 — 편성 변화(증설·재건) 시점에 부른다.</summary>
        public static void CollectCars(Dictionary<int, Transform> carCache)
        {
            if (carCache == null)
            {
                return;
            }

            carCache.Clear();
            foreach (CarView car in Object.FindObjectsByType<CarView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                carCache[car.CarIndex] = car.transform;
            }
        }

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
