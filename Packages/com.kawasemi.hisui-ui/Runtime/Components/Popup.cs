using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Hisui.UI.Components
{
    /// <summary>
    /// Target を初期位置からのオフセットスライド・拡大縮小・フェードの組み合わせで登場（In）させ、
    /// 時間経過・指定 <see cref="Button"/> のクリック・またはスクリプト呼び出しで退場（Out）させる演出コンポーネント。
    /// <para>
    /// <b>特徴：</b><br/>
    /// ・フェード・スケール・スライド（初期位置からのオフセット）の 3 種類を自由に組み合わせ可能。<br/>
    /// ・登場開始時（In）と退場終了時（Out）で別々のオフセット・スケール・アルファを指定可能（例：下から登場して上へ抜けて退場、拡大して登場し等倍のままフェード退場など）。<br/>
    /// ・指定した <see cref="CloseButton"/> のクリック、または In 完了後の時間経過（<see cref="Settings.autoOutDelay"/>）による自動退場に対応。<br/>
    /// ・開閉途中で逆方向が呼ばれた場合は、現在位置・現在スケール・現在アルファから途切れなく滑らかに反転する。<br/>
    /// ・複数要素を時間差（Stagger）で動かす場合は、各要素に本コンポーネントをアタッチし、親の <see cref="PopupGroup"/> で束ねて制御できる。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Popup")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class Popup : HisuiMotionBehaviour<Popup, Popup.Settings>
    {
        /// <summary>
        /// スライドのオフセット指定単位。
        /// </summary>
        public enum SlideOffsetUnit
        {
            /// <summary>ピクセル（アンカー座標）単位の絶対オフセット。</summary>
            Absolute = 0,

            /// <summary>Target の <see cref="RectTransform.rect"/> サイズに対する割合（1.0 = 幅または高さ 1 つ分）。</summary>
            RelativeToSize = 1,
        }

        /// <summary>
        /// <see cref="PopupGroup"/> から自動退場の挙動を上書き制御するための内部モード。
        /// </summary>
        internal enum GroupAutoOutOverride
        {
            None = 0,
            UseGroupDelay = 1,
            Suppress = 2,
        }

        /// <summary>
        /// <see cref="Popup"/> の全設定を保持するシリアライズ可能構造体。
        /// </summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>再生前は非表示（In 開始状態）にしておくか。</summary>
            [Header("Initial & Lifecycle")]
            [Tooltip("再生前は非表示状態にしておく（OnEnable 時点で配置しチラつきを防ぐ）")]
            public bool startHidden;

            /// <summary>退場（Out）完了時に GameObject.SetActive(false) を自動で呼ぶか。</summary>
            [Tooltip("退場完了時に GameObject.SetActive(false) を自動で呼ぶか")]
            public bool deactivateOnClose;

            /// <summary>自動再生トリガーが OnEnable のときの開始遅延（秒）。</summary>
            [Tooltip("自動再生トリガーが OnEnable のときの開始遅延（秒）")]
            [Min(0f)]
            public float startDelay;

            /// <summary>
            /// In 完了後に自動で退場するまでの待機秒数。負の値（-1）で自動退場しない。0 以上なら In 完了からその秒数待って Out を開始する。
            /// </summary>
            [Tooltip("In 完了後に自動退場するまでの待機秒数。-1 で自動退場しない、0 以上で In 完了からその秒数後に退場")]
            public float autoOutDelay;

            /// <summary>途中で反転した際、残り進捗に応じて Duration を短縮する。</summary>
            [Tooltip("途中で反転したとき、残り距離に応じて再生時間を短縮する")]
            public bool scaleDurationOnReverse;

            /// <summary>登場（In）のモーション設定。</summary>
            [Header("In / Out Motion")]
            [Tooltip("登場（In）のモーション設定（OutBack 等のオーバーシュートも可）")]
            public MotionSpec inMotion;

            /// <summary>退場（Out）のモーション設定。</summary>
            [Tooltip("退場（Out）のモーション設定")]
            public MotionSpec outMotion;

            /// <summary>フェード演出を行うか（Target の CanvasGroup、無ければ Graphic の alpha）。</summary>
            [Header("Fade")]
            [Tooltip("alpha フェードを併用する（Target の CanvasGroup、無ければ Graphic）")]
            public bool useFade;

            /// <summary>登場開始時の Alpha 値。</summary>
            [Tooltip("登場（In）開始時の Alpha")]
            [Range(0f, 1f)]
            public float inStartAlpha;

            /// <summary>true の場合、退場終了時の Alpha 値を <see cref="outEndAlpha"/> で別指定する。</summary>
            [Tooltip("true の場合、退場（Out）終了時の Alpha を別指定する")]
            public bool separateOutAlpha;

            /// <summary>退場終了時の Alpha 値（<see cref="separateOutAlpha"/> が true のとき有効）。</summary>
            [Tooltip("退場（Out）終了時の Alpha")]
            [Range(0f, 1f)]
            public float outEndAlpha;

            /// <summary>拡大縮小（スケール）演出を行うか。</summary>
            [Header("Scale")]
            [Tooltip("RectTransform.localScale による拡大縮小演出を行うか")]
            public bool useScale;

            /// <summary>登場開始時のスケール値（到着時は初期スケール）。</summary>
            [Tooltip("登場（In）開始時のスケール")]
            public Vector3 inStartScale;

            /// <summary>true の場合、退場終了時のスケール値を <see cref="outEndScale"/> で別指定する。</summary>
            [Tooltip("true の場合、退場（Out）終了時のスケールを別指定する（例: 等倍のままフェード退場など）")]
            public bool separateOutScale;

            /// <summary>退場終了時のスケール値（<see cref="separateOutScale"/> が true のとき有効）。</summary>
            [Tooltip("退場（Out）終了時のスケール")]
            public Vector3 outEndScale;

            /// <summary>初期位置からのオフセットによるスライド演出を行うか。</summary>
            [Header("Slide (Offset from Rest Position)")]
            [Tooltip("初期位置（基準位置）からのオフセットによるスライド演出を行うか")]
            public bool useSlide;

            /// <summary>オフセットの指定単位（ピクセル絶対値 / Target サイズ比）。</summary>
            [Tooltip("オフセットの単位（Absolute: ピクセル値 / RelativeToSize: Target サイズ比）")]
            public SlideOffsetUnit offsetUnit;

            /// <summary>登場開始時の初期位置からのオフセット。</summary>
            [Tooltip("登場（In）開始時のオフセット（例: (0, -40) で 40px 下から登場）")]
            public Vector2 inOffset;

            /// <summary>true の場合、退場先のオフセットを <see cref="outOffset"/> で別指定する。</summary>
            [Tooltip("true の場合、退場（Out）先のオフセットを別指定する（例: 下から登場して上へ退場）")]
            public bool separateOutOffset;

            /// <summary>退場終了時の初期位置からのオフセット（<see cref="separateOutOffset"/> が true のとき有効）。</summary>
            [Tooltip("退場（Out）終了時のオフセット")]
            public Vector2 outOffset;

            /// <summary>標準的なポップアップ向けのデフォルト設定。</summary>
            public static Settings Default => new Settings
            {
                startHidden = true,
                deactivateOnClose = false,
                startDelay = 0f,
                autoOutDelay = -1f,
                scaleDurationOnReverse = true,
                inMotion = new MotionSpec(0.3f, Ease.OutBack, 0f),
                outMotion = new MotionSpec(0.2f, Ease.InCubic, 0f),
                useFade = true,
                inStartAlpha = 0f,
                separateOutAlpha = false,
                outEndAlpha = 0f,
                useScale = true,
                inStartScale = new Vector3(0.8f, 0.8f, 1f),
                separateOutScale = false,
                outEndScale = new Vector3(0.8f, 0.8f, 1f),
                useSlide = true,
                offsetUnit = SlideOffsetUnit.Absolute,
                inOffset = new Vector2(0f, -40f),
                separateOutOffset = false,
                outOffset = new Vector2(0f, 40f),
            };
        }

        [Header("Target & Trigger (Optional)")]
        [Tooltip("アニメーション対象の RectTransform（未指定時は自身の RectTransform）")]
        [SerializeField]
        private RectTransform _target;

        [Tooltip("クリック時に退場（PlayOut）を開始する Button（任意）")]
        [SerializeField]
        private Button _closeButton;

        private Button _boundCloseButton;
        private CanvasGroup _canvasGroup;
        private Graphic _graphic;

        private Vector2 _restPos;
        private Vector3 _restScale = Vector3.one;
        private float _baseAlpha = 1f;
        private bool _restCaptured;
        private bool _initialized;

        private float _progress = 1f;
        private Vector2 _currentPos;
        private Vector3 _currentScale = Vector3.one;
        private float _currentAlpha = 1f;

        private float _fromProgress;
        private float _toProgress;
        private Vector2 _fromPos;
        private Vector2 _toPos;
        private Vector3 _fromScale;
        private Vector3 _toScale;
        private float _fromAlpha;
        private float _toAlpha;

        private bool _lastWasIn;
        private bool _atOutSide;
        private bool _waitingForInDelay;
        private bool _suppressOnEnableAutoPlay;

        private bool _autoOutArmed;
        private float _autoOutRemaining;

        private GroupAutoOutOverride _groupAutoOutOverride;
        private float _groupAutoOutDelay = -1f;

        private Action _onTransitionCompleteDelegate;

        private static readonly Action<float, Popup> s_tickCallback = static (e, self) => self.Tick(e);

        /// <summary>登場（In）アニメーションが完了したときに発火するイベント。</summary>
        public event Action<Popup> OnInCompleted;

        /// <summary>退場（Out）アニメーションが開始されたときに発火するイベント。</summary>
        public event Action<Popup> OnOutStarted;

        /// <summary>退場（Out）アニメーションが完了したときに発火するイベント。</summary>
        public event Action<Popup> OnOutCompleted;

        /// <summary><see cref="PopupGroup"/> の管理下にあるかどうか（OnEnable の個別自動再生を抑止するために使用）。</summary>
        internal bool ManagedByGroup { get; set; }

        /// <summary>制御対象の <see cref="RectTransform"/>（未指定時は自身の RectTransform）。</summary>
        public RectTransform Target => _target != null ? _target : (RectTransform)transform;

        /// <summary>退場トリガーとなる <see cref="Button"/>。</summary>
        public Button CloseButton
        {
            get => _closeButton;
            set
            {
                if (_closeButton == value)
                {
                    return;
                }
                UnbindCloseButton();
                _closeButton = value;
                if (isActiveAndEnabled && Application.isPlaying)
                {
                    BindCloseButton();
                }
            }
        }

        /// <summary>最後に要求された状態が In（登場）側かどうか。</summary>
        public bool IsIn => _lastWasIn;

        /// <summary>現在の進捗率（0 = 完全退場/非表示、1 = 到着完了）。</summary>
        public float Progress => _progress;

        /// <summary>現在表示中または登場アニメーション中（退場済みでない）かどうか。</summary>
        public bool IsVisibleOrEntering => _lastWasIn || _progress > 0.0001f || IsPlaying;

        private void Reset()
        {
            _target = GetComponent<RectTransform>();
            SettingsRef = Settings.Default;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            // 初回描画（PostLateUpdate）の前に一度可視状態で Canvas を強制ビルドし、
            // 透明カリング（m_CullTransparentMesh）による初回登場時の TMP / Image メッシュ欠落を防ぐ。
            if (_progress <= 0.0001f && gameObject.activeInHierarchy)
            {
                WarmupCanvasWhileVisible();
                ApplyCurrentVisuals();
            }

            if (!_lastWasIn && !IsPlaying && gameObject.activeSelf && AutoPlayTrigger == PlayTrigger.None && !ManagedByGroup)
            {
                if (SettingsRef.deactivateOnClose)
                {
                    DeactivateTargetAndSelf();
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            BindCloseButton();

            if (_suppressOnEnableAutoPlay)
            {
                return;
            }

            if (SettingsRef.startHidden && !_lastWasIn && !IsPlaying)
            {
                SetHiddenInternal(atOutSide: false);
            }

            if (!ManagedByGroup && AutoPlayTrigger == PlayTrigger.OnEnable)
            {
                CancelTrackedMotionsAndIncrementVersion();
                StartTransition(toIn: true, extraDelay: Mathf.Max(0f, SettingsRef.startDelay));
            }
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            UnbindCloseButton();
            base.OnDisable();
        }

        /// <inheritdoc />
        public override void Stop()
        {
            _autoOutArmed = false;
            _waitingForInDelay = false;
            base.Stop();
        }

        private void Update()
        {
            if (!_autoOutArmed)
            {
                return;
            }

            _autoOutRemaining -= IgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_autoOutRemaining <= 0f)
            {
                _autoOutArmed = false;
                PlayOut();
            }
        }

        /// <summary><see cref="HisuiMotionBehaviour.Play"/> は <see cref="PlayIn"/> と同義。</summary>
        protected override void OnPlayCore()
        {
            PrepareForInIfInactive();
            StartTransition(toIn: true, extraDelay: 0f);
        }

        /// <summary>
        /// 登場（In）アニメーションを開始する。<see cref="HisuiMotionBehaviour.Play"/> と同義。
        /// 非アクティブ時は自動的にアクティブ化して演出を開始する。退場の途中であれば現在状態から滑らかに反転する。
        /// </summary>
        public void PlayIn()
        {
            PrepareForInIfInactive();
            Play();
        }

        /// <summary>
        /// 退場（Out）アニメーションを開始する。登場の途中であれば現在状態から滑らかに反転する。
        /// 非アクティブ時はアニメーションせずに即座に退場完了状態にする。
        /// </summary>
        public void PlayOut()
        {
            PlayOutWithExtraDelay(0f);
        }

        /// <summary>
        /// 登場（In）を開始し、完了するまで待機する。中断・上書き・破棄時も例外を投げず静かに完了する。
        /// </summary>
        public UniTask PlayInAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            PlayIn();
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        /// <summary>
        /// 退場（Out）を開始し、完了するまで待機する。中断・上書き・破棄時も例外を投げず静かに完了する。
        /// </summary>
        public UniTask PlayOutAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            PlayOut();
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        /// <summary><see cref="PlayInAsync"/> と同義。</summary>
        public override UniTask PlayAsync(CancellationToken ct = default)
        {
            return PlayInAsync(ct);
        }

        /// <summary>
        /// <see cref="PopupGroup"/> から Stagger 遅延付きで登場を開始するための内部メソッド。
        /// </summary>
        internal void PlayInWithExtraDelay(float extraDelay)
        {
            PrepareForInIfInactive();
            CancelTrackedMotionsAndIncrementVersion();
            StartTransition(toIn: true, extraDelay: Mathf.Max(0f, extraDelay));
        }

        /// <summary>
        /// <see cref="PopupGroup"/> から Stagger 遅延付きで退場を開始するための内部メソッド。
        /// </summary>
        internal void PlayOutWithExtraDelay(float extraDelay)
        {
            EnsureInitialized();

            var targetRt = Target;
            if (!gameObject.activeInHierarchy || (targetRt != null && !targetRt.gameObject.activeInHierarchy))
            {
                SetOutImmediate();
                return;
            }

            CancelTrackedMotionsAndIncrementVersion();
            StartTransition(toIn: false, extraDelay: Mathf.Max(0f, extraDelay));
        }

        /// <summary>
        /// <see cref="PopupGroup"/> から自動退場の上書き設定を適用する。
        /// </summary>
        internal void SetGroupAutoOutOverride(GroupAutoOutOverride mode, float delay)
        {
            _groupAutoOutOverride = mode;
            _groupAutoOutDelay = delay;
        }

        /// <summary>演出なしで即座に到着（In 完了）状態にする。非アクティブ時は自動でアクティブ化する。</summary>
        public void SetInImmediate()
        {
            _autoOutArmed = false;
            _waitingForInDelay = false;
            CancelTrackedMotionsAndIncrementVersion();

            ActivateTargetAndSelfIfInactive(out bool wasInactive);
            EnsureInitialized();

            if (wasInactive)
            {
                Canvas.ForceUpdateCanvases();
            }

            _progress = 1f;
            _lastWasIn = true;
            _atOutSide = false;
            _currentPos = _restPos;
            _currentScale = _restScale;
            _currentAlpha = _baseAlpha;

            ApplyCurrentVisuals();
            ApplyCanvasGroupInteractable(interactable: true, blocksRaycasts: true);
        }

        /// <summary>演出なしで即座に退場（Out 完了）状態にする。<see cref="Settings.deactivateOnClose"/> が有効な場合は非アクティブ化する。</summary>
        public void SetOutImmediate()
        {
            SetHiddenInternal(atOutSide: true);

            if (SettingsRef.deactivateOnClose && Application.isPlaying)
            {
                DeactivateTargetAndSelf();
            }
        }

        /// <summary>
        /// 現在の <see cref="Target"/> の <c>anchoredPosition</c>・<c>localScale</c>・<c>alpha</c> を到着時の基準状態として再取得する。
        /// <see cref="SetInImmediate"/> の後など、到着状態にあるときに呼ぶこと。
        /// </summary>
        public Popup CaptureRestState()
        {
            var rt = Target;
            if (rt == null)
            {
                return this;
            }

            CacheTargetComponents(rt);
            _restPos = rt.anchoredPosition;
            Vector3 scale = rt.localScale;
            _restScale = scale.sqrMagnitude > 0.00001f ? scale : Vector3.one;

            float a = ReadAlpha();
            _baseAlpha = a > 0.001f ? a : 1f;
            _restCaptured = true;

            if (_progress >= 0.999f)
            {
                _currentPos = _restPos;
                _currentScale = _restScale;
                _currentAlpha = _baseAlpha;
            }

            return this;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            var rt = Target;
            if (rt != null)
            {
                CacheTargetComponents(rt);
            }

            if (!_restCaptured)
            {
                Canvas.ForceUpdateCanvases();
                CaptureRestState();
            }

            _initialized = true;
            _lastWasIn = !SettingsRef.startHidden;

            if (SettingsRef.startHidden)
            {
                SetHiddenInternal(atOutSide: false);
            }
            else
            {
                _progress = 1f;
                _currentPos = _restPos;
                _currentScale = _restScale;
                _currentAlpha = _baseAlpha;
                ApplyCurrentVisuals();
                ApplyCanvasGroupInteractable(interactable: true, blocksRaycasts: true);
            }
        }

        private void CacheTargetComponents(RectTransform rt)
        {
            if (!rt.TryGetComponent(out _canvasGroup))
            {
                _canvasGroup = null;
            }

            if (!rt.TryGetComponent(out _graphic))
            {
                _graphic = null;
            }
        }

        private void SetHiddenInternal(bool atOutSide)
        {
            _autoOutArmed = false;
            _waitingForInDelay = false;
            CancelTrackedMotionsAndIncrementVersion();

            if (!_restCaptured)
            {
                CaptureRestState();
            }

            _progress = 0f;
            _lastWasIn = false;
            _atOutSide = atOutSide;
            _currentPos = _restPos + (atOutSide ? GetOutPixelOffset() : GetInPixelOffset());
            _currentScale = atOutSide ? GetOutScale() : GetInScale();
            _currentAlpha = atOutSide ? GetOutAlpha() : GetInAlpha();

            ApplyCurrentVisuals();
            ApplyCanvasGroupInteractable(interactable: false, blocksRaycasts: false);
        }

        private void StartTransition(bool toIn, float extraDelay)
        {
            _autoOutArmed = false;
            EnsureInitialized();

            ref var s = ref SettingsRef;

            // すでに到着済みの状態から再度 PlayIn が呼ばれた場合、または完全に非表示の状態から PlayIn が呼ばれた場合は、
            // 登場元の初期オフセット・初期スケール・初期アルファから開始する。
            // Out の途中で PlayIn が呼ばれた場合（反転）は現在の状態から滑らかに戻る。
            if (toIn && ((_lastWasIn && _progress >= 0.9999f) || _progress <= 0.0001f))
            {
                _progress = 0f;
                _atOutSide = false;
                _currentPos = _restPos + GetInPixelOffset();
                _currentScale = GetInScale();
                _currentAlpha = GetInAlpha();
            }

            _lastWasIn = toIn;

            float targetProgress = toIn ? 1f : 0f;
            float distance = Mathf.Abs(targetProgress - _progress);

            _fromProgress = _progress;
            _toProgress = targetProgress;
            _fromPos = _currentPos;
            _toPos = toIn ? _restPos : _restPos + GetOutPixelOffset();
            _fromScale = _currentScale;
            _toScale = toIn ? _restScale : GetOutScale();
            _fromAlpha = _currentAlpha;
            _toAlpha = toIn ? _baseAlpha : GetOutAlpha();

            MotionSpec spec = toIn ? s.inMotion : s.outMotion;
            float totalDelay = Mathf.Max(0f, spec.delay + extraDelay);
            float duration = s.scaleDurationOnReverse
                ? spec.duration * Mathf.Min(1f, distance)
                : spec.duration;

            if (toIn)
            {
                // 登場遅延中に画面上へ見えてしまわないよう状態を反映する
                _waitingForInDelay = totalDelay > 0f && _fromProgress <= 0.0001f;
                ApplyCurrentVisuals();
                ApplyCanvasGroupInteractable(interactable: true, blocksRaycasts: true);
            }
            else
            {
                _waitingForInDelay = false;
                ApplyCanvasGroupInteractable(interactable: false, blocksRaycasts: true);
                OnOutStarted?.Invoke(this);
            }

            if (distance <= 0.0001f || (duration <= 0f && totalDelay <= 0f))
            {
                FinishTransition();
                return;
            }

            spec.duration = Mathf.Max(0.0001f, duration);
            spec.delay = totalDelay;

            _onTransitionCompleteDelegate ??= FinishTransition;

            var handle = HisuiMotionExtensions
                .CreateFloat(0f, 1f, in spec, IgnoreTimeScale)
                .WithOnComplete(_onTransitionCompleteDelegate)
                .Bind(this, s_tickCallback);

            TrackMotion(handle);
        }

        private void Tick(float e)
        {
            if (this == null)
            {
                return;
            }

            _waitingForInDelay = false;

            // Ease.OutBack 等のオーバーシュートを保持するため、位置とスケールは LerpUnclamped で補間する
            _progress = Mathf.Clamp01(Mathf.LerpUnclamped(_fromProgress, _toProgress, e));
            _currentPos = Vector2.LerpUnclamped(_fromPos, _toPos, e);
            _currentScale = Vector3.LerpUnclamped(_fromScale, _toScale, e);
            _currentAlpha = Mathf.Clamp01(Mathf.LerpUnclamped(_fromAlpha, _toAlpha, e));

            ApplyCurrentVisuals();
        }

        private void FinishTransition()
        {
            if (this == null)
            {
                return;
            }

            _waitingForInDelay = false;
            _progress = _toProgress;
            _currentPos = _toPos;
            _currentScale = _toScale;
            _currentAlpha = _toAlpha;
            _atOutSide = !_lastWasIn;

            ApplyCurrentVisuals();

            if (_lastWasIn)
            {
                ApplyCanvasGroupInteractable(interactable: true, blocksRaycasts: true);
                ArmAutoOutIfNeeded();
                OnInCompleted?.Invoke(this);
            }
            else
            {
                ApplyCanvasGroupInteractable(interactable: false, blocksRaycasts: false);
                if (SettingsRef.deactivateOnClose && Application.isPlaying)
                {
                    DeactivateTargetAndSelf();
                }
                OnOutCompleted?.Invoke(this);
            }
        }

        private void ArmAutoOutIfNeeded()
        {
            if (_groupAutoOutOverride == GroupAutoOutOverride.Suppress)
            {
                return;
            }

            float delay = _groupAutoOutOverride == GroupAutoOutOverride.UseGroupDelay
                ? _groupAutoOutDelay
                : SettingsRef.autoOutDelay;

            if (delay >= 0f)
            {
                _autoOutArmed = true;
                _autoOutRemaining = delay;
            }
        }

        private void ApplyCurrentVisuals()
        {
            var rt = Target;
            if (rt == null)
            {
                return;
            }

            rt.anchoredPosition = SettingsRef.useSlide ? _currentPos : _restPos;

            // フェード不使用かつ非表示待機中（または Stagger の登場遅延待機中）に画面上に残らないよう、
            // 完全閉状態または登場遅延待機中はスケールを 0 に退避して不可視化する。
            bool forceHideByScale = (_waitingForInDelay || (!_lastWasIn && !IsPlaying && _progress <= 0.0001f))
                && (!SettingsRef.useFade || _currentAlpha > 0.001f || (_canvasGroup == null && _graphic == null));

            if (forceHideByScale)
            {
                rt.localScale = Vector3.zero;
            }
            else
            {
                rt.localScale = SettingsRef.useScale ? _currentScale : _restScale;
            }

            if (SettingsRef.useFade)
            {
                WriteAlpha(_waitingForInDelay ? 0f : _currentAlpha);
            }
            else
            {
                WriteAlpha(_baseAlpha);
            }
        }

        private void ApplyCanvasGroupInteractable(bool interactable, bool blocksRaycasts)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        private Vector2 GetInPixelOffset()
        {
            return CalculatePixelOffset(SettingsRef.inOffset);
        }

        private Vector2 GetOutPixelOffset()
        {
            ref var s = ref SettingsRef;
            return CalculatePixelOffset(s.separateOutOffset ? s.outOffset : s.inOffset);
        }

        private Vector2 CalculatePixelOffset(Vector2 rawOffset)
        {
            if (!SettingsRef.useSlide)
            {
                return Vector2.zero;
            }

            if (SettingsRef.offsetUnit == SlideOffsetUnit.RelativeToSize)
            {
                var rt = Target;
                if (rt != null)
                {
                    Rect r = rt.rect;
                    return new Vector2(rawOffset.x * r.width, rawOffset.y * r.height);
                }
            }

            return rawOffset;
        }

        private Vector3 GetInScale()
        {
            return SettingsRef.useScale ? SettingsRef.inStartScale : _restScale;
        }

        private Vector3 GetOutScale()
        {
            ref var s = ref SettingsRef;
            if (!s.useScale)
            {
                return _restScale;
            }
            return s.separateOutScale ? s.outEndScale : s.inStartScale;
        }

        private float GetInAlpha()
        {
            return SettingsRef.useFade ? Mathf.Clamp01(SettingsRef.inStartAlpha) : _baseAlpha;
        }

        private float GetOutAlpha()
        {
            ref var s = ref SettingsRef;
            if (!s.useFade)
            {
                return _baseAlpha;
            }
            return Mathf.Clamp01(s.separateOutAlpha ? s.outEndAlpha : s.inStartAlpha);
        }

        private float ReadAlpha()
        {
            if (_canvasGroup != null) return _canvasGroup.alpha;
            if (_graphic != null) return _graphic.color.a;
            return 1f;
        }

        private void WriteAlpha(float alpha)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = alpha;
            }
            else if (_graphic != null)
            {
                Color c = _graphic.color;
                if (!Mathf.Approximately(c.a, alpha))
                {
                    c.a = alpha;
                    _graphic.color = c;
                }
            }
        }

        private void PrepareForInIfInactive()
        {
            ActivateTargetAndSelfIfInactive(out bool wasInactive);
            EnsureInitialized();

            if (wasInactive || _progress <= 0.0001f)
            {
                WarmupCanvasWhileVisible();
                ApplyCurrentVisuals();
            }
        }

        private void ActivateTargetAndSelfIfInactive(out bool wasInactive)
        {
            wasInactive = false;

            if (!gameObject.activeSelf)
            {
                wasInactive = true;
                _suppressOnEnableAutoPlay = true;
                gameObject.SetActive(true);
                _suppressOnEnableAutoPlay = false;
            }

            var rt = Target;
            if (rt != null && !rt.gameObject.activeSelf)
            {
                wasInactive = true;
                _suppressOnEnableAutoPlay = true;
                rt.gameObject.SetActive(true);
                _suppressOnEnableAutoPlay = false;
            }
        }

        private void DeactivateTargetAndSelf()
        {
            var rt = Target;
            if (rt != null && rt.gameObject != gameObject && rt.gameObject.activeSelf)
            {
                rt.gameObject.SetActive(false);
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void WarmupCanvasWhileVisible()
        {
            var rt = Target;
            if (rt == null)
            {
                return;
            }

            CacheTargetComponents(rt);

            Vector3 prevScale = rt.localScale;
            float prevAlpha = ReadAlpha();

            rt.localScale = _restScale;
            WriteAlpha(1f);

            Canvas.ForceUpdateCanvases();

            rt.localScale = prevScale;
            WriteAlpha(prevAlpha);
        }

        private void BindCloseButton()
        {
            if (_boundCloseButton == _closeButton)
            {
                return;
            }

            UnbindCloseButton();

            if (_closeButton != null)
            {
                _boundCloseButton = _closeButton;
                _boundCloseButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        private void UnbindCloseButton()
        {
            if (_boundCloseButton != null)
            {
                _boundCloseButton.onClick.RemoveListener(OnCloseButtonClicked);
                _boundCloseButton = null;
            }
        }

        private void OnCloseButtonClicked()
        {
            if (_lastWasIn || _progress > 0.0001f)
            {
                PlayOut();
            }
        }

        #region Fluent Configuration API

        /// <summary>アニメーション対象の <see cref="RectTransform"/> を設定する（メソッドチェーン対応）。</summary>
        public Popup SetTarget(RectTransform target)
        {
            _target = target;
            _restCaptured = false;
            if (_initialized && target != null)
            {
                CaptureRestState();
            }
            return this;
        }

        /// <summary>クリック時に退場させる <see cref="Button"/> を設定する（メソッドチェーン対応）。</summary>
        public Popup SetCloseButton(Button closeButton)
        {
            CloseButton = closeButton;
            return this;
        }

        /// <summary>再生前に非表示にしておくかを設定する（メソッドチェーン対応）。</summary>
        public Popup SetStartHidden(bool startHidden)
        {
            SettingsRef.startHidden = startHidden;
            return this;
        }

        /// <summary>退場完了時に GameObject.SetActive(false) を呼ぶかを設定する（メソッドチェーン対応）。</summary>
        public Popup SetDeactivateOnClose(bool deactivate)
        {
            SettingsRef.deactivateOnClose = deactivate;
            return this;
        }

        /// <summary>OnEnable 自動再生時の開始遅延（秒）を設定する（メソッドチェーン対応）。</summary>
        public Popup SetStartDelay(float delay)
        {
            SettingsRef.startDelay = Mathf.Max(0f, delay);
            return this;
        }

        /// <summary>In 完了後の自動退場までの待機秒数を設定する。-1 で自動退場しない（メソッドチェーン対応）。</summary>
        public Popup SetAutoOut(float delay)
        {
            SettingsRef.autoOutDelay = delay < 0f ? -1f : delay;
            return this;
        }

        /// <summary>途中反転時に残り距離に応じて再生時間を短縮するかを設定する（メソッドチェーン対応）。</summary>
        public Popup SetScaleDurationOnReverse(bool scaleDuration)
        {
            SettingsRef.scaleDurationOnReverse = scaleDuration;
            return this;
        }

        /// <summary>登場（In）の <see cref="MotionSpec"/> を設定する（メソッドチェーン対応）。</summary>
        public Popup SetInMotion(in MotionSpec spec)
        {
            SettingsRef.inMotion = spec;
            return this;
        }

        /// <summary>登場（In）の Duration / Ease / Delay を設定する（メソッドチェーン対応）。</summary>
        public Popup SetInMotion(float duration, Ease ease = Ease.OutBack, float delay = 0f)
        {
            SettingsRef.inMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>退場（Out）の <see cref="MotionSpec"/> を設定する（メソッドチェーン対応）。</summary>
        public Popup SetOutMotion(in MotionSpec spec)
        {
            SettingsRef.outMotion = spec;
            return this;
        }

        /// <summary>退場（Out）の Duration / Ease / Delay を設定する（メソッドチェーン対応）。</summary>
        public Popup SetOutMotion(float duration, Ease ease = Ease.InCubic, float delay = 0f)
        {
            SettingsRef.outMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>フェード演出の有効・無効と開始 Alpha を設定する（メソッドチェーン対応）。</summary>
        public Popup SetFade(bool enabled, float inStartAlpha = 0f)
        {
            ref var s = ref SettingsRef;
            s.useFade = enabled;
            s.inStartAlpha = Mathf.Clamp01(inStartAlpha);
            s.separateOutAlpha = false;
            return this;
        }

        /// <summary>フェード演出の登場開始 Alpha と退場終了 Alpha を個別に設定する（メソッドチェーン対応）。</summary>
        public Popup SetFade(bool enabled, float inStartAlpha, float outEndAlpha)
        {
            ref var s = ref SettingsRef;
            s.useFade = enabled;
            s.inStartAlpha = Mathf.Clamp01(inStartAlpha);
            s.separateOutAlpha = true;
            s.outEndAlpha = Mathf.Clamp01(outEndAlpha);
            return this;
        }

        /// <summary>スケール演出の有効・無効と開始スケールを設定する（メソッドチェーン対応）。</summary>
        public Popup SetScale(bool enabled, Vector3 inStartScale)
        {
            ref var s = ref SettingsRef;
            s.useScale = enabled;
            s.inStartScale = inStartScale;
            s.separateOutScale = false;
            return this;
        }

        /// <summary>スケール演出の有効・無効と均一な開始スケール値を設定する（メソッドチェーン対応）。</summary>
        public Popup SetScale(bool enabled, float uniformInStartScale = 0.8f)
        {
            return SetScale(enabled, new Vector3(uniformInStartScale, uniformInStartScale, 1f));
        }

        /// <summary>スケール演出の登場開始スケールと退場終了スケールを個別に設定する（メソッドチェーン対応）。</summary>
        public Popup SetScale(bool enabled, Vector3 inStartScale, Vector3 outEndScale)
        {
            ref var s = ref SettingsRef;
            s.useScale = enabled;
            s.inStartScale = inStartScale;
            s.separateOutScale = true;
            s.outEndScale = outEndScale;
            return this;
        }

        /// <summary>オフセットスライド演出の有効・無効と登場オフセットを設定する（メソッドチェーン対応）。</summary>
        public Popup SetSlide(
            bool enabled,
            Vector2 inOffset,
            SlideOffsetUnit unit = SlideOffsetUnit.Absolute)
        {
            ref var s = ref SettingsRef;
            s.useSlide = enabled;
            s.inOffset = inOffset;
            s.offsetUnit = unit;
            s.separateOutOffset = false;
            return this;
        }

        /// <summary>オフセットスライド演出の登場オフセットと退場オフセットを個別に設定する（メソッドチェーン対応）。</summary>
        public Popup SetSlide(
            bool enabled,
            Vector2 inOffset,
            Vector2 outOffset,
            SlideOffsetUnit unit = SlideOffsetUnit.Absolute)
        {
            ref var s = ref SettingsRef;
            s.useSlide = enabled;
            s.inOffset = inOffset;
            s.outOffset = outOffset;
            s.offsetUnit = unit;
            s.separateOutOffset = true;
            return this;
        }

        #endregion
    }
}
