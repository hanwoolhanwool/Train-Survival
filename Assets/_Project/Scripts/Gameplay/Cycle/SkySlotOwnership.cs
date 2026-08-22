namespace Game.Gameplay.Cycle
{
    /// <summary><see cref="UnityEngine.RenderSettings.skybox"/> 슬롯의 현재 주인.</summary>
    public enum SkySlotOwner
    {
        /// <summary>하늘 원본이 없어 아무것도 소유하지 않는다 — 낮/밤 연출이 하늘을 건드리지 않는다.</summary>
        None = 0,

        /// <summary>지역이 건 하늘이다. 프로퍼티만 쓰고 <b>슬롯은 건드리지도 되돌리지도 않는다</b>.</summary>
        Region = 1,

        /// <summary>낮/밤 연출이 만든 복제본이 걸려 있다. 프로퍼티를 쓰고, 놓을 때 슬롯도 되돌린다.</summary>
        DayCycle = 2,

        /// <summary>낮/밤 연출이 복제본을 만들어 슬롯에 걸어야 한다.</summary>
        DayCycleNeedsInstance = 3,
    }

    /// <summary>
    /// 하늘 슬롯의 소유 판정 (레벨 3차 · 미결 ② 해소 — <b>B안: 슬롯은 지역, 프로퍼티는 낮/밤 연출</b>).
    ///
    /// <para>
    /// 두 소유자가 같은 <see cref="UnityEngine.RenderSettings.skybox"/>를 만지므로 경계를 값이 아니라
    /// <b>층</b>으로 나눈다 — <b>어떤 머티리얼을 거는가</b>는 지역이, <b>거기에 무엇을 쓰는가</b>는
    /// 낮/밤 연출이 정한다. fog가 날씨 단독 소유인 것(M8 결정 ② ㉮)과 같은 이유로, 이 경계가
    /// 흐려지면 지역 전환과 국면 전환이 서로의 값을 덮어쓴다.
    /// </para>
    ///
    /// <para>
    /// <b>씬 기본 스카이박스를 지역 하늘로 오해하면 안 된다.</b> 슬롯이 비어 있지 않다는 것만으로
    /// 판정하면 씬 에셋에 직접 쓰게 되고, 그 값이 에디터 세션 내내 남는다 —
    /// 그래서 "지역이 건 것인가"는 슬롯을 보고 추측하지 않고 <b>지역 쪽에 물어서</b> 정한다.
    /// </para>
    /// </summary>
    public static class SkySlotOwnership
    {
        /// <summary>
        /// 지금 슬롯의 주인을 정한다.
        /// </summary>
        /// <param name="slotIsRegionSky">
        /// 슬롯에 걸린 것이 <b>지역이 건 하늘 복제본</b>인가. 씬 기본 스카이박스는 여기 해당하지 않는다.
        /// </param>
        /// <param name="slotIsOwnInstance">슬롯에 걸린 것이 낮/밤 연출이 만든 복제본인가.</param>
        /// <param name="hasOwnSource">낮/밤 연출이 복제할 원본 머티리얼을 갖고 있는가.</param>
        public static SkySlotOwner Resolve(bool slotIsRegionSky, bool slotIsOwnInstance, bool hasOwnSource)
        {
            // 지역이 먼저다 — 지역 하늘이 걸려 있으면 복제본을 새로 만들어 빼앗지 않는다.
            if (slotIsRegionSky)
            {
                return SkySlotOwner.Region;
            }

            if (slotIsOwnInstance)
            {
                return SkySlotOwner.DayCycle;
            }

            return hasOwnSource ? SkySlotOwner.DayCycleNeedsInstance : SkySlotOwner.None;
        }

        /// <summary>이 주인일 때 하늘 프로퍼티를 써도 되는가.</summary>
        public static bool CanWriteProperties(SkySlotOwner owner)
        {
            return owner == SkySlotOwner.Region || owner == SkySlotOwner.DayCycle;
        }

        /// <summary>
        /// 소유를 놓을 때 <b>슬롯까지</b> 되돌려야 하는가 — 내가 건 복제본일 때만이다.
        /// 지역이 건 하늘을 되돌리면 지역이 바뀐 적도 없는데 하늘이 씬 기본값으로 튄다.
        /// </summary>
        public static bool ShouldRestoreSlot(SkySlotOwner owner)
        {
            return owner == SkySlotOwner.DayCycle;
        }
    }
}
