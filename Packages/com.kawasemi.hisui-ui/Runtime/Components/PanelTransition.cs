using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;

namespace Hisui.UI.Components
{
    /// <summary>
    /// パネルの開閉演出（フェード / スケール / スライドの組み合わせ）と <see cref="CanvasGroup"/> の状態管理を行うコンポーネント。
    /// <para>
    /// <b>特徴：</b><br/>
    /// ・フェード・スケール・スライド（上下左右 / パネルサイズ基準または絶対ピクセル値）を自由に組み合わせ可能。<br/>
    /// ・Open と Close で個別の <see cref="MotionSpec"/>（Duration / Ease / Delay / CustomCurve）を設定可能。<br/>
    /// ・開閉の途中で逆方向（Open 中に Close、Close 中に Open）が呼ばれた場合、現在の進捗位置から途切れなく滑らかに反転する。<br/>
    /// ・<see cref="CanvasGroup"/> の <c>alpha</c> / <c>interactable</c> / <c>blocksRaycasts</c> および閉完了時の <c>GameObject.SetActive(false)</c> を自動制御する。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Panel Transition")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PanelTransition : HisuiMotionBehaviour<PanelTransition, PanelTransition.Settings>
    {
        /// <summary>
        /// パネルのスライド方向（閉じているときにどの方向へ退避しているか）。
        /// </summary>
        public enum SlideDirection
        {
            /// <summary>上方向。</summary>
            Up = 0,

            /// <summary>下方向。</summary>
            Down = 1,

            /// <summary>左方向。</summary>
            Left = 2,

            /// <summary>右方向。</summary>
            Right = 3,

            /// <summary>任意のカスタム方向ベクトル（<see cref="Settings.customSlideVector"/> を使用）。</summary>
            Custom = 4,
        }

        /// <summary>
        /// スライド移動量の指定単位。
        /// </summary>
        public enum SlideUnit
        {
            /// <summary>自身の <see cref="RectTransform.rect"/> のサイズに対する倍率（1.0 = パネル1つ分）。</summary>
            RelativeToPanelSize = 0,

            /// <summary>絶対値（ピクセル / アンカー座標単位）。</summary>
            Absolute = 1,
        }

        /// <summary>
        /// 開く演出時に <see cref="CanvasGroup.interactable"/> を <c>true</c> にするタイミング。
        /// </summary>
        public enum InteractableTiming
        {
            /// <summary>開く演出が完了した時点で操作可能にする（推奨・デフォルト）。</summary>
            OnOpenComplete = 0,

            /// <summary>開く演出の開始と同時に操作可能にする。</summary>
            OnOpenStart = 1,
        }

        /// <summary>
        /// 起動時の初期開閉状態。
        /// </summary>
        public enum InitialStateMode
        {
            /// <summary>開いた状態で初期化する。</summary>
            Open = 0,

            /// <summary>閉じた状態で初期化する。</summary>
            Closed = 1,
        }

        /// <summary>
        /// <see cref="PanelTransition"/> の全設定を保持するシリアライズ可能構造体。
        /// </summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>起動時の初期状態（Open / Closed）。</summary>
            [Header("Initial & Lifecycle")]
            [Tooltip("Awake 時の初期状態（Open / Closed）")]
            public InitialStateMode initialState;

            /// <summary>開く演出時に <c>CanvasGroup.interactable = true</c> に切り替えるタイミング。</summary>
            [Tooltip("開く演出時に interactable = true にするタイミング")]
            public InteractableTiming interactableTiming;

            /// <summary>閉じる演出が完了した際に <c>gameObject.SetActive(false)</c> にするかどうか。</summary>
            [Tooltip("true の場合、閉じる演出完了時に GameObject.SetActive(false) にする")]
            public bool deactivateOnClose;

            /// <summary>開閉途中で反転した際、残りの進捗割合に応じて Duration を短縮するかどうか。</summary>
            [Tooltip("true の場合、開閉途中で反転した際に残り距離に応じて再生時間を調整する")]
            public bool scaleDurationOnReverse;

            /// <summary>開くときのモーション設定（Duration / Ease / Delay）。</summary>
            [Header("Open / Close Motion")]
            [Tooltip("開くときのモーション設定")]
            public MotionSpec openMotion;

            /// <summary>閉じるときのモーション設定（Duration / Ease / Delay）。</summary>
            [Tooltip("閉じるときのモーション設定")]
            public MotionSpec closeMotion;

            /// <summary>フェード演出を有効にするか。</summary>
            [Header("Fade")]
            [Tooltip("CanvasGroup.alpha によるフェード演出を行うか")]
            public bool useFade;

            /// <summary>開いているときの Alpha 値。</summary>
            [Tooltip("開いているときの Alpha")]
            [Range(0f, 1f)]
            public float openAlpha;

            /// <summary>閉じているときの Alpha 値。</summary>
            [Tooltip("閉じているときの Alpha")]
            [Range(0f, 1f)]
            public float closedAlpha;

            /// <summary>スケール演出を有効にするか。</summary>
            [Header("Scale")]
            [Tooltip("RectTransform.localScale によるスケール演出を行うか")]
            public bool useScale;

            /// <summary>閉じているときのスケール値（開いているときは初期スケールまたは指定した基準スケール）。</summary>
            [Tooltip("閉じているときのスケール")]
            public Vector3 closedScale;

            /// <summary>スライド演出を有効にするか。</summary>
            [Header("Slide")]
            [Tooltip("RectTransform.anchoredPosition によるスライド演出を行うか")]
            public bool useSlide;

            /// <summary>閉じているときに退避する方向。</summary>
            [Tooltip("閉じているときの退避方向")]
            public SlideDirection slideDirection;

            /// <summary>スライド移動量の単位（パネルサイズ比 / 絶対値）。</summary>
            [Tooltip("スライド移動量の単位（RelativeToPanelSize: パネルサイズ比 / Absolute: ピクセル値）")]
            public SlideUnit slideUnit;

            /// <summary>スライド移動量（<see cref="SlideDirection.Custom"/> 以外で使用）。</summary>
            [Tooltip("スライド移動量（RelativeToPanelSize 時は倍率、Absolute 時はピクセル距離）")]
            public float slideDistance;

            /// <summary><see cref="slideDirection"/> が <see cref="SlideDirection.Custom"/> のときに使用するオフセットベクトル。</summary>
            [Tooltip("SlideDirection.Custom 選択時の退避オフセットベクトル")]
            public Vector2 customSlideVector;

            /// <summary>
            /// 標準的なポップアップ・パネル向けのデフォルト設定を返す。
            /// </summary>
            public static Settings Default => new Settings
            {
                initialState = InitialStateMode.Open,
                interactableTiming = InteractableTiming.OnOpenComplete,
                deactivateOnClose = false,
                scaleDurationOnReverse = true,
                openMotion = new MotionSpec(0.25f, Ease.OutCubic, 0f),
                closeMotion = new MotionSpec(0.20f, Ease.InCubic, 0f),
                useFade = true,
                openAlpha = 1f,
                closedAlpha = 0f,
                useScale = true,
                closedScale = new Vector3(0.92f, 0.92f, 1f),
                useSlide = false,
                slideDirection = SlideDirection.Down,
                slideUnit = SlideUnit.RelativeToPanelSize,
                slideDistance = 0.15f,
                customSlideVector = new Vector2(0f, -60f),
            };
        }

        [Header("Target Components (Optional: Auto-fetched if null)")]
        [Tooltip("移動・スケール対象の RectTransform（未指定時は自身の RectTransform）")]
        [SerializeField]
        private RectTransform _rectTransform;

        [Tooltip("フェード・入力制御対象の CanvasGroup（未指定時は自身の CanvasGroup）")]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        private Vector2 _baseAnchoredPosition;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseStateCaptured;
        private bool _initialized;
        private bool _isOpen = true;
        private float _progress = 1f;
        private bool _suppressOnEnableAutoPlay;

        private static readonly Action<float, PanelTransition> s_updateProgressCallback = static (p, self) =>
        {
            self.ApplyVisualProgress(p);
        };

        private Action _onTransitionCompleteDelegate;

        /// <summary>
        /// 現在パネルが開状態（または開く演出中）かどうかを返す。
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// 現在の開閉進捗率（0 = 完全閉、1 = 完全開）。
        /// </summary>
        public float Progress => _progress;

        /// <summary>
        /// 制御対象の <see cref="RectTransform"/>。
        /// </summary>
        public RectTransform TargetRectTransform
        {
            get
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }
                return _rectTransform;
            }
        }

        /// <summary>
        /// 制御対象の <see cref="CanvasGroup"/>。
        /// </summary>
        public CanvasGroup TargetCanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }

        private void Reset()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            SettingsRef = Settings.Default;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            // InitialState が Closed の場合、全子要素の Graphic / TMP / uPalette Synchronizer の OnEnable が完了した Start 時点で
            // 一度 alpha > 0 の状態で Canvas を強制ビルドし、初回 Open 時にメッシュ構築・マテリアル同期・RectTransform 計算が
            // スキップされるのを防ぐ（Start はフレーム 0 の描画前に実行されるため画面には一切表示されない）。
            if (!_isOpen && !IsPlaying && gameObject.activeSelf && AutoPlayTrigger == PlayTrigger.None)
            {
                WarmupCanvasWhileVisible();
                ApplyVisualProgress(0f);
                ApplyCanvasGroupStateOnComplete(false);

                if (SettingsRef.deactivateOnClose)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            EnsureInitialized();
            if (_suppressOnEnableAutoPlay)
            {
                return;
            }
            base.OnEnable();
        }

        /// <summary>
        /// <see cref="HisuiMotionBehaviour.Play"/> または <c>OnEnable</c> 自動再生が呼ばれた際は、パネルを開く演出を実行する。
        /// </summary>
        protected override void OnPlayCore()
        {
            EnsureInitialized();
            PrepareForOpenIfClosed();
            StartTransitionInternal(open: true, forceFromClosedIfAlreadyOpen: true);
        }

        /// <summary>
        /// パネルを開く演出を開始する。すでに閉じている途中の場合は現在位置から滑らかに反転して開く。
        /// </summary>
        public void Open()
        {
            EnsureInitialized();
            PrepareForOpenIfClosed();

            CancelTrackedMotionsAndIncrementVersion();
            StartTransitionInternal(open: true, forceFromClosedIfAlreadyOpen: false);
        }

        private void PrepareForOpenIfClosed()
        {
            bool wasInactive = !gameObject.activeSelf;
            if (wasInactive)
            {
                _suppressOnEnableAutoPlay = true;
                gameObject.SetActive(true);
                _suppressOnEnableAutoPlay = false;
            }

            if (wasInactive || _progress <= 0.0001f)
            {
                WarmupCanvasWhileVisible();
                ApplyVisualProgress(_progress);
            }
        }

        private void WarmupCanvasWhileVisible()
        {
            var cg = TargetCanvasGroup;
            if (cg != null)
            {
                float prevAlpha = cg.alpha;
                cg.alpha = 1f;
                Canvas.ForceUpdateCanvases();
                cg.alpha = prevAlpha;
            }
            else
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// パネルを閉じる演出を開始する。すでに開いている途中の場合は現在位置から滑らかに反転して閉じる。
        /// </summary>
        public void Close()
        {
            EnsureInitialized();

            if (!gameObject.activeInHierarchy)
            {
                SetOpenImmediate(false);
                return;
            }

            CancelTrackedMotionsAndIncrementVersion();
            StartTransitionInternal(open: false, forceFromClosedIfAlreadyOpen: false);
        }

        /// <summary>
        /// パネルの開閉状態をトグル（開いていれば Close、閉じていれば Open）する。
        /// </summary>
        public void Toggle()
        {
            EnsureInitialized();
            if (_isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>
        /// パネルを開く演出を開始し、完了するまで待機する。
        /// 中断・上書き・破棄時は例外をスローせず静かに完了する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        public UniTask OpenAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            Open();
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        /// <summary>
        /// パネルを閉じる演出を開始し、完了するまで待機する。
        /// 中断・上書き・破棄時は例外をスローせず静かに完了する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        public UniTask CloseAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            Close();
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        /// <summary>
        /// パネルの開閉をトグルし、演出が完了するまで待機する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        public UniTask ToggleAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            return _isOpen ? CloseAsync(ct) : OpenAsync(ct);
        }

        /// <summary>
        /// アニメーションなしで即座に開状態または閉状態へ切り替える。
        /// </summary>
        /// <param name="isOpen">true で開状態、false で閉状態。</param>
        public void SetOpenImmediate(bool isOpen)
        {
            CaptureBaseTransformIfNeeded();
            _initialized = true;

            CancelTrackedMotionsAndIncrementVersion();
            _isOpen = isOpen;

            if (isOpen && !gameObject.activeSelf)
            {
                _suppressOnEnableAutoPlay = true;
                gameObject.SetActive(true);
                _suppressOnEnableAutoPlay = false;
                Canvas.ForceUpdateCanvases();
            }

            ApplyVisualProgress(isOpen ? 1f : 0f);
            ApplyCanvasGroupStateOnComplete(isOpen);

            if (!isOpen && SettingsRef.deactivateOnClose && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 開状態の基準となる現在（または指定）の <c>anchoredPosition</c> と <c>localScale</c> を記録し直す。
        /// </summary>
        public PanelTransition CaptureBaseTransform()
        {
            var rt = TargetRectTransform;
            if (rt != null)
            {
                _baseAnchoredPosition = rt.anchoredPosition;
                _baseScale = rt.localScale;
                _baseStateCaptured = true;
            }
            return this;
        }

        private void CaptureBaseTransformIfNeeded()
        {
            if (_baseStateCaptured)
            {
                return;
            }
            CaptureBaseTransform();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            CaptureBaseTransformIfNeeded();
            _initialized = true;

            bool openInit = SettingsRef.initialState == InitialStateMode.Open;
            _isOpen = openInit;
            _progress = openInit ? 1f : 0f;

            ApplyVisualProgress(_progress);
            ApplyCanvasGroupStateOnComplete(openInit);
        }

        private void StartTransitionInternal(bool open, bool forceFromClosedIfAlreadyOpen)
        {
            ref var s = ref SettingsRef;
            _isOpen = open;

            if (open && forceFromClosedIfAlreadyOpen && Mathf.Approximately(_progress, 1f))
            {
                ApplyVisualProgress(0f);
            }

            float targetProgress = open ? 1f : 0f;
            float distance = Mathf.Abs(targetProgress - _progress);

            ApplyCanvasGroupStateOnStart(open);

            MotionSpec spec = open ? s.openMotion : s.closeMotion;
            float duration = s.scaleDurationOnReverse ? spec.duration * distance : spec.duration;

            if (distance <= 0.0001f || (duration <= 0f && spec.delay <= 0f))
            {
                ApplyVisualProgress(targetProgress);
                OnTransitionComplete();
                return;
            }

            spec.duration = Mathf.Max(0.0001f, duration);

            _onTransitionCompleteDelegate ??= OnTransitionComplete;

            var handle = HisuiMotionExtensions.CreateFloat(_progress, targetProgress, in spec, IgnoreTimeScale)
                .WithOnComplete(_onTransitionCompleteDelegate)
                .Bind(this, s_updateProgressCallback);

            TrackMotion(handle);
        }

        private void OnTransitionComplete()
        {
            if (this == null)
            {
                return;
            }

            ApplyVisualProgress(_isOpen ? 1f : 0f);
            ApplyCanvasGroupStateOnComplete(_isOpen);

            if (!_isOpen && SettingsRef.deactivateOnClose && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void ApplyCanvasGroupStateOnStart(bool opening)
        {
            var cg = TargetCanvasGroup;
            if (cg == null)
            {
                return;
            }

            if (opening)
            {
                cg.blocksRaycasts = true;
                cg.interactable = SettingsRef.interactableTiming == InteractableTiming.OnOpenStart;
            }
            else
            {
                // 閉じる開始時に即座に入力を遮断する
                cg.interactable = false;
            }
        }

        private void ApplyCanvasGroupStateOnComplete(bool opened)
        {
            var cg = TargetCanvasGroup;
            if (cg == null)
            {
                return;
            }

            if (opened)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            }
            else
            {
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        private void ApplyVisualProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);
            ref var s = ref SettingsRef;

            var cg = TargetCanvasGroup;
            if (cg != null)
            {
                cg.alpha = s.useFade
                    ? Mathf.Clamp01(Mathf.LerpUnclamped(s.closedAlpha, s.openAlpha, progress))
                    : s.openAlpha;
            }

            var rt = TargetRectTransform;
            if (rt != null)
            {
                rt.localScale = s.useScale
                    ? Vector3.LerpUnclamped(s.closedScale, _baseScale, progress)
                    : _baseScale;

                if (s.useSlide)
                {
                    Vector2 closedPos = _baseAnchoredPosition + CalculateClosedSlideOffset(rt, in s);
                    rt.anchoredPosition = Vector2.LerpUnclamped(closedPos, _baseAnchoredPosition, progress);
                }
                else
                {
                    rt.anchoredPosition = _baseAnchoredPosition;
                }
            }
        }

        private static Vector2 CalculateClosedSlideOffset(RectTransform rt, in Settings s)
        {
            Vector2 rawOffset = s.slideDirection switch
            {
                SlideDirection.Up => new Vector2(0f, s.slideDistance),
                SlideDirection.Down => new Vector2(0f, -s.slideDistance),
                SlideDirection.Left => new Vector2(-s.slideDistance, 0f),
                SlideDirection.Right => new Vector2(s.slideDistance, 0f),
                SlideDirection.Custom => s.customSlideVector,
                _ => Vector2.zero,
            };

            if (s.slideUnit == SlideUnit.RelativeToPanelSize)
            {
                Rect rect = rt.rect;
                return new Vector2(rawOffset.x * rect.width, rawOffset.y * rect.height);
            }

            return rawOffset;
        }

        #region Fluent Configuration API

        /// <summary>
        /// 起動時の初期状態（Open / Closed）を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetInitialState(InitialStateMode initialState)
        {
            SettingsRef.initialState = initialState;
            return this;
        }

        /// <summary>
        /// 開く演出時の <c>interactable = true</c> 切替タイミングを設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetInteractableTiming(InteractableTiming timing)
        {
            SettingsRef.interactableTiming = timing;
            return this;
        }

        /// <summary>
        /// 閉完了時に <c>GameObject.SetActive(false)</c> にするかどうかを設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetDeactivateOnClose(bool deactivate)
        {
            SettingsRef.deactivateOnClose = deactivate;
            return this;
        }

        /// <summary>
        /// 開閉途中での反転時に残り距離に応じて Duration をスケールするかどうかを設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetScaleDurationOnReverse(bool scaleDuration)
        {
            SettingsRef.scaleDurationOnReverse = scaleDuration;
            return this;
        }

        /// <summary>
        /// 開く演出の <see cref="MotionSpec"/> を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetOpenMotion(in MotionSpec spec)
        {
            SettingsRef.openMotion = spec;
            return this;
        }

        /// <summary>
        /// 開く演出の Duration / Ease / Delay を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetOpenMotion(float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            SettingsRef.openMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>
        /// 閉じる演出の <see cref="MotionSpec"/> を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetCloseMotion(in MotionSpec spec)
        {
            SettingsRef.closeMotion = spec;
            return this;
        }

        /// <summary>
        /// 閉じる演出の Duration / Ease / Delay を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetCloseMotion(float duration, Ease ease = Ease.InCubic, float delay = 0f)
        {
            SettingsRef.closeMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>
        /// フェード演出の有効・無効と開閉時の Alpha 値を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetFade(bool enabled, float closedAlpha = 0f, float openAlpha = 1f)
        {
            ref var s = ref SettingsRef;
            s.useFade = enabled;
            s.closedAlpha = Mathf.Clamp01(closedAlpha);
            s.openAlpha = Mathf.Clamp01(openAlpha);
            return this;
        }

        /// <summary>
        /// スケール演出の有効・無効と閉状態スケールを設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetScale(bool enabled, Vector3 closedScale)
        {
            ref var s = ref SettingsRef;
            s.useScale = enabled;
            s.closedScale = closedScale;
            return this;
        }

        /// <summary>
        /// スケール演出の有効・無効と均一な閉状態スケール値を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetScale(bool enabled, float uniformClosedScale = 0.9f)
        {
            return SetScale(enabled, new Vector3(uniformClosedScale, uniformClosedScale, 1f));
        }

        /// <summary>
        /// 上下左右のスライド演出を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetSlide(
            bool enabled,
            SlideDirection direction = SlideDirection.Down,
            float distance = 0.15f,
            SlideUnit unit = SlideUnit.RelativeToPanelSize)
        {
            ref var s = ref SettingsRef;
            s.useSlide = enabled;
            s.slideDirection = direction;
            s.slideDistance = distance;
            s.slideUnit = unit;
            return this;
        }

        /// <summary>
        /// カスタムベクトルによるスライド演出を設定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetCustomSlide(
            bool enabled,
            Vector2 customOffset,
            SlideUnit unit = SlideUnit.Absolute)
        {
            ref var s = ref SettingsRef;
            s.useSlide = enabled;
            s.slideDirection = SlideDirection.Custom;
            s.customSlideVector = customOffset;
            s.slideUnit = unit;
            return this;
        }

        /// <summary>
        /// 開状態の基準 <c>anchoredPosition</c> を明示的に指定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetBaseAnchoredPosition(Vector2 basePosition)
        {
            CaptureBaseTransformIfNeeded();
            _baseAnchoredPosition = basePosition;
            return this;
        }

        /// <summary>
        /// 開状態の基準 <c>localScale</c> を明示的に指定する（メソッドチェーン対応）。
        /// </summary>
        public PanelTransition SetBaseScale(Vector3 baseScale)
        {
            CaptureBaseTransformIfNeeded();
            _baseScale = baseScale;
            return this;
        }

        #endregion
    }
}
