using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 역 소품의 종류 — 무엇이 들었고 어느 등급 집게로 끌 수 있는가
    /// ([기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.3).
    /// </summary>
    public enum StationPropKind : byte
    {
        /// <summary>쓰레기통 — 잡동사니 소량. 흔하고 값싸다. "뒤질 게 많다"는 밀도를 만든다.</summary>
        Bin = 0,

        /// <summary>나무·화물 상자 — 자원 묶음.</summary>
        Crate = 1,

        /// <summary>잠긴 금고·컨테이너 — 무기·부품. <b>3단계 집게</b>가 있어야 끌 수 있다.</summary>
        Safe = 2,

        /// <summary>자판기·사물함 — 음식·소모품.</summary>
        Vending = 3,
    }

    /// <summary>
    /// 역 세그먼트가 제공하는 <b>소품 배치 지점</b> — <see cref="ResourceAnchor"/>와 같은 규약의
    /// 마커다(정적 레지스트리 · 사용 플래그 · 풀 재사용 시 리셋).
    ///
    /// <para><b>왜 자원 앵커를 재사용하지 않는가.</b> 소비자와 주기가 다르다.
    /// 자원 앵커는 <see cref="GroundResourceSpawner"/>가 <b>주행 거리마다</b> 하나씩 골라 심는
    /// 자리이고, 소품은 <b>타일이 켜질 때 그 타일의 것을 전부</b> 심는다. 같은 목록에 섞으면
    /// 역 소품 자리에 평범한 돌무더기가 심기거나 그 반대가 된다.</para>
    ///
    /// <para><b>배치 규격은 자원 앵커와 같다</b> — \|x\| 4~16(집게 1단계 사거리) · \|z\| ≤ 20.
    /// 그래서 검사기는 <see cref="ClearZoneRules.EvaluateAnchor"/>를 그대로 쓴다.</para>
    /// </summary>
    public sealed class StationPropAnchor : MonoBehaviour
    {
        [Tooltip("이 자리에 놓일 소품의 종류.")]
        [SerializeField] private StationPropKind _kind = StationPropKind.Crate;

        [Tooltip("이 자리를 비워 둘 확률 (0이면 항상 놓인다). 역마다 조금씩 달라 보이게 한다.")]
        [SerializeField, Range(0f, 1f)] private float _emptyChance;

        // 활성 앵커 레지스트리 — 타일이 풀에서 켜지고 꺼질 때마다 갱신된다.
        // FindObjectsOfType 대신 이 목록을 쓴다 (매 프레임 씬 전체를 뒤지지 않는다).
        private static readonly List<StationPropAnchor> ActiveAnchors = new List<StationPropAnchor>(32);

        public StationPropKind Kind => _kind;

        public float EmptyChance => _emptyChance;

        /// <summary>이번 활성 구간에서 이미 소품이 놓인 자리인지. 타일이 재사용되면 리셋된다.</summary>
        public bool IsUsed { get; private set; }

        public static IReadOnlyList<StationPropAnchor> Active => ActiveAnchors;

        /// <summary>테스트 전용 — 정적 레지스트리를 비운다 (EditMode TearDown 규약).</summary>
        public static void ClearRegistry()
        {
            ActiveAnchors.Clear();
        }

        public void MarkUsed()
        {
            IsUsed = true;
        }

        /// <summary>
        /// 풀에서 다시 켜질 때의 초기화 — <b>이걸 빠뜨리면 재사용 타일에 소품이 영원히 안 놓인다.</b>
        /// <see cref="OnEnable"/>이 부르지만, EditMode 테스트는 <c>OnEnable</c>을 태울 수 없어
        /// 직접 부를 수 있게 열어 둔다.
        /// </summary>
        public void ResetForReuse()
        {
            IsUsed = false;
        }

        private void OnEnable()
        {
            ResetForReuse();
            ActiveAnchors.Add(this);
        }

        private void OnDisable()
        {
            ActiveAnchors.Remove(this);
        }
    }
}
