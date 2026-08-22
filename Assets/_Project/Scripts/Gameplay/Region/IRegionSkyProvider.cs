using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 하늘의 소유자 (레벨 3차 · 미결 ② B안). <b>슬롯을 채우는 쪽</b>이며,
    /// 걸어 둔 복제본이 무엇인지 알려 준다.
    ///
    /// <para>
    /// 낮/밤 연출(<c>DayCycleVisualController</c>)은 이 값을 <see cref="RenderSettings.skybox"/>와
    /// 비교해 <b>지역이 건 하늘인지 씬 기본값인지</b>를 가른다 — 슬롯이 비어 있지 않다는 것만으로
    /// 판정하면 씬 에셋에 직접 쓰게 되고 그 값이 에디터 세션 내내 남는다.
    /// </para>
    ///
    /// <para>이 서비스가 없으면 낮/밤 연출은 <b>종전 그대로</b> 동작한다 — 회귀 방어선.</para>
    /// </summary>
    public interface IRegionSkyProvider
    {
        /// <summary>지금 슬롯에 걸어 둔 지역 하늘 복제본. 지역에 하늘이 없으면 null.</summary>
        Material CurrentSky { get; }
    }
}
