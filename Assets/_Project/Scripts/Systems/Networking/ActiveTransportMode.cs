using System;

namespace Game.Systems.Networking
{
    /// <summary>
    /// 이번 실행의 트랜스포트 모드 — 실행 인자(릴리스)·에디터 토글로 결정되며 실행 중 불변
    /// (M6 2차 결정 ②). 기본은 UnityTransport 직결이라 CI·MPPM·기존 개발 흐름은 무영향이다.
    /// </summary>
    public static class ActiveTransportMode
    {
        /// <summary>에디터 토글(EditorPrefs) 키 — Game/Steam Transport (Editor) 메뉴가 쓴다.</summary>
        public const string EditorPrefsKey = "TrainSurvival.SteamTransportEditor";

        private static NetworkTransportMode? _mode;

        public static NetworkTransportMode Mode
        {
            get
            {
                if (!_mode.HasValue)
                {
                    _mode = ResolveOnce();
                }

                return _mode.Value;
            }
        }

        public static bool IsSteam => Mode == NetworkTransportMode.SteamRelay;

        private static NetworkTransportMode ResolveOnce()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EditorPrefsKey, false))
            {
                return NetworkTransportMode.SteamRelay;
            }
#endif
            return NetworkTransportModeResolver.Resolve(Environment.GetCommandLineArgs());
        }
    }
}
