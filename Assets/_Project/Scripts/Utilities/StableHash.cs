namespace Game.Utilities
{
    /// <summary>
    /// 실행 간 안정적인 문자열 해시 — string.GetHashCode()는 런타임에 따라 실행마다 달라질 수
    /// 있어 FNV-1a로 직접 계산한다. 에디터·MPPM 가상 플레이어가 persistentDataPath를 공유하는
    /// 환경에서 인스턴스별 파일명 분리(dataPath 해시)에 쓴다 (M6 1차 식별 토큰 → 3차 메타 저장).
    /// </summary>
    public static class StableHash
    {
        /// <summary>FNV-1a 32비트 해시의 16진 문자열 (8자리).</summary>
        public static string Fnv1aHex(string value)
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
