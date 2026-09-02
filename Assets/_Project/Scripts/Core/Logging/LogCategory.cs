using System;

namespace Game.Core.Logging
{
    /// <summary>
    /// 로그 카테고리. <see cref="GameLog"/>의 런타임 on/off 단위이자 출력 접두어다.
    /// 비트 플래그이므로 <c>LogCategory.Net | LogCategory.Steam</c>처럼 묶어서 켜고 끌 수 있다.
    /// 스크립트 이름은 <c>[CallerFilePath]</c>로 자동으로 붙으므로(<c>[Net/NgoNetworkSessionService]</c>),
    /// 카테고리는 "어느 계통인가"만 고르면 된다.
    /// 새 카테고리는 남는 비트에 추가한다 — <see cref="All"/>은 전 비트라 자동으로 포함된다.
    /// </summary>
    [Flags]
    public enum LogCategory
    {
        None = 0,

        /// <summary>EventBus·PoolManager·ServiceLocator 등 Core 인프라.</summary>
        Core = 1 << 0,

        /// <summary>NGO 세션·전송·프리팹 등록·로비 등 네트워크 계통.</summary>
        Net = 1 << 1,

        /// <summary>Steamworks 연동 (로비·업적·아이덴티티·전송).</summary>
        Steam = 1 << 2,

        /// <summary>플레이어 컨트롤러·시점.</summary>
        Player = 1 << 3,

        /// <summary>총기·근접 등 전투 판정.</summary>
        Combat = 1 << 4,

        /// <summary>집게(하푼) — 발사·훅·견인·계측.</summary>
        Harpoon = 1 << 5,

        /// <summary>몬스터·보스·웨이브·스탬피드.</summary>
        Monsters = 1 << 6,

        /// <summary>열차 편성·건축·창고·높이.</summary>
        Train = 1 << 7,

        /// <summary>지형 스트리밍·지상 자원·지역.</summary>
        World = 1 << 8,

        /// <summary>낮/밤 사이클·날씨·연출.</summary>
        Cycle = 1 << 9,

        /// <summary>세션 수명주기·게임오버.</summary>
        Session = 1 << 10,

        /// <summary>메타 진행도 저장·불러오기.</summary>
        Meta = 1 << 11,

        /// <summary>HUD·메뉴 등 화면 표시.</summary>
        Ui = 1 << 12,

        /// <summary>QA 핫키·검증 도구가 찍는 로그.</summary>
        Qa = 1 << 13,

        /// <summary>ViewLab 등 에디터 전용 진단 툴.</summary>
        ViewLab = 1 << 14,

        /// <summary>버그 추적용 임시 진단 로그. 문제 해결 후 통째로 지우는 것이 전제다.</summary>
        Diagnostics = 1 << 15,

        /// <summary>
        /// 성능 벤치·스모크 주행 (`-perfrun` · `-smoke`). <see cref="Diagnostics"/>와 달리
        /// <b>지우지 않는 상설 계통</b>이다 — 벤치가 무엇을 재고 어디서 멈췄는지는 실행마다 남아야 한다.
        /// </summary>
        Performance = 1 << 16,

        All = ~0,
    }
}
