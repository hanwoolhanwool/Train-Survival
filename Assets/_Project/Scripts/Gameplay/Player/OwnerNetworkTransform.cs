using Unity.Netcode.Components;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 소유자 권위 NetworkTransform — 자기 캐릭터 이동은 소유자 권위 (네트워크 문서 §4).
    /// 수신 측은 NGO 기본 스냅샷 보간으로 표현만 한다.
    /// </summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
