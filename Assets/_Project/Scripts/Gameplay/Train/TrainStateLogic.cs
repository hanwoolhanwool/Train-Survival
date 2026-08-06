using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>칸 데미지·발생하는 데미지 결과 종류.</summary>
    public enum CarDamageResult
    {
        /// <summary>파괴 불가·이미 파괴·이탈·범위 밖 등으로 무시됨.</summary>
        Ignored,

        /// <summary>체력이 줄었으나 아직 파괴되지 않음.</summary>
        Damaged,

        /// <summary>이 데미지로 파괴됨(체력 0).</summary>
        Destroyed,
    }

    /// <summary>연결부 데미지 결과 종류.</summary>
    public enum CouplingDamageResult
    {
        /// <summary>이미 끊김·양쪽 칸 미연결·범위 밖 등으로 무시됨.</summary>
        Ignored,

        /// <summary>체력이 줄었으나 아직 끊기지 않음.</summary>
        Damaged,

        /// <summary>이 데미지로 끊김 — 후방 칸이 이탈한다.</summary>
        Broken,
    }

    /// <summary>
    /// 열차 편성 상태의 순수 계산 로직 (개발 가이드 §M3·§6.3). 네트워크·씬 의존이 없어 EditMode로 전 경계 검증한다.
    /// 편성 규약: 인덱스 0 = 기관차(선두, 파괴 불가), 값이 클수록 후방. 연결부 파괴 = 그 뒤 칸부터 후방 연쇄 이탈.
    /// </summary>
    public static class TrainStateLogic
    {
        /// <summary>기관차는 항상 선두(인덱스 0). 파괴·이탈 불변식의 기준점.</summary>
        public const int LocomotiveIndex = 0;

        /// <summary>기관차만 파괴 불가 — 그 외 칸은 파괴 가능.</summary>
        public static bool IsDestructible(CarType type)
        {
            return type != CarType.Locomotive;
        }

        /// <summary>편성에 붙어 있고 파괴되지 않은(표현·공격 대상이 되는) 칸인지.</summary>
        public static bool IsCarPresent(CarState car)
        {
            return car.Attached && car.Health > 0f;
        }

        /// <summary>편성 순서(선두→후방)대로 초기 칸 배열을 만든다. 모두 최대 체력·연결 상태로 시작한다.</summary>
        public static CarState[] BuildInitialCars(CarType[] order, Func<CarType, float> maxHealthFor)
        {
            if (order == null || maxHealthFor == null)
            {
                return Array.Empty<CarState>();
            }

            var cars = new CarState[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                float max = maxHealthFor(order[i]);
                cars[i] = new CarState
                {
                    Type = order[i],
                    Health = max,
                    MaxHealth = max,
                    Attached = true,
                };
            }

            return cars;
        }

        /// <summary>
        /// 칸 하나에 데미지를 적용한다. 파괴 불가·이미 파괴·이탈·범위 밖이면 무시한다.
        /// 배열 원소를 직접 갱신하며(구조체 복사 주의), 결과 종류를 돌려준다.
        /// </summary>
        public static CarDamageResult ApplyDamage(CarState[] cars, int index, float amount)
        {
            if (cars == null || index < 0 || index >= cars.Length || amount <= 0f)
            {
                return CarDamageResult.Ignored;
            }

            CarState car = cars[index];
            if (!car.Attached || car.Health <= 0f || !IsDestructible(car.Type))
            {
                return CarDamageResult.Ignored;
            }

            car.Health = Mathf.Max(0f, car.Health - amount);
            cars[index] = car;

            return car.Health <= 0f ? CarDamageResult.Destroyed : CarDamageResult.Damaged;
        }

        /// <summary>
        /// <paramref name="startIndex"/>부터 후방의 '연결된' 칸을 모두 이탈 처리한다(후방 연쇄 이탈).
        /// 기관차(인덱스 0)는 절대 이탈하지 않으므로 시작점을 1 이상으로 보정한다.
        /// 이번에 새로 이탈한 인덱스를 오름차순으로 돌려준다(권위 이벤트 페이로드용).
        /// </summary>
        public static int[] DetachFrom(CarState[] cars, int startIndex)
        {
            if (cars == null)
            {
                return Array.Empty<int>();
            }

            int from = Mathf.Max(LocomotiveIndex + 1, startIndex);
            List<int> detached = null;
            for (int i = from; i < cars.Length; i++)
            {
                if (!cars[i].Attached)
                {
                    continue;
                }

                CarState car = cars[i];
                car.Attached = false;
                cars[i] = car;

                (detached ?? (detached = new List<int>())).Add(i);
            }

            return detached != null ? detached.ToArray() : Array.Empty<int>();
        }

        /// <summary>
        /// 칸 <paramref name="index"/>를 파괴 상태(체력 0·이탈)로 만들고, 그 뒤 칸부터 후방 연쇄 이탈시킨다.
        /// 반환 = 파괴 칸을 제외하고 이번에 새로 이탈한 후방 칸 인덱스(오름차순).
        /// </summary>
        public static int[] DestroyAndDetach(CarState[] cars, int index)
        {
            if (cars == null || index < LocomotiveIndex + 1 || index >= cars.Length)
            {
                return Array.Empty<int>();
            }

            CarState car = cars[index];
            car.Health = 0f;
            car.Attached = false;
            cars[index] = car;

            return DetachFrom(cars, index + 1);
        }

        // ── 연결부 (기획서 §9 — 밤 방어전 핵심 방어 목표) ──────────────────

        /// <summary>칸 수에 맞춰 초기 연결부 배열을 만든다(연결부 수 = 칸 수 - 1). 모두 최대 체력·연결 상태.</summary>
        public static CouplingState[] BuildInitialCouplings(int carCount, float maxHealth)
        {
            int count = carCount > 1 ? carCount - 1 : 0;
            var couplings = new CouplingState[count];
            for (int i = 0; i < count; i++)
            {
                couplings[i] = new CouplingState
                {
                    Health = maxHealth,
                    MaxHealth = maxHealth,
                    Broken = false,
                };
            }

            return couplings;
        }

        /// <summary>
        /// 연결부 <paramref name="index"/>가 지금 공격 가능한지 — 잇는 두 칸(index, index+1)이 모두 편성에 살아 붙어 있어야 한다.
        /// 이미 끊긴 연결부, 이탈/파괴된 칸 사이의 연결부는 공격 대상이 아니다.
        /// </summary>
        public static bool IsCouplingLive(CouplingState[] couplings, CarState[] cars, int index)
        {
            if (couplings == null || cars == null || index < 0 || index >= couplings.Length)
            {
                return false;
            }

            if (couplings[index].Broken)
            {
                return false;
            }

            return index + 1 < cars.Length && IsCarPresent(cars[index]) && IsCarPresent(cars[index + 1]);
        }

        /// <summary>
        /// 연결부 <paramref name="index"/>가 지금 공격(타겟팅) 가능한지 — 살아 있는 연결부 중 가장 후미여야 한다.
        /// 연결부는 열차 끝에서부터 순차적으로만 끊을 수 있다: 뒤쪽에 살아 있는 연결부가 하나라도 남아 있으면
        /// 앞쪽 연결부(예: 기관차-다음 칸)는 표적이 아니다.
        /// </summary>
        public static bool IsCouplingTargetable(CouplingState[] couplings, CarState[] cars, int index)
        {
            if (!IsCouplingLive(couplings, cars, index))
            {
                return false;
            }

            for (int i = index + 1; i < couplings.Length; i++)
            {
                if (IsCouplingLive(couplings, cars, i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 연결부 하나에 데미지를 적용한다. 이미 끊김·양쪽 칸 미연결·범위 밖이거나,
        /// 뒤쪽에 살아 있는 연결부가 남아 있으면(후미 순차 파괴 규칙) 무시한다.
        /// 끊기면 <see cref="CouplingDamageResult.Broken"/>을 돌려주며, 후방 칸 이탈은 호출부가 <see cref="DetachFrom"/>로 처리한다.
        /// </summary>
        public static CouplingDamageResult ApplyCouplingDamage(
            CouplingState[] couplings, CarState[] cars, int index, float amount)
        {
            if (amount <= 0f || !IsCouplingTargetable(couplings, cars, index))
            {
                return CouplingDamageResult.Ignored;
            }

            CouplingState coupling = couplings[index];
            coupling.Health = Mathf.Max(0f, coupling.Health - amount);
            if (coupling.Health <= 0f)
            {
                coupling.Broken = true;
                couplings[index] = coupling;
                return CouplingDamageResult.Broken;
            }

            couplings[index] = coupling;
            return CouplingDamageResult.Damaged;
        }

        /// <summary>
        /// 연결부 하나를 끊긴 것으로 표시한다(칸 파괴로 인접 연결이 함께 끊길 때). 범위 밖·이미 끊김이면 false.
        /// </summary>
        public static bool BreakCoupling(CouplingState[] couplings, int index)
        {
            if (couplings == null || index < 0 || index >= couplings.Length || couplings[index].Broken)
            {
                return false;
            }

            CouplingState coupling = couplings[index];
            coupling.Broken = true;
            couplings[index] = coupling;
            return true;
        }

        // ── 칸 위 건축물 (기획서 §9 — 칸 위의 각 건축물도 개별 파괴 가능) ──────────────────

        /// <summary>건축물이 존재하고 파괴되지 않았는지(표현·공격·수리 대상).</summary>
        public static bool IsStructureAlive(StructureState structure)
        {
            return structure.Present && structure.Health > 0f;
        }

        /// <summary>초기 건축물 배열을 만든다(인덱스 = 칸 인덱스 1:1) — 전부 빈 슬롯. 건축물은 설치로만 생긴다.</summary>
        public static StructureState[] BuildInitialStructures(int carCount)
        {
            return carCount > 0 ? new StructureState[carCount] : Array.Empty<StructureState>();
        }

        /// <summary>
        /// 이 칸에 건축물을 설치할 수 있는지 — 칸이 편성에 살아 붙어 있고(기관차 제외),
        /// 살아 있는 건축물이 아직 없어야 한다. 파괴된 건축물 자리에는 새로 지을 수 있다.
        /// </summary>
        public static bool CanBuildStructureAt(StructureState[] structures, CarState[] cars, int index)
        {
            if (structures == null || cars == null
                || index < 0 || index >= structures.Length || index >= cars.Length)
            {
                return false;
            }

            return IsCarPresent(cars[index]) && IsDestructible(cars[index].Type)
                && !IsStructureAlive(structures[index]);
        }

        /// <summary>칸 위에 건축물을 설치한다 — 지정 종류·최대 체력으로 시작. 설치 불가 자리면 false.</summary>
        public static bool BuildStructure(
            StructureState[] structures, CarState[] cars, int index, StructureKind kind, float maxHealth)
        {
            if (!CanBuildStructureAt(structures, cars, index))
            {
                return false;
            }

            structures[index] = new StructureState
            {
                Present = true,
                Kind = kind,
                Health = maxHealth,
                MaxHealth = maxHealth,
            };
            return true;
        }

        /// <summary>
        /// 건축물 하나에 데미지를 적용한다. 칸이 편성에 살아 붙어 있고 건축물이 살아 있을 때만 유효하다
        /// (이탈·파괴된 칸 위의 건축물은 칸과 운명을 같이하므로 별도 표적이 아니다).
        /// </summary>
        public static CarDamageResult ApplyStructureDamage(
            StructureState[] structures, CarState[] cars, int index, float amount)
        {
            if (structures == null || cars == null || amount <= 0f
                || index < 0 || index >= structures.Length || index >= cars.Length)
            {
                return CarDamageResult.Ignored;
            }

            if (!IsCarPresent(cars[index]) || !IsStructureAlive(structures[index]))
            {
                return CarDamageResult.Ignored;
            }

            StructureState structure = structures[index];
            structure.Health = Mathf.Max(0f, structure.Health - amount);
            structures[index] = structure;

            return structure.Health <= 0f ? CarDamageResult.Destroyed : CarDamageResult.Damaged;
        }

        // ── 수리 (기획서 §9 — 수리 망치. 파괴·이탈된 부위의 복구(재결합)는 미결이라 불가) ──────────────────

        /// <summary>
        /// 칸을 수리한다 — 편성에 살아 붙어 있고 만피가 아닐 때만. 기관차는 파괴 불가(체력 무한)라 수리 대상이 아니다.
        /// </summary>
        public static bool RepairCar(CarState[] cars, int index, float amount)
        {
            if (cars == null || index < 0 || index >= cars.Length || amount <= 0f)
            {
                return false;
            }

            CarState car = cars[index];
            if (!IsCarPresent(car) || !IsDestructible(car.Type) || car.Health >= car.MaxHealth)
            {
                return false;
            }

            car.Health = Mathf.Min(car.MaxHealth, car.Health + amount);
            cars[index] = car;
            return true;
        }

        /// <summary>연결부를 수리한다 — 살아 있는(잇는 두 칸이 건재한) 연결부만. 끊긴 연결부는 되살릴 수 없다.</summary>
        public static bool RepairCoupling(CouplingState[] couplings, CarState[] cars, int index, float amount)
        {
            if (amount <= 0f || !IsCouplingLive(couplings, cars, index))
            {
                return false;
            }

            CouplingState coupling = couplings[index];
            if (coupling.Health >= coupling.MaxHealth)
            {
                return false;
            }

            coupling.Health = Mathf.Min(coupling.MaxHealth, coupling.Health + amount);
            couplings[index] = coupling;
            return true;
        }

        /// <summary>건축물을 수리한다 — 칸이 살아 붙어 있고 건축물이 살아 있을 때만. 파괴된 건축물 재건은 M5(건설) 범위.</summary>
        public static bool RepairStructure(StructureState[] structures, CarState[] cars, int index, float amount)
        {
            if (structures == null || cars == null || amount <= 0f
                || index < 0 || index >= structures.Length || index >= cars.Length)
            {
                return false;
            }

            if (!IsCarPresent(cars[index]) || !IsStructureAlive(structures[index]))
            {
                return false;
            }

            StructureState structure = structures[index];
            if (structure.Health >= structure.MaxHealth)
            {
                return false;
            }

            structure.Health = Mathf.Min(structure.MaxHealth, structure.Health + amount);
            structures[index] = structure;
            return true;
        }

        // ── 칸 건설 (개발 가이드 §M3 — 칸 증설/연결, 기획서 §7.1) ──────────────────

        /// <summary>
        /// 지금 칸을 지을 슬롯을 찾는다 — 선두부터 훑어 첫 '비어 있는' 슬롯(파괴됐거나 소실 거리 밖으로
        /// 사라진 칸)을 재건 대상으로 돌려주고, 빈 슬롯이 없으면 상한 미만일 때 후미 새 슬롯(= 칸 수)을 돌려준다.
        /// 첫 빈 슬롯의 칸이 아직 가까이서 이탈 중(회수 다툼 중)이면 -1 — 그 자리에 겹쳐 지을 수 없고,
        /// 연결부 규약(c = 칸 c ↔ c+1) 때문에 그 슬롯을 건너뛰고 뒤에 지을 수도 없다.
        /// </summary>
        public static int FindBuildSlot(CarState[] cars, float[] ejectOffsets, float lostDistance, int maxCarCount)
        {
            if (cars == null || cars.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < cars.Length; i++)
            {
                if (IsCarPresent(cars[i]))
                {
                    continue;
                }

                bool destroyed = cars[i].Health <= 0f;
                bool lost = ejectOffsets != null && i < ejectOffsets.Length && ejectOffsets[i] >= lostDistance;
                return destroyed || lost ? i : -1;
            }

            return cars.Length < maxCarCount ? cars.Length : -1;
        }

        /// <summary>슬롯 하나를 새 확장 칸으로 재건한다 — 칸을 최대 체력·연결 상태로 되살리고 앞 연결부를 복구한다.</summary>
        public static void RebuildSlot(CarState[] cars, CouplingState[] couplings, StructureState[] structures,
            int index, float carMaxHealth, float couplingMaxHealth)
        {
            cars[index] = new CarState
            {
                Type = CarType.Standard,
                Health = carMaxHealth,
                MaxHealth = carMaxHealth,
                Attached = true,
            };

            // 앞 연결부(index-1 = 칸 index-1 ↔ index)를 복구한다 — 재건은 첫 빈 슬롯에서만 일어나므로 앞 칸은 건재하다.
            if (couplings != null && index - 1 >= 0 && index - 1 < couplings.Length)
            {
                couplings[index - 1] = new CouplingState
                {
                    Health = couplingMaxHealth,
                    MaxHealth = couplingMaxHealth,
                    Broken = false,
                };
            }

            // 옛 건축물 잔해는 함께 사라진다 — 새 칸은 빈 슬롯으로 시작한다.
            if (structures != null && index < structures.Length)
            {
                structures[index] = default;
            }
        }

        // ── 이탈 칸 재결합 (손잡이-이탈저항 스펙 §4.1 — 끌어온 칸을 편성에 다시 붙인다) ──────────────────

        /// <summary>재결합으로 되살아난 연결부가 회복하는 체력 비율 — 급히 다시 이은 연결이라 절반만 돌아온다.</summary>
        public const float RecoupledCouplingHealthRatio = 0.5f;

        /// <summary>
        /// 이탈 칸을 편성에 다시 붙일 수 있는 구조인지 — 기관차가 아니고, 이탈 중이며 살아 있고,
        /// 앞 칸(index-1)이 편성에 살아 붙어 있어야 한다(앞에서부터 순차 재결합 — 편성 중간에 뜬 칸을 만들지 않는다).
        /// 슬롯 도달(offset == 0)·자원·거리는 상태 밖의 조건이라 호출부(호스트)가 따로 검증한다.
        /// </summary>
        public static bool CanRecouple(CarState[] cars, int index)
        {
            if (cars == null || index < LocomotiveIndex + 1 || index >= cars.Length)
            {
                return false;
            }

            CarState car = cars[index];
            if (car.Attached || car.Health <= 0f)
            {
                return false;
            }

            return IsCarPresent(cars[index - 1]);
        }

        /// <summary>
        /// 이탈 칸을 편성에 다시 붙인다 — 칸 체력은 그대로 두고(이탈 중에는 손상되지 않는다)
        /// 뚫렸던 앞 연결부만 절반 체력으로 되살린다. 칸 위 건축물은 이탈 중에도 데이터가 보존되므로 함께 복귀한다
        /// (건축물을 없애는 <see cref="RebuildSlot"/>과 달리, 이것이 회수의 실질 이득이다).
        /// </summary>
        public static bool Recouple(CarState[] cars, CouplingState[] couplings, int index, float couplingMaxHealth)
        {
            if (!CanRecouple(cars, index))
            {
                return false;
            }

            CarState car = cars[index];
            car.Attached = true;
            cars[index] = car;

            // 앞 연결부(index-1 = 칸 index-1 ↔ index) 중 '실제로 끊겼던' 자리만 절반 체력으로 다시 잇는다 —
            // 이어서 좌클릭 수리로 마저 채우는 행동이 자연스럽게 이어지고, 다시 뚫릴 위험이 긴장감으로 남는다.
            // 연쇄 이탈로 함께 떨어졌을 뿐 뚫리지 않은 연결부는 그동안 손상되지 않았으므로 체력을 그대로 둔다.
            int front = index - 1;
            if (couplings != null && front >= 0 && front < couplings.Length && couplings[front].Broken)
            {
                couplings[front] = new CouplingState
                {
                    Health = couplingMaxHealth * RecoupledCouplingHealthRatio,
                    MaxHealth = couplingMaxHealth,
                    Broken = false,
                };
            }

            return true;
        }
    }
}
