using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// CLI 빌드 진입점. 로컬 CLI와 CI(GameCI unity-builder)가 <b>같은 코드</b>를 탄다.
    /// 사용: Unity -batchmode -executeMethod Game.Editor.BuildScript.PerformWindowsBuild
    ///
    /// GameCI는 이 메서드를 부르면서 <c>-customBuildPath</c>(확장자까지 포함한 절대 경로)와
    /// <c>-buildVersion</c>을 넘긴다. 인자가 없으면 로컬 기본값으로 굽는다.
    /// 출력은 명령에 대한 응답이라 카테고리 필터에 걸리면 안 되므로
    /// 규약(architecture-rules.md §3 "예외 둘")에 따라 <see cref="Debug"/>를 그대로 쓴다.
    /// </summary>
    public static class BuildScript
    {
        private const string DefaultOutputPath = "Builds/StandaloneWindows64/TrainSurvival.exe";
        private const string WindowsExtension = ".exe";

        public static void PerformWindowsBuild()
        {
            Build(BuildOptions.None);
        }

        /// <summary>
        /// 벤치·스모크용 <b>개발 빌드</b>. 배포 빌드와 갈라 두는 이유는 하나다 —
        /// <c>DEVELOPMENT_BUILD</c>가 켜져야 <c>ProfilerRecorder</c> 카운터 다수가 채워진다.
        /// 그 대가로 <b>배포판은 이 빌드보다 빠르므로</b>, 결과 JSON은 <c>development: true</c>를
        /// 함께 남겨 무엇을 잰 값인지 스스로 밝힌다 (성능 프로파일링 자동화 계획 §7).
        /// </summary>
        public static void PerformPerfBuild()
        {
            Build(BuildOptions.Development);
        }

        private static void Build(BuildOptions buildOptions)
        {
            string outputPath = ResolveOutputPath();

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new Exception("Build failed: EditorBuildSettings 에 켜진 씬이 하나도 없다.");
            }

            ApplyBuildVersion();

            Debug.Log($"[Build] 대상 {outputPath} · 씬 {scenes.Length}개 · 버전 {PlayerSettings.bundleVersion}" +
                      $" · 개발 빌드 {(buildOptions & BuildOptions.Development) != 0}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = buildOptions
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[Build] 결과 {summary.result} · {summary.totalSize / (1024 * 1024)} MB · " +
                      $"{summary.totalTime} · 오류 {summary.totalErrors}건 · 경고 {summary.totalWarnings}건");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Build failed: {summary.result} (오류 {summary.totalErrors}건)");
            }
        }

        /// <summary>
        /// GameCI가 준 경로를 쓰되, 확장자가 빠져 있으면 붙인다 (없으면 Unity가 빌드를 거부한다).
        /// 인자가 없는 로컬 실행에서는 기존 기본 경로를 그대로 쓴다.
        /// </summary>
        private static string ResolveOutputPath()
        {
            string custom = GetCommandLineArg("-customBuildPath");
            if (string.IsNullOrWhiteSpace(custom))
            {
                return DefaultOutputPath;
            }

            return custom.EndsWith(WindowsExtension, StringComparison.OrdinalIgnoreCase)
                ? custom
                : custom + WindowsExtension;
        }

        /// <summary>
        /// GameCI의 versioning 결과를 산출물에 박는다. versioning 을 끄면 "none" 이 넘어오므로 거른다.
        /// </summary>
        private static void ApplyBuildVersion()
        {
            string version = GetCommandLineArg("-buildVersion");
            if (string.IsNullOrWhiteSpace(version) ||
                string.Equals(version, "none", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PlayerSettings.bundleVersion = version;
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
