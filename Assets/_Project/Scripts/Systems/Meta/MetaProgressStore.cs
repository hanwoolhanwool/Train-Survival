using System;
using System.IO;
using Game.Utilities;
using UnityEngine;

namespace Game.Systems.Meta
{
    /// <summary>
    /// 메타 진행 파일 I/O — <c>persistentDataPath/Meta/progress-{해시}.json</c>.
    /// MPPM 가상 플레이어가 persistentDataPath를 공유하므로 dataPath 해시로 파일을 분리한다
    /// (식별 토큰과 같은 함정·같은 처방 — M6 1차 결정 ③ 선례). 읽기·쓰기 실패는 게임을 막지
    /// 않는다 — 손상 파일은 새 진행으로, 쓰기 실패는 경고 로그로 흡수한다.
    /// </summary>
    public sealed class MetaProgressStore
    {
        private const string DirectoryName = "Meta";

        public MetaProgress Load()
        {
            string path = GetFilePath();
            try
            {
                if (File.Exists(path))
                {
                    return MetaProgressOps.Normalize(
                        JsonUtility.FromJson<MetaProgress>(File.ReadAllText(path)));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                Debug.LogWarning($"[MetaProgressStore] 메타 진행 읽기 실패({e.Message}) — 새 진행으로 시작합니다.");
            }

            return new MetaProgress();
        }

        public void Save(MetaProgress progress)
        {
            string path = GetFilePath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(progress, prettyPrint: true));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[MetaProgressStore] 메타 진행 저장 실패({e.Message}) — 이번 기록을 건너뜁니다.");
            }
        }

        private static string GetFilePath()
        {
            string instanceKey = StableHash.Fnv1aHex(Application.dataPath);
            return Path.Combine(Application.persistentDataPath, DirectoryName, $"progress-{instanceKey}.json");
        }
    }
}
