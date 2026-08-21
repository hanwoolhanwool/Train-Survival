using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 준비 화면의 큰 패널 한 장 — 자기 자리를 스스로 계산하는 것.
    ///
    /// <para><b>연출이 배치를 건드리지 않게 하려고 있는 계약이다.</b> 패널의 최종 위치는
    /// <see cref="ReadyPanelLayout"/>의 실측표가 정하고, 등장 연출(<see cref="ReadyPanelSlide"/>)은
    /// 거기에 <b>더할 값만</b> 넘긴다 — 둘이 같은 필드를 두고 다투면 화면비가 바뀌는 순간
    /// 어느 한쪽이 상대를 덮어쓴다.</para>
    /// </summary>
    public interface IReadyPanel
    {
        /// <summary>등장 연출용 오프셋. 0이면 제자리다.</summary>
        Vector2 IntroOffset { get; set; }
    }
}
