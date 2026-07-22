using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 저주기 위치 스냅샷(10~15Hz)의 클라이언트 보간 버퍼 (네트워크 문서 §6.2).
    /// 수신 스냅샷을 시각과 함께 쌓고, 렌더 시각(현재 − 보간 지연)에 해당하는 위치·방향을 보간해 꺼낸다.
    /// "화면상 움직임의 부드러움은 보간으로 보장" — 순수 로직 (EditMode 테스트 대상).
    /// </summary>
    public sealed class MotionSnapshotBuffer
    {
        private readonly struct Snapshot
        {
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly double Time;

            public Snapshot(Vector3 position, float yaw, double time)
            {
                Position = position;
                Yaw = yaw;
                Time = time;
            }
        }

        private const double MaxKeepSeconds = 1.0;

        private readonly List<Snapshot> _snapshots = new List<Snapshot>(16);

        public int Count => _snapshots.Count;

        public void Clear()
        {
            _snapshots.Clear();
        }

        /// <summary>스냅샷을 추가한다. 시각은 단조 증가를 전제로 하며, 오래된 항목은 자동 정리된다.</summary>
        public void AddSnapshot(Vector3 position, float yaw, double time)
        {
            _snapshots.Add(new Snapshot(position, yaw, time));

            while (_snapshots.Count > 2 && time - _snapshots[0].Time > MaxKeepSeconds)
            {
                _snapshots.RemoveAt(0);
            }
        }

        /// <summary>
        /// 렌더 시각의 위치·방향을 보간해 꺼낸다. 스냅샷이 없으면 false.
        /// 렌더 시각이 범위 밖이면 가장 가까운 끝 값을 유지한다 (외삽하지 않는다 — 역행/워프 방지).
        /// </summary>
        public bool TrySample(double renderTime, out Vector3 position, out float yaw)
        {
            if (_snapshots.Count == 0)
            {
                position = Vector3.zero;
                yaw = 0f;
                return false;
            }

            if (renderTime <= _snapshots[0].Time)
            {
                position = _snapshots[0].Position;
                yaw = _snapshots[0].Yaw;
                return true;
            }

            Snapshot last = _snapshots[_snapshots.Count - 1];
            if (renderTime >= last.Time)
            {
                position = last.Position;
                yaw = last.Yaw;
                return true;
            }

            for (int i = 1; i < _snapshots.Count; i++)
            {
                if (renderTime <= _snapshots[i].Time)
                {
                    Snapshot from = _snapshots[i - 1];
                    Snapshot to = _snapshots[i];
                    float t = (float)((renderTime - from.Time) / (to.Time - from.Time));
                    position = Vector3.Lerp(from.Position, to.Position, t);
                    yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
                    return true;
                }
            }

            position = last.Position;
            yaw = last.Yaw;
            return true;
        }
    }
}
