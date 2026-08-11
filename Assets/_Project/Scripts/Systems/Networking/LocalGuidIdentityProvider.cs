using System;
using System.IO;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// 개발용 <see cref="IPlayerIdentityProvider"/> — 최초 접근 시 GUID를 생성해 로컬 파일로 영속한다.
    /// MPPM 가상 플레이어는 프로젝트 클론(Library/VP/mppm*)으로 실행돼 persistentDataPath·PlayerPrefs를
    /// 본 에디터와 공유하므로(회사·제품명 기반 경로), 인스턴스마다 다른 Application.dataPath를
    /// 해시해 파일명에 넣어 토큰을 분리한다 — M6 1차 결정 ③.
    /// </summary>
    public sealed class LocalGuidIdentityProvider : IPlayerIdentityProvider
    {
        private const string TokenDirectoryName = "PlayerIdentity";

        private string _cachedToken;

        public string LocalPlayerToken
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedToken))
                {
                    _cachedToken = LoadOrCreateToken();
                }

                return _cachedToken;
            }
        }

        private static string LoadOrCreateToken()
        {
            string path = GetTokenFilePath();
            try
            {
                if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path).Trim(), out Guid stored))
                {
                    return stored.ToString("N");
                }

                string token = Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, token);
                return token;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[LocalGuidIdentityProvider] 토큰 파일 접근 실패({e.Message}) — 이번 실행 한정 임시 토큰을 사용합니다.");
                return Guid.NewGuid().ToString("N");
            }
        }

        private static string GetTokenFilePath()
        {
            string instanceKey = ComputeStableHash(Application.dataPath);
            return Path.Combine(Application.persistentDataPath, TokenDirectoryName, $"token-{instanceKey}.txt");
        }

        // string.GetHashCode()는 런타임에 따라 실행마다 달라질 수 있어 FNV-1a로 직접 계산한다.
        private static string ComputeStableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value)
                {
                    hash = (hash ^ c) * 16777619u;
                }

                return hash.ToString("x8");
            }
        }
    }
}
