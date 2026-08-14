namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 이동속도에 배율을 거는 상태 축의 계약 (M7 3차 — 결정 ②의 개입점 설계).
    ///
    /// <para>이동 컨트롤러는 <b>"동상"을 모르고 "배율"만 받는다</b> — 개입점을 한 곳에 두되 그 지점이
    /// 특정 축을 알지 않게 하는 경계다. 4차 이후의 다른 축(피로·부상·거치 무기 점유)은 이 계약을
    /// 구현하는 것만으로 같은 지점을 재사용한다 (OCP — 컨트롤러 수정 없이 확장).</para>
    ///
    /// <para>여러 축이 동시에 걸리면 배율은 <b>곱해진다</b>. 각 축은 자기 배율만 책임진다.</para>
    /// </summary>
    public interface IMoveSpeedModifier
    {
        /// <summary>이동속도 배율 (1 = 저하 없음). 음수는 호출부가 0으로 자른다.</summary>
        float MoveSpeedMultiplier { get; }
    }
}
