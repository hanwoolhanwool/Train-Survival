namespace Game.Gameplay.Train
{
    /// <summary>수리 망치가 겨누는 열차 부위 종류 (기획서 §9 — 수리 망치로 수리). RPC 페이로드로 쓰므로 byte 고정.</summary>
    public enum TrainPartKind : byte
    {
        Car = 0,
        Coupling = 1,
        Structure = 2,
    }
}
