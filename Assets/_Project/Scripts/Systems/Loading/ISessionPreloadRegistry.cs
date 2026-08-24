namespace Game.Systems.Loading
{
    /// <summary>
    /// 프리로드 스텝 등록부 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §6.2.
    ///
    /// <para><b>스텝이 자기를 등록한다.</b> 코디네이터가 목록을 들고 있으면 새 스텝이 생길 때마다
    /// 코디네이터를 고쳐야 한다 — 그 방향을 뒤집는 것이 이 인터페이스의 전부다(OCP).</para>
    ///
    /// <para>등록·해제는 짝을 맞춘다. 인게임 씬의 스텝은 씬과 함께 사라지므로
    /// <c>OnDisable</c>에서 반드시 해제해야 한다 — 남아 있으면 다음 로딩에서
    /// <b>파괴된 오브젝트를 돌린다</b>.</para>
    /// </summary>
    public interface ISessionPreloadRegistry
    {
        /// <summary>스텝을 등록한다. 이미 등록돼 있으면 아무것도 하지 않는다.</summary>
        void Register(ISessionPreloadStep step);

        /// <summary>스텝을 해제한다. 등록돼 있지 않으면 아무것도 하지 않는다.</summary>
        void Unregister(ISessionPreloadStep step);
    }
}
