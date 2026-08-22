using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Game.Core.Logging
{
    /// <summary>
    /// 프로젝트의 모든 디버그 로그가 지나가는 단일 창구.
    /// <c>UnityEngine.Debug</c>를 직접 부르지 말고 항상 이쪽을 쓴다.
    ///
    /// <para><b>1. 릴리스에서 사라진다</b> — <see cref="Info"/>·<see cref="Warn"/>과 그 변형은
    /// <see cref="ConditionalAttribute"/>가 걸려 있어 에디터·개발 빌드에서만 컴파일된다.
    /// 호출 자체가 제거되므로 인자의 문자열 보간 비용도 릴리스에서는 0이다.
    /// <see cref="Error"/>만 릴리스에 남는다 — 배포판에서 사고를 진단할 수단은 남겨야 한다.</para>
    ///
    /// <para><b>2. 카테고리로 켜고 끈다</b> — <see cref="Enabled"/>는 비트 플래그다.
    /// <c>GameLog.Only(LogCategory.Harpoon)</c>처럼 좁히면 그 계통만 콘솔에 남는다.
    /// 에디터에서는 <c>Game/QA/Log Categories</c> 창으로 조절하며, 설정은 EditorPrefs에 남는다.
    /// <see cref="Error"/>는 이 필터를 무시하고 항상 출력된다.</para>
    ///
    /// <para><b>3. 접두어는 자동이다</b> — 호출부가 태그 문자열을 쓰지 않는다.
    /// 카테고리와 <c>[CallerFilePath]</c>로 <c>[Harpoon/HarpoonProjectile] 메시지</c> 형태가 만들어진다
    /// (CallerFilePath는 컴파일 타임 리터럴이라 호출 비용이 없고, 파일명 추출 결과는 캐시된다).</para>
    ///
    /// <para><b>4. 스팸은 <see cref="InfoLimited"/>로 막는다</b> — 파일마다 카운터 필드를 만들지 않는다.
    /// 매 프레임 경로처럼 문자열 보간조차 아까운 자리에서는 <see cref="IsEnabled"/>로 먼저 가드한다.</para>
    /// </summary>
    public static class GameLog
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";

#if UNITY_EDITOR
        private const string PrefsKey = "Game.GameLog.EnabledCategories";
#endif

        private static LogCategory _enabled = LogCategory.All;

        // 접두어 조립 캐시 — enum.ToString()과 경로 파싱을 매 호출 반복하지 않는다.
        private static readonly Dictionary<LogCategory, string> _categoryNames = new Dictionary<LogCategory, string>();
        private static readonly Dictionary<string, string> _callerNames = new Dictionary<string, string>();

        // InfoLimited/WarnLimited/Once의 키별 출력 횟수.
        private static readonly Dictionary<string, int> _emitCounts = new Dictionary<string, int>();

        static GameLog()
        {
#if UNITY_EDITOR
            _enabled = (LogCategory)UnityEditor.EditorPrefs.GetInt(PrefsKey, (int)LogCategory.All);
#endif
        }

        /// <summary>현재 출력이 허용된 카테고리 집합. 에디터에서는 변경 즉시 EditorPrefs에 저장된다.</summary>
        public static LogCategory Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetInt(PrefsKey, (int)_enabled);
#endif
            }
        }

        /// <summary>해당 카테고리가 켜져 있는지. 매 프레임 경로에서 문자열 보간 전에 가드로 쓴다.</summary>
        public static bool IsEnabled(LogCategory category) => (_enabled & category) != LogCategory.None;

        /// <summary>지정한 카테고리를 추가로 켠다.</summary>
        public static void Enable(LogCategory category) => Enabled = _enabled | category;

        /// <summary>지정한 카테고리를 끈다.</summary>
        public static void Disable(LogCategory category) => Enabled = _enabled & ~category;

        /// <summary>지정한 카테고리만 남기고 전부 끈다 — 한 계통을 집중해서 볼 때.</summary>
        public static void Only(LogCategory category) => Enabled = category;

        /// <summary>전 카테고리를 켠다.</summary>
        public static void EnableAll() => Enabled = LogCategory.All;

        /// <summary>전 카테고리를 끈다 (<see cref="Error"/>는 여전히 출력된다).</summary>
        public static void DisableAll() => Enabled = LogCategory.None;

        /// <summary>일반 정보 로그. 릴리스 빌드에서는 호출이 제거된다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void Info(LogCategory category, string message, Object context = null,
            [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category))
            {
                return;
            }

            Debug.Log(Format(category, message, callerPath), context);
        }

        /// <summary>경고 로그 — 비정상이지만 진행은 되는 상황. 릴리스 빌드에서는 호출이 제거된다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void Warn(LogCategory category, string message, Object context = null,
            [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category))
            {
                return;
            }

            Debug.LogWarning(Format(category, message, callerPath), context);
        }

        /// <summary>
        /// 오류 로그 — 배선 누락·계약 위반 등 고쳐야 하는 상황.
        /// 릴리스 빌드에도 남고 카테고리 필터도 무시한다.
        /// </summary>
        public static void Error(LogCategory category, string message, Object context = null,
            [CallerFilePath] string callerPath = null)
        {
            Debug.LogError(Format(category, message, callerPath), context);
        }

        /// <summary>
        /// 같은 <paramref name="key"/>로 <paramref name="limit"/>회까지만 찍는 정보 로그.
        /// 반복 경로의 진단 로그가 콘솔을 덮지 않게 한다 — 호출부에 카운터 필드를 두지 않는다.
        /// 횟수는 <c>(#n/limit)</c>로 메시지 끝에 붙는다.
        /// </summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void InfoLimited(LogCategory category, string key, int limit, string message,
            Object context = null, [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category) || !TryTakeSlot(key, limit, out int emitted))
            {
                return;
            }

            Debug.Log(Format(category, $"{message}  (#{emitted}/{limit})", callerPath), context);
        }

        /// <summary>같은 <paramref name="key"/>로 <paramref name="limit"/>회까지만 찍는 경고 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void WarnLimited(LogCategory category, string key, int limit, string message,
            Object context = null, [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category) || !TryTakeSlot(key, limit, out int emitted))
            {
                return;
            }

            Debug.LogWarning(Format(category, $"{message}  (#{emitted}/{limit})", callerPath), context);
        }

        /// <summary>같은 <paramref name="key"/>로 최초 1회만 찍는 정보 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void InfoOnce(LogCategory category, string key, string message, Object context = null,
            [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category) || !TryTakeSlot(key, 1, out _))
            {
                return;
            }

            Debug.Log(Format(category, message, callerPath), context);
        }

        /// <summary>같은 <paramref name="key"/>로 최초 1회만 찍는 경고 로그.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void WarnOnce(LogCategory category, string key, string message, Object context = null,
            [CallerFilePath] string callerPath = null)
        {
            if (!IsEnabled(category) || !TryTakeSlot(key, 1, out _))
            {
                return;
            }

            Debug.LogWarning(Format(category, message, callerPath), context);
        }

        /// <summary>
        /// 특정 키의 출력 횟수를 0으로 되돌린다 — 상태가 정상으로 돌아왔을 때 다시 알림을 받고 싶은 경우.
        /// (예: 입력이 닫혔다 열리면 "닫힘" 로그를 다시 받을 수 있게 한다.)
        /// </summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void ResetLimit(string key)
        {
            _emitCounts.Remove(key);
        }

        /// <summary>모든 횟수 제한을 초기화한다. 플레이 모드 진입 시 자동 호출된다.</summary>
        public static void ResetAllLimits()
        {
            _emitCounts.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // 도메인 리로드를 끈 상태에서도 플레이 진입마다 횟수 제한이 새로 시작하게 한다.
            ResetAllLimits();
        }

        private static bool TryTakeSlot(string key, int limit, out int emitted)
        {
            _emitCounts.TryGetValue(key, out int used);
            if (used >= limit)
            {
                emitted = used;
                return false;
            }

            emitted = used + 1;
            _emitCounts[key] = emitted;
            return true;
        }

        private static string Format(LogCategory category, string message, string callerPath)
        {
            return $"[{CategoryName(category)}/{CallerName(callerPath)}] {message}";
        }

        private static string CategoryName(LogCategory category)
        {
            if (!_categoryNames.TryGetValue(category, out string name))
            {
                name = category.ToString();
                _categoryNames[category] = name;
            }

            return name;
        }

        private static string CallerName(string callerPath)
        {
            if (string.IsNullOrEmpty(callerPath))
            {
                return "?";
            }

            if (_callerNames.TryGetValue(callerPath, out string name))
            {
                return name;
            }

            int end = callerPath.LastIndexOf('.');
            int start = callerPath.LastIndexOfAny(new[] { '/', '\\' }) + 1;
            name = end > start ? callerPath.Substring(start, end - start) : callerPath.Substring(start);
            _callerNames[callerPath] = name;
            return name;
        }
    }
}
