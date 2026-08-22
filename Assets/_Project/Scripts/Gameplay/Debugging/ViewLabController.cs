using Game.Core.Logging;
using System.Collections.Generic;
using Game.Gameplay.Combat;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// ViewLab 랩 컨트롤러 — 무기 뷰모델 피벗의 위치·회전·스케일과 캐릭터 애니메이션을
    /// 한 씬에서, Play 중에, 씬 뷰 핸들로 조정하고 결과를 Player.prefab에 저장한다
    /// (docs/plans/뷰랩-씬-계획.md). 키보드 핫키 없음 — 조작은 씬 뷰 핸들 + 마우스만.
    /// 에디터 전용 씬(빌드 미포함) 소속이라 런타임 비용·구조 규약은 본편 기준을 적용하지 않는다.
    /// </summary>
    public sealed class ViewLabController : MonoBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int JumpParam = Animator.StringToHash("Jump");
        private static readonly int DeadParam = Animator.StringToHash("Dead");
        private static readonly int GrabbedParam = Animator.StringToHash("Grabbed");

        /// <summary>피벗 TRS의 단일 출처 — 저장 대상 (계획 §8-①).</summary>
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        [SerializeField] private ViewLabOrbitCamera _orbitCamera;
        [SerializeField] private ViewLabGizmo _gizmo;

        [Tooltip("가슴·머리 본 절차 회전 계수 — PlayerAimView와 같은 에셋을 물린다 (계획 §4-③)")]
        [SerializeField] private PlayerAnimationSettings _animationSettings;

        [Tooltip("피치 클램프·걷기/달리기 속도 — 본편과 같은 에셋을 물린다")]
        [SerializeField] private PlayerMovementSettings _movementSettings;

        [SerializeField] private float _moveStep = 0.05f;
        [SerializeField] private float _moveStepFine = 0.005f;
        [SerializeField] private float _rotateStep = 5f;
        [SerializeField] private float _rotateStepFine = 0.5f;
        [SerializeField] private float _scaleStep = 0.05f;
        [SerializeField] private float _scaleStepFine = 0.005f;

        [Tooltip("HUD·dirty 검사 주기(초) — 매 프레임 문자열 할당 금지 (계획 §4-②)")]
        [SerializeField] private float _hudRefreshInterval = 0.25f;

        [SerializeField] private float _panelWidth = 300f;

        private Transform _playerRoot;
        private Transform _aimPivot;
        private Transform _cameraPivot;
        private Camera _labCamera;

        private readonly List<Transform> _pivots = new List<Transform>();
        private readonly List<Renderer[]> _pivotRenderers = new List<Renderer[]>();
        private int _pivotIndex = -1;

        private GameObject _girlModel;
        private GameObject _manModel;
        private Animator _activeAnimator;
        private SkinnedMeshRenderer _activeCharacterRenderer;
        private bool _useGirl = true;

        private LocomotionTier _tier = LocomotionTier.Idle;
        private bool _dead;
        private bool _grabbed;
        private float _pitch;
        private float _playbackSpeed = 1f;
        private bool _fpCamera;
        private bool _fineStep;

        // 가슴·머리 본 캐시 — 캐릭터(Animator) 전환 시 무효화.
        private Animator _cachedBoneAnimator;
        private Transform _chestBone;
        private Transform _headBone;

        // 패널 수치 필드 버퍼 — 타이핑 중 갱신으로 덮지 않는다 (RigLab 필드 버퍼 방식).
        private readonly string[] _fields = new string[7];
        private Vector3 _lastFieldPosition;
        private Quaternion _lastFieldRotation;
        private Vector3 _lastFieldScale;
        private bool _fieldsValid;
        private bool _textFieldFocused;

        private bool _dirty;
        private float _nextHudRefresh;
        private string _hudText = "";
        private GUIStyle _hudStyle;
        private Vector2 _panelScroll;

        private Transform CurrentPivot =>
            _pivotIndex >= 0 && _pivotIndex < _pivots.Count ? _pivots[_pivotIndex] : null;

        private float WalkSpeed => _movementSettings != null ? _movementSettings.WalkSpeed : 4.5f;
        private float RunSpeed => _movementSettings != null ? _movementSettings.RunSpeed : 7f;
        private float MaxPitch => _movementSettings != null ? _movementSettings.MaxPitch : 85f;

        private void Awake()
        {
            // 비포커스에서 Play가 멈추면 씬 뷰 핸들 조정 중 포즈가 정지한다 (계획 §7).
            Application.runInBackground = true;
        }

        private void Start()
        {
            if (!TryBindPlayer())
            {
                enabled = false;
                return;
            }

            DisableNetworkGates();
            ApplyCharacter(useGirl: true);

            if (_orbitCamera != null)
            {
                _orbitCamera.SetTarget(_playerRoot);
                _labCamera = _orbitCamera.GetComponent<Camera>();
            }

            if (_pivots.Count > 0)
            {
                SelectPivot(0);
            }
        }

        /// <summary>씬의 Player.prefab 인스턴스에서 조정 대상 계층을 찾는다 — 이름 하드코딩은 피벗이 아니라 고정 계층만.</summary>
        private bool TryBindPlayer()
        {
            PlayerAimView aim = FindAnyObjectByType<PlayerAimView>(FindObjectsInactive.Include);
            if (aim == null)
            {
                GameLog.Error(LogCategory.ViewLab, "씬에 Player.prefab 인스턴스가 없다 — PlayerAimView 미발견");
                return false;
            }

            _playerRoot = aim.transform.root;
            _aimPivot = _playerRoot.Find("AimPivot");
            _cameraPivot = _playerRoot.Find("CameraRig/CameraPivot");

            Transform girl = _playerRoot.Find("Body/Character_Girl");
            Transform man = _playerRoot.Find("Body/Character_Man");
            _girlModel = girl != null ? girl.gameObject : null;
            _manModel = man != null ? man.gameObject : null;

            if (_aimPivot == null)
            {
                GameLog.Error(LogCategory.ViewLab, "AimPivot을 찾지 못했다 — Player.prefab 계층 변경 여부 확인");
                return false;
            }

            // 피벗은 자식 열거로 수용 — 새 피벗(라이플·마체테)이 생기면 자동 포함 (계획 §1.3).
            for (int i = 0; i < _aimPivot.childCount; i++)
            {
                Transform pivot = _aimPivot.GetChild(i);
                _pivots.Add(pivot);
                _pivotRenderers.Add(pivot.GetComponentsInChildren<Renderer>(includeInactive: true));
            }

            return true;
        }

        /// <summary>
        /// 네트워크 게이트 무력화 (계획 §4-①) — View들의 Awake(SetVisible(false))가 이미 돈 뒤인
        /// Start에서 실행해야 렌더러 재점등이 유지된다 (계획 §7).
        /// </summary>
        private void DisableNetworkGates()
        {
            // ① 뷰모델 표시 게이트 — 비활성화 후 표시 제어를 랩이 인수한다.
            foreach (GunView view in _playerRoot.GetComponentsInChildren<GunView>(true))
            {
                view.enabled = false;
            }

            foreach (HarpoonView view in _playerRoot.GetComponentsInChildren<HarpoonView>(true))
            {
                view.enabled = false;
            }

            foreach (RepairHammerView view in _playerRoot.GetComponentsInChildren<RepairHammerView>(true))
            {
                view.enabled = false;
            }

            foreach (MeleeSwingView view in _playerRoot.GetComponentsInChildren<MeleeSwingView>(true))
            {
                view.enabled = false;
            }

            // ② 캐릭터 택1은 랩이 직접 — 소유자 ShadowsOnly 경로 차단.
            foreach (PlayerCharacterView view in _playerRoot.GetComponentsInChildren<PlayerCharacterView>(true))
            {
                view.enabled = false;
            }

            // ③ 미스폰이라 침묵하지만 명시적으로 꺼서 피치·파라미터 주입과 경합할 여지를 없앤다.
            foreach (PlayerAimView view in _playerRoot.GetComponentsInChildren<PlayerAimView>(true))
            {
                view.enabled = false;
            }

            foreach (PlayerAnimationDriver driver in _playerRoot.GetComponentsInChildren<PlayerAnimationDriver>(true))
            {
                driver.enabled = false;
            }

            // ④ 프리팹 내부 카메라 리그 — 미스폰이라 소유자 판정이 없어 기본 활성으로 남는데,
            //    랩 카메라·AudioListener와 충돌하므로 통째로 끈다 (비활성이어도 Transform 계산은 유효).
            Transform cameraRig = _playerRoot.Find("CameraRig");
            if (cameraRig != null)
            {
                cameraRig.gameObject.SetActive(false);
            }
        }

        // ── 캐릭터·애니메이션 (계획 §4-③) ─────────────────────────

        private void ApplyCharacter(bool useGirl)
        {
            _useGirl = useGirl;
            if (_girlModel != null)
            {
                _girlModel.SetActive(useGirl);
            }

            if (_manModel != null)
            {
                _manModel.SetActive(!useGirl);
            }

            GameObject model = useGirl ? _girlModel : _manModel;
            _activeAnimator = model != null ? model.GetComponent<Animator>() : null;
            _activeCharacterRenderer = model != null
                ? model.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                : null;
            _cachedBoneAnimator = null;
            _chestBone = null;
            _headBone = null;
            ApplyCharacterVisibility();

            if (_activeAnimator == null)
            {
                return;
            }

            // 오프스크린에서 포즈가 멈추는 함정 대응 (계획 §4-①-4).
            _activeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _activeAnimator.speed = _playbackSpeed;
            ReapplyParameters();
        }

        /// <summary>캐릭터 전환 뒤에도 패널 상태(이동·사망·그랩)가 이어지도록 파라미터를 재주입.</summary>
        private void ReapplyParameters()
        {
            _activeAnimator.SetFloat(SpeedParam, ViewLabMath.SpeedForTier(_tier, WalkSpeed, RunSpeed));
            _activeAnimator.SetBool(DeadParam, _dead);
            _activeAnimator.SetBool(GrabbedParam, _grabbed);
        }

        private void SetTier(LocomotionTier tier)
        {
            _tier = tier;
            if (_activeAnimator != null)
            {
                _activeAnimator.SetFloat(SpeedParam, ViewLabMath.SpeedForTier(tier, WalkSpeed, RunSpeed));
            }
        }

        // ── 무기 선택 + 피벗 표시 (계획 §4-②) ────────────────────

        private void SelectPivot(int index)
        {
            _pivotIndex = index;
            for (int i = 0; i < _pivots.Count; i++)
            {
                SetPivotVisible(i, i == index);
            }

            Transform pivot = CurrentPivot;
            if (_gizmo != null)
            {
                _gizmo.Target = pivot;
            }

#if UNITY_EDITOR
            // 씬 뷰에서 바로 이동/회전/스케일 핸들로 끈다 — "사람이 씬에서 수정하는 방식"의 본체.
            if (pivot != null)
            {
                UnityEditor.Selection.activeGameObject = pivot.gameObject;
            }
#endif
            RefreshFields();
        }

        /// <summary>
        /// 피벗을 반대 손으로 옮긴다 — 위치·회전을 YZ 평면 기준으로 미러링한다 (스케일은 유지).
        /// 손을 별도 값으로 들고 있지 않으므로 미러링 자체가 곧 지정이고, 결과는 다른 조정과
        /// 똑같이 dirty로 잡혀 프리팹 저장에 실린다.
        /// 메시는 뒤집지 않는다 — 음수 스케일은 노멀·컬링을 깨뜨린다. 좌우 비대칭 모델은
        /// 미러 결과를 출발점 삼아 사람이 마저 맞춘다.
        /// </summary>
        private void SetPivotHand(Transform pivot, PivotHand hand)
        {
            PivotHand current = ViewLabMath.ResolveHand(pivot.localPosition.x);
            if (current == hand || current == PivotHand.Center)
            {
                return;
            }

            pivot.localPosition = ViewLabMath.MirrorPosition(pivot.localPosition);
            pivot.localRotation = ViewLabMath.MirrorRotation(pivot.localRotation);
            RefreshFields();
        }

        private void SetPivotVisible(int index, bool visible)
        {
            Renderer[] renderers = _pivotRenderers[index];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (visible && !renderer.gameObject.activeSelf)
                {
                    // MeleePivot의 Blade처럼 GameObject째 꺼진 시각도 켠다.
                    renderer.gameObject.SetActive(true);
                }

                renderer.enabled = visible;
            }
        }

        // ── 주기 갱신 ──────────────────────────────────────────────

        private void Update()
        {
            if (Time.unscaledTime < _nextHudRefresh)
            {
                return;
            }

            _nextHudRefresh = Time.unscaledTime + _hudRefreshInterval;
#if UNITY_EDITOR
            _dirty = ComputeDirty();
#endif
            SyncFieldsFromScene();
            RefreshHud();
        }

        private void LateUpdate()
        {
            ApplyPitch();
            ApplyBodyPitch();
            ApplyLabCamera();
        }

        /// <summary>피치 슬라이더 → AimPivot·CameraPivot — PlayerAimView와 동일 공식.</summary>
        private void ApplyPitch()
        {
            Quaternion pitchRotation = Quaternion.Euler(_pitch, 0f, 0f);
            if (_aimPivot != null)
            {
                _aimPivot.localRotation = pitchRotation;
            }

            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = pitchRotation;
            }
        }

        /// <summary>
        /// 가슴·머리 본 절차 회전 — PlayerAimView.ApplyBodyPitch와 동일 공식.
        /// 본 회전이 빠지면 TP 정합 판단이 틀어진다 (계획 §4-③). Animator 평가 뒤인 LateUpdate 필수.
        /// </summary>
        private void ApplyBodyPitch()
        {
            Animator animator = _activeAnimator;
            if (animator == null || !animator.isActiveAndEnabled || _animationSettings == null || _dead)
            {
                return;
            }

            if (animator != _cachedBoneAnimator)
            {
                _cachedBoneAnimator = animator;
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                _chestBone = chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            Vector3 right = _playerRoot.right;
            if (_chestBone != null)
            {
                float chestAngle = _animationSettings.ChestPitchOffsetDegrees
                    + _pitch * _animationSettings.ChestPitchWeight;
                _chestBone.rotation = Quaternion.AngleAxis(chestAngle, right) * _chestBone.rotation;
            }

            if (_headBone != null)
            {
                float headAngle = _animationSettings.HeadPitchOffsetDegrees
                    + _pitch * _animationSettings.HeadPitchWeight;
                _headBone.rotation = Quaternion.AngleAxis(headAngle, right) * _headBone.rotation;
            }
        }

        /// <summary>FP 모드 — 소유자 화면에서 뷰모델이 실제로 어떻게 보이는지가 최종 판정 기준 (계획 §4-④).</summary>
        private void ApplyLabCamera()
        {
            if (!_fpCamera || _labCamera == null || _cameraPivot == null)
            {
                return;
            }

            _labCamera.transform.SetPositionAndRotation(_cameraPivot.position, _cameraPivot.rotation);
        }

        private void SetFpCamera(bool fp)
        {
            _fpCamera = fp;
            if (_orbitCamera != null)
            {
                // FP 동안 궤도 카메라의 LateUpdate 재배치를 멈춰 조작권을 넘긴다.
                _orbitCamera.enabled = !fp;
            }

            ApplyCharacterVisibility();
        }

        /// <summary>
        /// FP 모드에서는 자기 몸을 그림자만 남긴다 — 본편 소유자와 같은 처리
        /// (<see cref="PlayerCharacterView"/>의 IsOwner 분기). 몸이 카메라(y 1.6)를 가리면
        /// 뷰모델이 실제로 어떻게 보이는지 판정할 수 없다.
        /// </summary>
        private void ApplyCharacterVisibility()
        {
            if (_activeCharacterRenderer == null)
            {
                return;
            }

            _activeCharacterRenderer.shadowCastingMode = _fpCamera
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ── dirty·리셋·저장 (계획 §4-⑤) ──────────────────────────

#if UNITY_EDITOR
        /// <summary>
        /// 프리팹 에셋에서 같은 이름의 피벗을 찾는다. Play 모드에서는 씬 인스턴스의 프리팹 연결이
        /// 끊겨 GetCorrespondingObjectFromSource가 null이므로, 에셋을 직접 로드해 비교한다.
        /// </summary>
        private static Transform FindAssetPivot(string pivotName)
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (asset == null)
            {
                return null;
            }

            return ViewLabMath.FindChildByName(asset.transform.Find("AimPivot"), pivotName);
        }

        /// <summary>씬 피벗 로컬 값 ↔ 프리팹 에셋 값 차이 — 주기 검사 전용.</summary>
        private bool ComputeDirty()
        {
            for (int i = 0; i < _pivots.Count; i++)
            {
                Transform pivot = _pivots[i];
                Transform source = FindAssetPivot(pivot.name);
                if (source == null)
                {
                    continue;
                }

                if (ViewLabMath.TrsDiffers(
                    pivot.localPosition, pivot.localRotation, pivot.localScale,
                    source.localPosition, source.localRotation, source.localScale))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>선택 피벗을 프리팹 에셋의 현재 값으로 복원 — 스케일 포함 (계획 §7).</summary>
        private void ResetPivotToPrefab(Transform pivot)
        {
            Transform source = FindAssetPivot(pivot.name);
            if (source == null)
            {
                GameLog.Warn(LogCategory.ViewLab, $"프리팹에서 {pivot.name}을 찾지 못해 리셋을 건너뛴다");
                return;
            }

            pivot.localPosition = source.localPosition;
            pivot.localRotation = source.localRotation;
            pivot.localScale = source.localScale;
            RefreshFields();
        }

        /// <summary>
        /// Play 중 프리팹 에셋 기록 — 에셋 기록은 Play 종료와 무관하게 유지된다 (계획 §4-⑤).
        /// 씬 인스턴스 오버라이드 Apply는 Play 중 불가능하므로 쓰지 않는다.
        /// </summary>
        private void SavePivotsToPrefab()
        {
            GameObject contents = UnityEditor.PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform assetAimPivot = contents.transform.Find("AimPivot");
                if (assetAimPivot == null)
                {
                    GameLog.Error(LogCategory.ViewLab, "프리팹에서 AimPivot을 찾지 못해 저장을 중단한다");
                    return;
                }

                int written = 0;
                for (int i = 0; i < _pivots.Count; i++)
                {
                    Transform pivot = _pivots[i];
                    Transform asset = ViewLabMath.FindChildByName(assetAimPivot, pivot.name);
                    if (asset == null)
                    {
                        GameLog.Warn(LogCategory.ViewLab, $"프리팹에 {pivot.name}이 없어 건너뛴다");
                        continue;
                    }

                    if (ViewLabMath.TrsDiffers(
                        pivot.localPosition, pivot.localRotation, pivot.localScale,
                        asset.localPosition, asset.localRotation, asset.localScale))
                    {
                        // 변경 전→후 로그 — 커밋 메시지 재료 (계획 §4-⑤).
                        GameLog.Info(LogCategory.ViewLab, $"{pivot.name}  pos {asset.localPosition:F4} → {pivot.localPosition:F4}"
                                                    + $"  rot {asset.localEulerAngles:F2} → {pivot.localEulerAngles:F2}"
                                                    + $"  scale {asset.localScale:F3} → {pivot.localScale:F3}");
                        written++;
                    }

                    asset.localPosition = pivot.localPosition;
                    asset.localRotation = pivot.localRotation;
                    asset.localScale = pivot.localScale;
                }

                UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                GameLog.Info(LogCategory.ViewLab, $"저장 완료 — 변경 {written}건 ({PlayerPrefabPath})");
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(contents);
            }

            _dirty = false;
        }
#endif

        // ── 필드 버퍼·HUD ─────────────────────────────────────────

        /// <summary>씬 뷰 핸들·넛지로 바뀐 값을 패널 수치에 반영 — 타이핑 중(포커스)엔 덮지 않는다.</summary>
        private void SyncFieldsFromScene()
        {
            Transform pivot = CurrentPivot;
            if (pivot == null || _textFieldFocused)
            {
                return;
            }

            if (_fieldsValid && !ViewLabMath.TrsDiffers(
                pivot.localPosition, pivot.localRotation, pivot.localScale,
                _lastFieldPosition, _lastFieldRotation, _lastFieldScale))
            {
                return;
            }

            RefreshFields();
        }

        private void RefreshFields()
        {
            Transform pivot = CurrentPivot;
            if (pivot == null)
            {
                return;
            }

            Vector3 pos = pivot.localPosition;
            Vector3 rot = pivot.localEulerAngles;
            _fields[0] = pos.x.ToString("0.####");
            _fields[1] = pos.y.ToString("0.####");
            _fields[2] = pos.z.ToString("0.####");
            _fields[3] = rot.x.ToString("0.##");
            _fields[4] = rot.y.ToString("0.##");
            _fields[5] = rot.z.ToString("0.##");
            _fields[6] = pivot.localScale.x.ToString("0.####");

            _lastFieldPosition = pivot.localPosition;
            _lastFieldRotation = pivot.localRotation;
            _lastFieldScale = pivot.localScale;
            _fieldsValid = true;
        }

        private void ApplyFields(Transform pivot)
        {
            GUIUtility.keyboardControl = 0;
            bool ok = float.TryParse(_fields[0], out float px)
                & float.TryParse(_fields[1], out float py)
                & float.TryParse(_fields[2], out float pz)
                & float.TryParse(_fields[3], out float rx)
                & float.TryParse(_fields[4], out float ry)
                & float.TryParse(_fields[5], out float rz);
            if (!ok)
            {
                GameLog.Warn(LogCategory.ViewLab, "입력 값 해석 실패 — 숫자만 입력하세요.");
                return;
            }

            pivot.localPosition = new Vector3(px, py, pz);
            pivot.localRotation = Quaternion.Euler(rx, ry, rz);
            if (float.TryParse(_fields[6], out float scale))
            {
                pivot.localScale = Vector3.one * scale;
            }

            RefreshFields();
        }

        private static string HandLabel(PivotHand hand)
        {
            switch (hand)
            {
                case PivotHand.Right:
                    return "오른팔";
                case PivotHand.Left:
                    return "왼팔";
                default:
                    return "중앙";
            }
        }

        private void RefreshHud()
        {
            Transform pivot = CurrentPivot;
            string adjust = pivot != null
                ? $"{pivot.name}  {HandLabel(ViewLabMath.ResolveHand(pivot.localPosition.x))}"
                  + $"  pos{pivot.localPosition:F3}  rot{pivot.localEulerAngles:F1}  scale{pivot.localScale:F2}"
                : "(AimPivot 아래 피벗 없음)";
            string dirtyMark = _dirty ? "  ● 미저장 변경 — Stop 전에 저장!" : "";

            _hudText =
                $"[뷰랩]  {adjust}{dirtyMark}\n"
                + $"캐릭터 {(_useGirl ? "Girl" : "Man")}  ·  {_tier}"
                + $"{(_dead ? "  ·  Dead" : "")}{(_grabbed ? "  ·  Grabbed" : "")}"
                + $"  ·  피치 {_pitch:0.#}°  ·  속도 x{_playbackSpeed:0.0}  ·  카메라 {(_fpCamera ? "FP" : "궤도")}\n"
                + "씬 뷰 핸들로 선택 피벗을 끌어 조정  ·  게임 뷰: 우클릭 회전 / 휠 줌 / 휠클릭 팬";
        }

        // ── OnGUI 패널 — 마우스가 유일 조작부 (계획 §4-②) ────────

        private void OnGUI()
        {
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _hudStyle.normal.textColor = new Color(0.6f, 0.9f, 1f);
            }

            GUI.Label(new Rect(12f, 12f, 1100f, 100f), _hudText, _hudStyle);
            DrawPanel();

            // 필드 포커스 감지는 OnGUI 안에서만 유효 — Update의 필드 동기가 읽는다.
            _textFieldFocused = GUIUtility.keyboardControl != 0;
        }

        private void DrawPanel()
        {
            var area = new Rect(Screen.width - _panelWidth - 12f, 12f, _panelWidth, Screen.height - 24f);
            GUILayout.BeginArea(area, GUI.skin.box);
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            DrawCharacterSection();
            GUILayout.Space(8f);
            DrawWeaponSection();
            GUILayout.Space(8f);
            DrawAdjustSection();
            GUILayout.Space(8f);
            DrawAnimationSection();
            GUILayout.Space(8f);
            DrawCameraSection();
            GUILayout.Space(8f);
            DrawSaveSection();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>선택 상태 버튼 — 켜져 있으면 초록 틴트.</summary>
        private bool ToggleButton(string label, bool active, params GUILayoutOption[] options)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.5f, 1f, 0.5f) : old;
            bool clicked = GUILayout.Button(label, options);
            GUI.backgroundColor = old;
            if (clicked)
            {
                GUIUtility.keyboardControl = 0;   // 버튼 조작 시 필드 포커스 해제
            }

            return clicked;
        }

        private bool ActionButton(string label, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Button(label, options);
            if (clicked)
            {
                GUIUtility.keyboardControl = 0;
            }

            return clicked;
        }

        private void DrawCharacterSection()
        {
            GUILayout.Label("── 캐릭터 (파지 공유 — 양쪽 확인 필수) ──");
            GUILayout.BeginHorizontal();
            if (ToggleButton("Girl", _useGirl) && !_useGirl)
            {
                ApplyCharacter(useGirl: true);
            }

            if (ToggleButton("Man", !_useGirl) && _useGirl)
            {
                ApplyCharacter(useGirl: false);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawWeaponSection()
        {
            GUILayout.Label("── 무기 (선택 = 씬 뷰 핸들 대상) ──");
            if (_pivots.Count == 0)
            {
                GUILayout.Label("(AimPivot 아래 피벗이 없다)");
                return;
            }

            for (int i = 0; i < _pivots.Count; i++)
            {
                if (ToggleButton(_pivots[i].name, _pivotIndex == i) && _pivotIndex != i)
                {
                    SelectPivot(i);
                }
            }
        }

        private void DrawAdjustSection()
        {
            Transform pivot = CurrentPivot;
            if (pivot == null)
            {
                return;
            }

            GUILayout.Label($"── 조정  {pivot.name} ──");
            DrawHandRow(pivot);

            GUILayout.BeginHorizontal();
            if (ToggleButton(_fineStep ? "미세 스텝 ON" : "미세 스텝 OFF", _fineStep))
            {
                _fineStep = !_fineStep;
            }

#if UNITY_EDITOR
            if (ActionButton("리셋 (프리팹 값)"))
            {
                ResetPivotToPrefab(pivot);
            }
#endif
            GUILayout.EndHorizontal();

            float moveStep = ViewLabMath.Step(_fineStep, _moveStep, _moveStepFine);
            float rotateStep = ViewLabMath.Step(_fineStep, _rotateStep, _rotateStepFine);
            float scaleStep = ViewLabMath.Step(_fineStep, _scaleStep, _scaleStepFine);

            GUILayout.Label($"위치 (스텝 {moveStep:0.###})");
            DrawFieldRow(0);
            DrawNudgeRow(isMove: true, (axis, sign) =>
            {
                pivot.localPosition = ViewLabMath.NudgePosition(pivot.localPosition, axis, sign * moveStep);
                RefreshFields();
            });

            GUILayout.Label($"회전 ° (스텝 {rotateStep:0.#})");
            DrawFieldRow(3);
            DrawNudgeRow(isMove: false, (axis, sign) =>
            {
                pivot.localRotation = ViewLabMath.NudgeRotation(pivot.localRotation, axis, sign * rotateStep);
                RefreshFields();
            });

            GUILayout.Label($"스케일 (균일, 스텝 {scaleStep:0.###})");
            GUILayout.BeginHorizontal();
            _fields[6] = GUILayout.TextField(_fields[6], GUILayout.ExpandWidth(true));
            if (ActionButton("−", GUILayout.Width(36f)))
            {
                pivot.localScale = ViewLabMath.NudgeUniformScale(pivot.localScale, -scaleStep);
                RefreshFields();
            }

            if (ActionButton("+", GUILayout.Width(36f)))
            {
                pivot.localScale = ViewLabMath.NudgeUniformScale(pivot.localScale, scaleStep);
                RefreshFields();
            }

            GUILayout.EndHorizontal();

            if (ActionButton("입력 값 적용"))
            {
                ApplyFields(pivot);
            }
        }

        /// <summary>좌우 손 지정 — 누르면 위치·회전이 미러링된다.</summary>
        private void DrawHandRow(Transform pivot)
        {
            PivotHand hand = ViewLabMath.ResolveHand(pivot.localPosition.x);
            if (hand == PivotHand.Center)
            {
                GUILayout.Label("손: 중앙 (X=0) — 좌우가 같아 지정 대상이 아니다");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("손", GUILayout.Width(28f));
            if (ToggleButton("오른팔", hand == PivotHand.Right))
            {
                SetPivotHand(pivot, PivotHand.Right);
            }

            if (ToggleButton("왼팔", hand == PivotHand.Left))
            {
                SetPivotHand(pivot, PivotHand.Left);
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>수치 입력 3칸 (pos 또는 rot).</summary>
        private void DrawFieldRow(int offset)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 3; i++)
            {
                _fields[offset + i] = GUILayout.TextField(_fields[offset + i], GUILayout.ExpandWidth(true));
            }

            GUILayout.EndHorizontal();
        }

        private static readonly string[] MoveNudgeLabels = { "X−", "X+", "Y−", "Y+", "Z−", "Z+" };
        private static readonly string[] RotateNudgeLabels = { "P−", "P+", "Y−", "Y+", "R−", "R+" };

        private void DrawNudgeRow(bool isMove, System.Action<int, float> nudge)
        {
            string[] labels = isMove ? MoveNudgeLabels : RotateNudgeLabels;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 3; i++)
            {
                if (ActionButton(labels[i * 2], GUILayout.ExpandWidth(true)))
                {
                    nudge(i, -1f);
                }

                if (ActionButton(labels[i * 2 + 1], GUILayout.ExpandWidth(true)))
                {
                    nudge(i, +1f);
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawAnimationSection()
        {
            GUILayout.Label("── 애니메이션 ──");
            if (_activeAnimator == null)
            {
                GUILayout.Label("(활성 캐릭터에 Animator가 없다)");
                return;
            }

            GUILayout.BeginHorizontal();
            if (ToggleButton("Idle", _tier == LocomotionTier.Idle))
            {
                SetTier(LocomotionTier.Idle);
            }

            if (ToggleButton("Walk", _tier == LocomotionTier.Walk))
            {
                SetTier(LocomotionTier.Walk);
            }

            if (ToggleButton("Run", _tier == LocomotionTier.Run))
            {
                SetTier(LocomotionTier.Run);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (ActionButton("Jump"))
            {
                _activeAnimator.SetTrigger(JumpParam);
            }

            if (ToggleButton("Dead", _dead))
            {
                _dead = !_dead;
                _activeAnimator.SetBool(DeadParam, _dead);
            }

            // B축 그랩 착수 전 포즈 확인 수단 — AC_Player에 파라미터만 선배치돼 있다 (계획 §1.2).
            if (ToggleButton("Grabbed", _grabbed))
            {
                _grabbed = !_grabbed;
                _activeAnimator.SetBool(GrabbedParam, _grabbed);
            }

            GUILayout.EndHorizontal();

            // 피치 전 구간에서 화면을 가리지 않아야 한다 — 훑으며 조정 (계획 §4-③).
            GUILayout.BeginHorizontal();
            GUILayout.Label($"피치 {_pitch:0.#}°", GUILayout.Width(80f));
            if (ActionButton("0", GUILayout.Width(28f)))
            {
                _pitch = 0f;
            }

            GUILayout.EndHorizontal();
            _pitch = GUILayout.HorizontalSlider(_pitch, -MaxPitch, MaxPitch);

            GUILayout.Label($"재생 속도 x{_playbackSpeed:0.0}");
            float speed = GUILayout.HorizontalSlider(_playbackSpeed, 0.1f, 2f);
            if (!Mathf.Approximately(speed, _playbackSpeed))
            {
                _playbackSpeed = speed;
                _activeAnimator.speed = speed;
            }
        }

        private void DrawCameraSection()
        {
            GUILayout.Label("── 카메라 ──");
            GUILayout.BeginHorizontal();
            if (ToggleButton("궤도 (TP 정합)", !_fpCamera) && _fpCamera)
            {
                SetFpCamera(false);
            }

            if (ToggleButton("FP (소유자 화면)", _fpCamera) && !_fpCamera)
            {
                SetFpCamera(true);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSaveSection()
        {
            GUILayout.Label("── 저장 ──");
#if UNITY_EDITOR
            if (ActionButton(_dirty ? "Player.prefab에 저장 ●" : "Player.prefab에 저장"))
            {
                SavePivotsToPrefab();
            }

            GUILayout.Label(_dirty
                ? "● 미저장 변경 — Play를 멈추면 씬 값은 사라진다"
                : "프리팹과 일치 (저장할 변경 없음)");
#else
            GUILayout.Label("(저장은 에디터에서만 가능)");
#endif
        }
    }
}
