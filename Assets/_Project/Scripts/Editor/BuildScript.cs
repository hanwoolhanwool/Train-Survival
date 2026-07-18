using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    /// <summary>
    /// CLI 빌드 진입점.
    /// 사용: Unity -batchmode -executeMethod Game.Editor.BuildScript.PerformWindowsBuild
    /// </summary>
    public static class BuildScript
    {
        private const string OutputPath = "Builds/StandaloneWindows64/TrainSurvival.exe";

        public static void PerformWindowsBuild()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Build failed: {report.summary.result}");
            }
        }
    }
}
