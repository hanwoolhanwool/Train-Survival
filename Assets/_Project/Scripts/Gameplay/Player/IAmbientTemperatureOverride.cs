namespace Game.Gameplay.Player
{
    /// <summary>
    /// 환경 온도를 <b>지역·날씨 대신</b> 정하는 상태 축의 계약 (북극 지역 구현 계획 §8.1 ②).
    ///
    /// <para><see cref="IMoveSpeedModifier"/>와 같은 규약이다 — <see cref="PlayerTemperature"/>는
    /// <b>무엇이 왜 덮어쓰는지 모른다.</b> 물에 잠긴 것이든 화염 앞이든, 구현체가 값을 내면
    /// 체온 계산은 그 값을 쓴다. 새 축은 이 계약을 구현하는 것만으로 붙는다(OCP).</para>
    ///
    /// <para><b>이속 배율과 달리 곱하지 않고 고른다.</b> 온도는 곱셈이 성립하지 않는 물리량이라
    /// (−2 ℃ × 0.7이 무슨 뜻인가) <b>먼저 값을 낸 구현체가 이긴다</b>. 지금은 침수 하나뿐이고,
    /// 둘 이상이 겹칠 때의 우선순위가 필요해지면 그때 정한다.</para>
    /// </summary>
    public interface IAmbientTemperatureOverride
    {
        /// <summary>
        /// 이 축이 지금 환경 온도를 덮어쓰는가.
        /// </summary>
        /// <param name="ambientCelsius">덮어쓸 환경 온도(℃).</param>
        /// <param name="ignoresInsulation">
        /// 장비·요리 단열을 <b>무효</b>로 만드는가. 물에 잠기면 방한복이 젖어 단열이 듣지 않는다 —
        /// 이 플래그가 없으면 방한 풀셋(0.9)이 침수 처벌을 통째로 지운다.
        /// </param>
        bool TryGetAmbient(out float ambientCelsius, out bool ignoresInsulation);
    }
}
