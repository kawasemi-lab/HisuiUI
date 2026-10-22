using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using Hisui.UI.Palette;
using LitMotion;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hisui.UI.Components
{
    /// <summary>
    /// HPバーなどのように「即時側（前面メインバー）」と「遅延側（背面サブバー）」の2層で値変化を表現するゲージコンポーネント。
    /// <para>
    /// <b>実装方式（uGUI Slider 方式・9-Slice 対応）：</b><br/>
    /// ・<c>Image.Type.Filled</c> を使わず、uGUI 標準の <see cref="Slider"/> と同様に「親 Area オブジェクト → 子の Fill オブジェクト」の構成で、
    ///   子オブジェクトの <see cref="RectTransform"/> の <c>anchorMin</c> / <c>anchorMax</c>（<see cref="Direction.LeftToRight"/> の場合は <c>anchorMax.x</c>）を 0〜1 の値と同期させて伸縮する。これにより 9-Slice（<see cref="Image.Type.Sliced"/>）のスプライトを崩さず使用できる。<br/>
    /// ・常に <c>FrontFillAmount &lt;= BackFillAmount</c> の不変条件を保つことで、背面バーのはみ出し領域（<c>Back - Front</c>）が差分ゲージとして機能する。<br/>
    /// ・<b>減少（ダメージ）時：</b>背面バーをダメージ色にし、前面バーが先行して減少、背面バーが <c>damageHoldDuration</c> 秒待機した後に追従して減少する。<br/>
    /// ・<b>増加（回復）時：</b>背面バーを回復色にし、背面バーが先行して増加、前面バーが <c>healHoldDuration</c> 秒待機した後に追従して増加する。<br/>
    /// ・インスペクタ上の <c>Value</c> スライダーを動かした際も、エディタ上（Edit モード / Play モード両方）で実際の遅延アニメーションが再生される。
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Hisui/UI/Delay Gauge")]
    [DisallowMultipleComponent]
    public sealed class DelayGauge : HisuiMotionBehaviour<DelayGauge, DelayGauge.Settings>
    {
        /// <summary>
        /// ゲージの伸縮方向（uGUI の <see cref="Slider.Direction"/> と同様の4方向）。
        /// </summary>
        public enum Direction
        {
            /// <summary>左から右へ増加（<c>anchorMax.x</c> を 0〜1 で制御）。</summary>
            LeftToRight = 0,

            /// <summary>右から左へ増加（<c>anchorMin.x</c> を 1〜0 で制御）。</summary>
            RightToLeft = 1,

            /// <summary>下から上へ増加（<c>anchorMax.y</c> を 0〜1 で制御）。</summary>
            BottomToTop = 2,

            /// <summary>上から下へ増加（<c>anchorMin.y</c> を 1〜0 で制御）。</summary>
            TopToBottom = 3,
        }

        /// <summary>
        /// <see cref="DelayGauge"/> の全設定を保持するシリアライズ可能構造体。
        /// </summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>現在の正規化ゲージ値（0〜1）。インスペクタ上で動かすと遅延アニメーションが再生される。</summary>
            [Header("Value & Direction")]
            [Tooltip("現在の正規化ゲージ値（0〜1）。インスペクタ上で動かすと遅延アニメーションが再生されます")]
            [Range(0f, 1f)]
            [FormerlySerializedAs("initialValue")]
            public float value;

            /// <summary>ゲージの伸縮方向。</summary>
            [Tooltip("ゲージの伸縮方向（LeftToRight の場合は子の AnchorMax.x を制御）")]
            public Direction direction;

            /// <summary>ダメージ（減少）時の前面バー（即時側）のモーション設定。duration = 0 で即時反映。</summary>
            [Header("Damage (Decrease) Motion")]
            [Tooltip("減少時の前面（即時側）モーション設定。Duration = 0 で即時減少")]
            public MotionSpec damageFrontMotion;

            /// <summary>ダメージ（減少）時に背面バー（遅延側）が動き出すまでの待機時間（秒）。</summary>
            [Tooltip("減少時に遅延側が減少を開始するまでのホールド時間（秒）")]
            [Min(0f)]
            public float damageHoldDuration;

            /// <summary>ダメージ（減少）時の背面バー（遅延側）の追従モーション設定。</summary>
            [Tooltip("減少時の背面（遅延側）追従モーション設定")]
            public MotionSpec damageBackMotion;

            /// <summary>回復（増加）時の背面バー（先行側）のモーション設定。duration = 0 で即時反映。</summary>
            [Header("Heal (Increase) Motion")]
            [Tooltip("回復時の背面（先行側）モーション設定。Duration = 0 で即時増加")]
            public MotionSpec healLeadMotion;

            /// <summary>回復（増加）時に前面バー（追従側）が動き出すまでの待機時間（秒）。</summary>
            [Tooltip("回復時にメイン側が増加を開始するまでのホールド時間（秒）")]
            [Min(0f)]
            public float healHoldDuration;

            /// <summary>回復（増加）時の前面バー（追従側）のモーション設定。</summary>
            [Tooltip("回復時の前面（追従側）モーション設定")]
            public MotionSpec healFollowMotion;

            /// <summary>メインバー（前面）の色設定（Direct / uPalette）。</summary>
            [Header("Colors (Direct or uPalette)")]
            [Tooltip("メインバー（前面）の色")]
            public HisuiColor mainColor;

            /// <summary>ダメージ時の遅延バー（背面）の色設定（Direct / uPalette）。</summary>
            [Tooltip("ダメージ時の遅延バー（背面）の色")]
            public HisuiColor damageDelayColor;

            /// <summary>回復時の先行バー（背面）の色設定（Direct / uPalette）。</summary>
            [Tooltip("回復時の先行バー（背面）の色")]
            public HisuiColor healDelayColor;

            /// <summary>uPalette のテーマ切替時の色クロスフェード時間（秒）。0 以下で即時切替。</summary>
            [Tooltip("uPalette テーマ切替時のクロスフェード時間（秒）")]
            [Min(0f)]
            public float themeCrossfadeDuration;

            /// <summary>
            /// 標準的な HP ゲージ向けのデフォルト設定を返す。
            /// </summary>
            public static Settings Default => new Settings
            {
                value = 1f,
                direction = Direction.LeftToRight,
                damageFrontMotion = new MotionSpec(0.08f, Ease.OutQuad, 0f),
                damageHoldDuration = 0.35f,
                damageBackMotion = new MotionSpec(0.45f, Ease.OutCubic, 0f),
                healLeadMotion = new MotionSpec(0.12f, Ease.OutQuad, 0f),
                healHoldDuration = 0.25f,
                healFollowMotion = new MotionSpec(0.40f, Ease.OutCubic, 0f),
                mainColor = HisuiColor.FromColor(new Color(0.24f, 0.85f, 0.42f, 1f)),
                damageDelayColor = HisuiColor.FromColor(new Color(0.92f, 0.26f, 0.26f, 1f)),
                healDelayColor = HisuiColor.FromColor(new Color(0.55f, 0.96f, 0.68f, 1f)),
                themeCrossfadeDuration = 0.15f,
            };
        }

        private enum SubBarColorState : byte
        {
            None = 0,
            Damage = 1,
            Heal = 2,
        }

        [Header("Target Fill Objects (9-Slice / Anchor Driven)")]
        [Tooltip("前面に表示されるメインゲージの Fill Image（親の Fill Area の子として Anchor が制御されます）")]
        [SerializeField]
        private Image _frontImage;

        [Tooltip("背面に表示される遅延・先行ゲージの Fill Image（親の Delay Area の子として Anchor が制御されます）")]
        [SerializeField]
        private Image _backImage;

        private readonly HisuiColorBinder _mainColorBinder = new HisuiColorBinder();
        private readonly HisuiColorBinder _subColorBinder = new HisuiColorBinder();

        private DrivenRectTransformTracker _tracker;
        private float _targetValue = 1f;
        private float _frontAmount = 1f;
        private float _backAmount = 1f;
        private bool _initialized;
        private bool _pendingValidate;
        private SubBarColorState _subBarColorState = SubBarColorState.None;

        private static readonly Action<float, DelayGauge> s_updateFrontCallback = static (v, self) =>
        {
            self.SetFrontFillInternal(v);
        };

        private static readonly Action<float, DelayGauge> s_updateBackCallback = static (v, self) =>
        {
            self.SetBackFillInternal(v);
        };

        /// <summary>
        /// 目標値（<see cref="Value"/> / <see cref="CurrentValue"/>）が変更されたときに発火するイベント。引数は新しい正規化値（0〜1）。
        /// </summary>
        public event Action<float> OnValueChanged;

        /// <summary>
        /// 現在の正規化ゲージ値（0〜1）を取得・設定する。設定時は遅延アニメーションを伴って更新される。
        /// </summary>
        public float Value
        {
            get => _targetValue;
            set => SetValue(value, immediate: false);
        }

        /// <summary>
        /// 現在の論理目標値（0〜1）。<see cref="Value"/> と同義。
        /// </summary>
        public float CurrentValue
        {
            get => _targetValue;
            set => SetValue(value, immediate: false);
        }

        /// <summary>
        /// 前面（メイン）バーの現在の実表示量（0〜1）。
        /// </summary>
        public float FrontFillAmount => _frontAmount;

        /// <summary>
        /// 背面（遅延/先行）バーの現在の実表示量（0〜1）。
        /// </summary>
        public float BackFillAmount => _backAmount;

        /// <summary>
        /// 前面（メイン）の <see cref="Image"/> を取得・設定する。
        /// </summary>
        public Image FrontImage
        {
            get => _frontImage;
            set
            {
                _frontImage = value;
                BindColors();
                ApplyFillAmountsToRects();
            }
        }

        /// <summary>
        /// 背面（遅延/先行）の <see cref="Image"/> を取得・設定する。
        /// </summary>
        public Image BackImage
        {
            get => _backImage;
            set
            {
                _backImage = value;
                BindColors();
                ApplyFillAmountsToRects();
            }
        }

        /// <summary>
        /// 前面（メイン）バーの <see cref="RectTransform"/> を返す。
        /// </summary>
        public RectTransform FrontRect => _frontImage != null ? _frontImage.rectTransform : null;

        /// <summary>
        /// 背面（遅延/先行）バーの <see cref="RectTransform"/> を返す。
        /// </summary>
        public RectTransform BackRect => _backImage != null ? _backImage.rectTransform : null;

        private void Reset()
        {
            SettingsRef = Settings.Default;
            _initialized = false;
            EnsureInitialized();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            EnsureInitialized();
            BindColors();
            ApplyFillAmountsToRects();
            base.OnEnable();
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            _pendingValidate = false;
            _tracker.Clear();
            base.OnDisable();
            _mainColorBinder.Unbind();
            _subColorBinder.Unbind();
            _subBarColorState = SubBarColorState.None;
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            _tracker.Clear();
            base.OnDestroy();
            _mainColorBinder.Dispose();
            _subColorBinder.Dispose();
        }

        private void Update()
        {
            if (_pendingValidate)
            {
                _pendingValidate = false;
                ProcessInspectorChanges();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || !_initialized)
            {
                return;
            }
            ApplyFillAmountsToRects();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ref var s = ref SettingsRef;
            s.value = Mathf.Clamp01(s.value);
            s.damageHoldDuration = Mathf.Max(0f, s.damageHoldDuration);
            s.healHoldDuration = Mathf.Max(0f, s.healHoldDuration);
            s.themeCrossfadeDuration = Mathf.Max(0f, s.themeCrossfadeDuration);

            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
            {
                return;
            }

            if (!isActiveAndEnabled || !_initialized)
            {
                _targetValue = s.value;
                _frontAmount = s.value;
                _backAmount = s.value;
                return;
            }

            // OnValidate 内で直接 RectTransform.anchorMin / anchorMax を書き換えると
            // Unity の SendMessage 警告が出るため、直後の Update で処理する
            _pendingValidate = true;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif

        private void ProcessInspectorChanges()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            BindColors();

            float inspectorValue = Mathf.Clamp01(SettingsRef.value);
            if (!Mathf.Approximately(inspectorValue, _targetValue))
            {
                SetValue(inspectorValue, immediate: false);
            }
            else
            {
                ApplyFillAmountsToRects();
            }
        }

        /// <inheritdoc />
        protected override void OnSettingsChanged()
        {
            ref var s = ref SettingsRef;
            s.value = Mathf.Clamp01(s.value);

            if (isActiveAndEnabled)
            {
                _subBarColorState = SubBarColorState.None;
                BindColors();

                if (!Mathf.Approximately(s.value, _targetValue))
                {
                    SetValue(s.value, immediate: false);
                }
                else
                {
                    ApplyFillAmountsToRects();
                }
            }
        }

        /// <summary>
        /// 基底クラスの <see cref="HisuiMotionBehaviour.Play"/> から呼ばれた際は、現在の <see cref="Value"/> へ向けて現在の表示位置からアニメーションを再生する。
        /// </summary>
        protected override void OnPlayCore()
        {
            EnsureInitialized();
            bool isDecrease = _targetValue <= _frontAmount;
            AnimateToValueInternal(_targetValue, isDecrease);
        }

        /// <summary>
        /// ゲージの目標値（0〜1）を更新する。
        /// </summary>
        /// <param name="value">新しい正規化値（0〜1 にクランプされる）。</param>
        /// <param name="immediate">true の場合、アニメーションを行わず即座に両バーを目標値へ揃える。</param>
        public void SetValue(float value, bool immediate = false)
        {
            EnsureInitialized();

            float clamped = Mathf.Clamp01(value);
            float previousTarget = _targetValue;
            bool valueChanged = !Mathf.Approximately(previousTarget, clamped);

            _targetValue = clamped;
            SettingsRef.value = clamped;

            if (immediate || !isActiveAndEnabled)
            {
                CancelTrackedMotionsAndIncrementVersion();
                SetFrontFillInternal(clamped);
                SetBackFillInternal(clamped);
            }
            else
            {
                // 前回の目標値との比較（同値の場合は現在の前面バーとの比較）で減少演出か回復演出かを判定する
                bool isDecrease = valueChanged ? (clamped < previousTarget) : (clamped <= _frontAmount);
                CancelTrackedMotionsAndIncrementVersion();
                AnimateToValueInternal(clamped, isDecrease);
            }

            if (valueChanged)
            {
                OnValueChanged?.Invoke(clamped);
            }
        }

        /// <summary>
        /// アニメーションを行わず、即座にゲージ値を指定値（0〜1）へ設定する。
        /// </summary>
        /// <param name="value">新しい正規化値（0〜1 にクランプされる）。</param>
        public void SetValueImmediate(float value)
        {
            SetValue(value, immediate: true);
        }

        /// <summary>
        /// ゲージの目標値（0〜1）を更新し、即時側・遅延側の両モーションが完了するまで待機する。
        /// <para>
        /// 途中で再度 <see cref="SetValue"/> / <see cref="SetValueAsync"/> が呼ばれて上書きされた場合や、
        /// <see cref="HisuiMotionBehaviour.Stop"/> / <c>OnDisable</c> / <c>OnDestroy</c> / <paramref name="ct"/> で中断された場合も、
        /// 例外をスローせず静かに完了する。
        /// </para>
        /// </summary>
        /// <param name="value">新しい正規化値（0〜1 にクランプされる）。</param>
        /// <param name="ct">キャンセルトークン。</param>
        public UniTask SetValueAsync(float value, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return UniTask.CompletedTask;
            }

            SetValue(value, immediate: false);
            return WaitTrackedMotionsAsync(PlayVersion, ct);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            float init = Mathf.Clamp01(SettingsRef.value);
            SettingsRef.value = init;
            _targetValue = init;
            _frontAmount = init;
            _backAmount = init;
            ApplyFillAmountsToRects();
        }

        private void BindColors()
        {
            ref var s = ref SettingsRef;
            _mainColorBinder.Bind(_frontImage, in s.mainColor, s.themeCrossfadeDuration, IgnoreTimeScale);

            var desiredSubState = _subBarColorState == SubBarColorState.None
                ? SubBarColorState.Damage
                : _subBarColorState;
            ApplySubBarColorState(desiredSubState, forceRebind: true);
        }

        private void ApplySubBarColorState(SubBarColorState state, bool forceRebind = false)
        {
            if (!forceRebind && _subBarColorState == state)
            {
                return;
            }

            _subBarColorState = state;
            ref var s = ref SettingsRef;
            ref var colorToBind = ref (state == SubBarColorState.Heal ? ref s.healDelayColor : ref s.damageDelayColor);
            // 色モードの切替（ダメージ⇔回復）自体は即座に反映し、以降のテーマ切替時はクロスフェードさせる
            _subColorBinder.Bind(_backImage, in colorToBind, s.themeCrossfadeDuration, IgnoreTimeScale);
            RequestEditorRepaintIfNeeded();
        }

        private void AnimateToValueInternal(float target, bool isDecrease)
        {
            ref var s = ref SettingsRef;
            bool ignoreTs = IgnoreTimeScale;

            if (isDecrease)
            {
                // --- ダメージ（減少）演出 ---
                // 回復途中からダメージへ転じた場合は、未到達の回復先行バーを現在の前面バー位置に揃えてからダメージ遅延を開始する
                if (_subBarColorState == SubBarColorState.Heal || _backAmount < _frontAmount)
                {
                    SetBackFillInternal(_frontAmount);
                }

                ApplySubBarColorState(SubBarColorState.Damage);

                // 1. 前面（即時側）を target へ減少
                ScheduleBarMotion(
                    from: _frontAmount,
                    to: target,
                    spec: in s.damageFrontMotion,
                    extraHold: 0f,
                    ignoreTimeScale: ignoreTs,
                    isFront: true);

                // 2. 背面（遅延側）を damageHoldDuration 待機後に target へ減少
                ScheduleBarMotion(
                    from: _backAmount,
                    to: target,
                    spec: in s.damageBackMotion,
                    extraHold: s.damageHoldDuration,
                    ignoreTimeScale: ignoreTs,
                    isFront: false);
            }
            else
            {
                // --- 回復（増加）演出 ---
                // ダメージ残存中から回復へ転じた場合、または背面が前面より低い場合は、背面バーを現在の前面バー位置に揃えてから先行増加させる
                if (_subBarColorState != SubBarColorState.Heal || _backAmount < _frontAmount)
                {
                    SetBackFillInternal(_frontAmount);
                }

                ApplySubBarColorState(SubBarColorState.Heal);

                // 1. 背面（先行側）を target へ先行増加
                ScheduleBarMotion(
                    from: _backAmount,
                    to: target,
                    spec: in s.healLeadMotion,
                    extraHold: 0f,
                    ignoreTimeScale: ignoreTs,
                    isFront: false);

                // 2. 前面（追従側）を healHoldDuration 待機後に target へ追従増加
                ScheduleBarMotion(
                    from: _frontAmount,
                    to: target,
                    spec: in s.healFollowMotion,
                    extraHold: s.healHoldDuration,
                    ignoreTimeScale: ignoreTs,
                    isFront: true);
            }
        }

        private void ScheduleBarMotion(
            float from,
            float to,
            in MotionSpec spec,
            float extraHold,
            bool ignoreTimeScale,
            bool isFront)
        {
            float totalDelay = Mathf.Max(0f, spec.delay) + Mathf.Max(0f, extraHold);

            if (Mathf.Approximately(from, to) || (spec.duration <= 0f && totalDelay <= 0f))
            {
                if (isFront)
                {
                    SetFrontFillInternal(to);
                }
                else
                {
                    SetBackFillInternal(to);
                }
                return;
            }

            var effectiveSpec = spec;
            effectiveSpec.delay = totalDelay;
            effectiveSpec.duration = Mathf.Max(0.0001f, spec.duration);

            var handle = HisuiMotionExtensions.CreateFloat(from, to, in effectiveSpec, ignoreTimeScale)
                .WithImmediateBind(false)
                .Bind(this, isFront ? s_updateFrontCallback : s_updateBackCallback);

            TrackMotion(handle);
        }

        private void SetFrontFillInternal(float value)
        {
            _frontAmount = Mathf.Clamp01(value);
            Direction dir = SettingsRef.direction;

            // 遅延側が即時側より小さくならない不変条件（Front <= Back）を維持
            if (_backAmount < _frontAmount)
            {
                _backAmount = _frontAmount;
                if (_backImage != null)
                {
                    ApplyAnchorFill(_backImage.rectTransform, _backAmount, dir);
                }
            }

            if (_frontImage != null)
            {
                ApplyAnchorFill(_frontImage.rectTransform, _frontAmount, dir);
            }

            RequestEditorRepaintIfNeeded();
        }

        private void SetBackFillInternal(float value)
        {
            // 背面バーは前面バーを下回らないようクランプ
            _backAmount = Mathf.Clamp(value, _frontAmount, 1f);
            if (_backImage != null)
            {
                ApplyAnchorFill(_backImage.rectTransform, _backAmount, SettingsRef.direction);
            }

            RequestEditorRepaintIfNeeded();
        }

        private void ApplyFillAmountsToRects()
        {
            _tracker.Clear();
            Direction dir = SettingsRef.direction;

            if (_frontImage != null)
            {
                RectTransform frontRect = _frontImage.rectTransform;
                _tracker.Add(this, frontRect, DrivenTransformProperties.Anchors);
                ApplyAnchorFill(frontRect, _frontAmount, dir);
            }

            if (_backImage != null)
            {
                RectTransform backRect = _backImage.rectTransform;
                _tracker.Add(this, backRect, DrivenTransformProperties.Anchors);
                ApplyAnchorFill(backRect, _backAmount, dir);
            }

            RequestEditorRepaintIfNeeded();
        }

        private static void ApplyAnchorFill(RectTransform rect, float normalizedAmount, Direction dir)
        {
            if (rect == null)
            {
                return;
            }

            float clamped = Mathf.Clamp01(normalizedAmount);
            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.one;

            switch (dir)
            {
                case Direction.LeftToRight:
                    anchorMax.x = clamped;
                    break;
                case Direction.RightToLeft:
                    anchorMin.x = 1f - clamped;
                    break;
                case Direction.BottomToTop:
                    anchorMax.y = clamped;
                    break;
                case Direction.TopToBottom:
                    anchorMin.y = 1f - clamped;
                    break;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
        }

        private static void RequestEditorRepaintIfNeeded()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        #region Fluent Configuration API

        /// <summary>
        /// 前面（メイン）と背面（遅延/先行）の Fill <see cref="Image"/> を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetImages(Image frontImage, Image backImage)
        {
            _frontImage = frontImage;
            _backImage = backImage;
            if (isActiveAndEnabled)
            {
                BindColors();
            }
            ApplyFillAmountsToRects();
            return this;
        }

        /// <summary>
        /// ゲージの伸縮方向を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDirection(Direction direction)
        {
            SettingsRef.direction = direction;
            ApplyFillAmountsToRects();
            return this;
        }

        /// <summary>
        /// 初期値（または即時値 0〜1）を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetInitialValue(float initialValue)
        {
            SetValue(initialValue, immediate: true);
            return this;
        }

        /// <summary>
        /// ダメージ時の前面（即時側）モーション設定を変更する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDamageFrontMotion(in MotionSpec spec)
        {
            SettingsRef.damageFrontMotion = spec;
            return this;
        }

        /// <summary>
        /// ダメージ時の背面（遅延側）ホールド時間（秒）を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDamageHoldDuration(float holdSeconds)
        {
            SettingsRef.damageHoldDuration = Mathf.Max(0f, holdSeconds);
            return this;
        }

        /// <summary>
        /// ダメージ時の背面（遅延側）追従モーション設定を変更する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDamageBackMotion(in MotionSpec spec)
        {
            SettingsRef.damageBackMotion = spec;
            return this;
        }

        /// <summary>
        /// ダメージ時の遅延バーの再生時間とイージングを簡易設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDamageDelay(float holdSeconds, float duration, Ease ease = Ease.OutCubic)
        {
            SettingsRef.damageHoldDuration = Mathf.Max(0f, holdSeconds);
            SettingsRef.damageBackMotion = SettingsRef.damageBackMotion.WithDuration(duration).WithEase(ease);
            return this;
        }

        /// <summary>
        /// 回復時の背面（先行側）モーション設定を変更する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetHealLeadMotion(in MotionSpec spec)
        {
            SettingsRef.healLeadMotion = spec;
            return this;
        }

        /// <summary>
        /// 回復時の前面（追従側）ホールド時間（秒）を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetHealHoldDuration(float holdSeconds)
        {
            SettingsRef.healHoldDuration = Mathf.Max(0f, holdSeconds);
            return this;
        }

        /// <summary>
        /// 回復時の前面（追従側）モーション設定を変更する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetHealFollowMotion(in MotionSpec spec)
        {
            SettingsRef.healFollowMotion = spec;
            return this;
        }

        /// <summary>
        /// 回復時の追従バーのホールド時間・再生時間・イージングを簡易設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetHealDelay(float holdSeconds, float duration, Ease ease = Ease.OutCubic)
        {
            SettingsRef.healHoldDuration = Mathf.Max(0f, holdSeconds);
            SettingsRef.healFollowMotion = SettingsRef.healFollowMotion.WithDuration(duration).WithEase(ease);
            return this;
        }

        /// <summary>
        /// メインバー（前面）の色を設定する（Direct / uPalette 両対応、メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetMainColor(in HisuiColor color)
        {
            SettingsRef.mainColor = color;
            OnSettingsChanged();
            return this;
        }

        /// <summary>
        /// ダメージ時の遅延バー（背面）の色を設定する（Direct / uPalette 両対応、メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetDamageDelayColor(in HisuiColor color)
        {
            SettingsRef.damageDelayColor = color;
            OnSettingsChanged();
            return this;
        }

        /// <summary>
        /// 回復時の先行バー（背面）の色を設定する（Direct / uPalette 両対応、メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetHealDelayColor(in HisuiColor color)
        {
            SettingsRef.healDelayColor = color;
            OnSettingsChanged();
            return this;
        }

        /// <summary>
        /// uPalette テーマ切替時のクロスフェード秒数を設定する（メソッドチェーン対応）。
        /// </summary>
        public DelayGauge SetThemeCrossfadeDuration(float duration)
        {
            SettingsRef.themeCrossfadeDuration = Mathf.Max(0f, duration);
            OnSettingsChanged();
            return this;
        }

        #endregion
    }
}
