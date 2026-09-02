using UnityEngine;

namespace Game.Systems.Diagnostics
{
    /// <summary>
    /// 시나리오 에셋을 이름으로 찾는다. <b>Resources를 쓰는 이유</b>는 <see cref="PerfRunner"/>가
    /// 씬에 배치되지 않아 인스펙터 참조를 받을 수 없기 때문이다 — 인자로 받은 이름 하나로
    /// 에셋에 닿아야 하고, 그 경로가 Resources다.
    /// </summary>
    /// <remarks>
    /// 시나리오가 늘어나도 코드는 그대로다 — 폴더에 에셋을 하나 더 넣으면 끝이다
    /// (§6 3차 완료 기준 "시나리오 파일 추가만으로 새 측정이 되고 코드 변경이 없다").
    /// </remarks>
    public static class PerfScenarioCatalog
    {
        /// <summary>시나리오 에셋이 사는 Resources 하위 폴더.</summary>
        public const string ResourceFolder = "PerfScenarios";

        /// <summary>
        /// 이름으로 찾는다. 파일명이 먼저이고, 못 찾으면 폴더를 훑어
        /// <see cref="PerfScenario.ScenarioId"/>로 한 번 더 찾는다 — 파일명과 id가 어긋나도
        /// "시나리오를 못 찾았다"로 60초를 날리지 않게 한다.
        /// </summary>
        public static PerfScenario Find(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId))
            {
                return null;
            }

            var byFileName = Resources.Load<PerfScenario>($"{ResourceFolder}/{scenarioId}");
            if (byFileName != null)
            {
                return byFileName;
            }

            PerfScenario[] all = Resources.LoadAll<PerfScenario>(ResourceFolder);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ScenarioId == scenarioId)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
