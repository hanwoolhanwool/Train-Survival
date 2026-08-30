namespace Game.Gameplay.Train
{
    /// <summary>
    /// 발자국 크기를 알아야 하는 건축물 뷰 (천막 계획 1차) — 가변 크기 종류는 프리팹 하나가
    /// 여러 크기로 서기 때문에, 스폰 뒤 자기 항목의 실제 크기를 받아 모양을 맞춘다.
    ///
    /// <see cref="StructureView"/>를 스폰하는 쪽이 이 인터페이스를 찾아 부르므로, 새로운 가변 크기
    /// 건축물이 생겨도 스포너는 종류를 알 필요가 없다(OCP).
    /// </summary>
    public interface IStructureFootprintView
    {
        /// <summary>
        /// 점유 칸 수와 셀 크기를 받아 모양을 맞춘다 — <b>회전 전(로컬 기준)</b> 값이다.
        /// 회전은 스폰할 때 Transform에 이미 걸려 있으므로 여기서 또 반영하면 두 번 도는 셈이 된다.
        /// 로컬 크기는 <paramref name="width"/> × <paramref name="cellSize"/> (X),
        /// <paramref name="length"/> × <paramref name="cellSize"/> (Z)다.
        /// </summary>
        void ApplyFootprint(int width, int length, float cellSize);
    }
}
