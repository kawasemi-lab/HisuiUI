using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hisui.UI.Components
{
    /// <summary>
    /// ボタンの状態遷移（ホバー・押下・クリック・無効・選択）に応じた<b>スケールと位置オフセットの演出</b>を付けるコンポーネント。
    /// <para>
    /// <b>役割分担：</b><br/>
    /// ・クリック判定・<c>onClick</c> の発火・色／スプライトの Transition は標準の <see cref="Button"/>（<see cref="Selectable"/>）が担当する。<br/>
    /// ・本コンポーネントは追加の動き（スケール・位置オフセット・クリック時のパンチ）のみを担当し、<see cref="Graphic"/> の色・スプライトには一切触れない。<br/>
    /// ・入力を消費せず、<see cref="Button.onClick"/> の発火も妨げない。uPalette にも依存しない。
    /// </para>
    /// <para>
    /// <b>値の意味：</b>状態ごとのスケールは Normal（Awake 時の localScale）に対する<b>倍率</b>、位置オフセットは Normal（Awake 時の anchoredPosition）からの<b>差分</b>。
    /// </para>
    /// <para>
    /// <b>基底の Play / Stop の意味づけ：</b><br/>
    /// ・<see cref="HisuiMotionBehaviour.Play"/> ＝ クリック演出（Punch）の再生（<see cref="PlayClick"/> と同義）。<br/>
    /// ・<see cref="Stop"/> ＝ 進行中のモーションをすべて止めて Normal の値へ即座に戻す（<see cref="ResetImmediate"/> と同義）。<br/>
    /// ・自動再生トリガーは使用しない（None 固定。<c>OnEnable</c> でも自動再生しない）。
    /// </para>
    /// <para>
    /// <b>注意：</b>Button の Transition が Animation の場合は Animator とスケール・位置が競合する。
    /// 他の ButtonMotion / PanelTransition と同じ RectTransform を操作する場合も競合する。
    /// 親が LayoutGroup の場合は anchoredPosition が上書きされるため、見た目用の子オブジェクトを Target にすること。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Button Motion")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ButtonMotion : HisuiMotionBehaviour<ButtonMotion, ButtonMotion.Settings>,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        /// <summary>Hover / Pressed / Disabled / Selected 1 状態分の設定。</summary>
        [Serializable]
        public struct StateSettings
        {
            /// <summary>false の場合、この状態に入っても Normal と同じ値を目標とする（動かない）。</summary>
            [Tooltip("無効にすると、この状態に入っても Normal と同じ値を目標とする（動かない）")]
            public bool enabled;

            /// <summary>Normal に対するスケール倍率。</summary>
            [Tooltip("Normal に対するスケール倍率")]
            public Vector3 scale;

            /// <summary>Normal からの anchoredPosition 差分。</summary>
            [Tooltip("Normal からの anchoredPosition の差分（押下時に少し沈むなど）")]
            public Vector2 offset;

            /// <summary>この状態へ遷移するときの duration / ease。</summary>
            [Tooltip("この状態へ遷移するときの再生時間・イージング")]
            public MotionSpec motion;

            /// <summary>指定値で生成する。</summary>
            public StateSettings(bool enabled, Vector3 scale, Vector2 offset, MotionSpec motion)
            {
                this.enabled = enabled;
                this.scale = scale;
                this.offset = offset;
                this.motion = motion;
            }
        }

        /// <summary>クリック演出（Punch）の設定。</summary>
        [Serializable]
        public struct PunchSettings
        {
            /// <summary>false の場合、クリックしてもパンチを再生しない。</summary>
            [Tooltip("無効にするとクリック時のパンチを再生しない")]
            public bool enabled;

            /// <summary>スケールの膨らみ量（0.1 で最大 +10%）。</summary>
            [Tooltip("スケールの膨らみ量（0.1 で最大 +10%）")]
            public float strength;

            /// <summary>再生時間と、時間軸のイージング。</summary>
            [Tooltip("パンチの再生時間・イージング")]
            public MotionSpec motion;

            /// <summary>振動回数（1 で膨らんで戻る。2 以上で行き来を繰り返しながら減衰する）。</summary>
            [Tooltip("振動回数（1 で膨らんで戻る。2 以上で振動しながら減衰）")]
            [Min(0.5f)]
            public float vibrato;
        }

        /// <summary><see cref="ButtonMotion"/> の全設定。</summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>Normal へ戻るときの duration / ease（Normal の値自体は Awake 時に自動取得）。</summary>
            [Tooltip("Normal へ戻るときの再生時間・イージング")]
            public MotionSpec normalMotion;

            /// <summary>ホバー時の設定。</summary>
            [Header("States")]
            public StateSettings hover;

            /// <summary>押下時の設定。</summary>
            public StateSettings pressed;

            /// <summary>無効時の設定。</summary>
            public StateSettings disabled;

            /// <summary>選択（フォーカス）時の設定。</summary>
            public StateSettings selected;

            /// <summary>クリック演出の設定。</summary>
            [Header("Click Punch")]
            public PunchSettings click;

            /// <summary>標準的なデフォルト設定。</summary>
            public static Settings Default => new Settings
            {
                normalMotion = new MotionSpec(0.15f, Ease.OutCubic, 0f),
                hover = new StateSettings(true, new Vector3(1.05f, 1.05f, 1f), Vector2.zero, new MotionSpec(0.15f, Ease.OutCubic, 0f)),
                pressed = new StateSettings(true, new Vector3(0.95f, 0.95f, 1f), new Vector2(0f, -4f), new MotionSpec(0.08f, Ease.OutCubic, 0f)),
                disabled = new StateSettings(false, Vector3.one, Vector2.zero, new MotionSpec(0.15f, Ease.OutCubic, 0f)),
                selected = new StateSettings(false, new Vector3(1.03f, 1.03f, 1f), Vector2.zero, new MotionSpec(0.15f, Ease.OutCubic, 0f)),
                click = new PunchSettings
                {
                    enabled = true,
                    strength = 0.12f,
                    motion = new MotionSpec(0.25f, Ease.Linear, 0f),
                    vibrato = 1f,
                },
            };
        }

        [Header("Targets")]
        [Tooltip("状態の取得元。空の場合は同じ GameObject の Selectable（通常は Button）")]
        [SerializeField]
        private Selectable _selectable;

        [Tooltip("動かす RectTransform。空の場合は自分自身。当たり判定を拡縮させたくない場合は見た目用の子を指定する")]
        [SerializeField]
        private RectTransform _target;

        private static readonly Action<float, ButtonMotion> s_stateTick = static (t, self) => self.OnStateTick(t);
        private static readonly Action<float, ButtonMotion> s_punchTick = static (t, self) => self.OnPunchTick(t);

        private Button _button;
        private UnityAction _onClickAction;
        private bool _initialized;
        private bool _subscribed;

        // Normal の基準値
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePosition;

        // 現在値（スケールは Normal に対する倍率、位置は Normal からの差分）
        private Vector3 _curScale = Vector3.one;
        private Vector2 _curOffset;
        private float _punch;

        private Vector3 _fromScale;
        private Vector2 _fromOffset;
        private Vector3 _toScale = Vector3.one;
        private Vector2 _toOffset;

        private float _punchStrength;
        private float _punchVibrato;
        private int _punchVersion;

        private MotionHandle _stateHandle;
        private MotionHandle _punchHandle;

        private ButtonMotionState _currentState;
        private bool _hover;
        private bool _pointerDown;
        private bool _selectedFlag;
        private bool _lastInteractable = true;

        /// <summary>現在の状態。</summary>
        public ButtonMotionState CurrentState => _currentState;

        /// <summary>状態の取得元 Selectable（見つからない場合は null）。</summary>
        public Selectable Selectable => _selectable;

        /// <summary>動かす対象の RectTransform。</summary>
        public RectTransform Target => _target != null ? _target : (RectTransform)transform;

        /// <inheritdoc />
        public override bool IsPlaying => _stateHandle.IsActive() || _punchHandle.IsActive();

        private void Reset()
        {
            SettingsRef = Settings.Default;
            _selectable = GetComponent<Selectable>();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <inheritdoc />
        /// <remarks>自動再生トリガーは使わないため、基底の OnEnable は呼ばない。</remarks>
        protected override void OnEnable()
        {
            EnsureInitialized();
            Subscribe();

            _hover = false;
            _pointerDown = false;
            _selectedFlag = _selectable != null
                && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == _selectable.gameObject;
            _lastInteractable = IsInteractableNow();

            GoTo(Evaluate(), true);
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            ResetImmediate();
            Unsubscribe();
            base.OnDisable();
        }

        private void LateUpdate()
        {
            // Button.interactable の直接変更など、通知の来ない変更を bool の比較だけで拾う。
            if (_selectable != null && _selectable.IsInteractable() != _lastInteractable)
            {
                Refresh();
            }
        }

        // Unity メッセージ：親の CanvasGroup 変更（interactable / blocksRaycasts 等）。
        private void OnCanvasGroupChanged()
        {
            Refresh();
        }

        #region Input

        /// <inheritdoc />
        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            Refresh();
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            Refresh();
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _hover = true;
            _pointerDown = true;
            Refresh();
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pointerDown = false;

            // タッチ（pointerId >= 0）は指を離した後に Hover が残らないよう、要素上でも Normal へ戻す。
            if (eventData.pointerId >= 0)
            {
                _hover = false;
            }

            Refresh();
        }

        /// <inheritdoc />
        public void OnSelect(BaseEventData eventData)
        {
            _selectedFlag = true;
            Refresh();
        }

        /// <inheritdoc />
        public void OnDeselect(BaseEventData eventData)
        {
            _selectedFlag = false;
            Refresh();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 現在の入力状態・<c>IsInteractable()</c> から状態を再評価し、変化していれば遷移する。
        /// 通知の来ない変更をコードから明示的に反映したいときに呼ぶ。
        /// </summary>
        public void Refresh()
        {
            if (!_initialized || !isActiveAndEnabled)
            {
                return;
            }

            _lastInteractable = IsInteractableNow();
            ButtonMotionState next = Evaluate();
            if (next != _currentState)
            {
                GoTo(next, false);
            }
        }

        /// <summary>
        /// 状態を強制的に指定して遷移する。次に入力や <see cref="Refresh"/> による再評価が走ると、その結果で上書きされる。
        /// </summary>
        /// <param name="state">遷移先の状態。</param>
        /// <param name="immediate">true の場合、演出なしで即座に反映する。</param>
        public void SetState(ButtonMotionState state, bool immediate = false)
        {
            EnsureInitialized();
            GoTo(state, immediate);
        }

        /// <summary>クリック演出（Punch）を再生する。<see cref="HisuiMotionBehaviour.Play"/> と同義。</summary>
        public void PlayClick()
        {
            Play();
        }

        /// <summary>
        /// クリック演出を再生し、完了まで待機する。中断・上書き・無効化・破棄・<paramref name="ct"/> のキャンセル時も例外を投げず静かに戻る。
        /// </summary>
        public async UniTask PlayClickAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || this == null)
            {
                return;
            }

            PlayClick();
            int version = _punchVersion;
            if (!_punchHandle.IsActive())
            {
                return;
            }

            var destroyToken = this.GetCancellationTokenOnDestroy();
            while (this != null && _punchVersion == version && _punchHandle.IsActive())
            {
                if (ct.IsCancellationRequested)
                {
                    CancelPunch(version);
                    return;
                }

                bool canceled = await UniTask.NextFrame(PlayerLoopTiming.Update, destroyToken).SuppressCancellationThrow();
                if (canceled)
                {
                    return;
                }
            }
        }

        /// <summary><see cref="PlayClickAsync"/> と同義。</summary>
        public override UniTask PlayAsync(CancellationToken ct = default)
        {
            return PlayClickAsync(ct);
        }

        /// <summary>
        /// 進行中のすべてのモーションを止め、Normal の値へ即座に戻す。<see cref="Stop"/> と同義。
        /// 入力フラグ（Hover など）は保持するため、次の <see cref="Refresh"/> で再評価される。
        /// </summary>
        public void ResetImmediate()
        {
            _stateHandle.TryCancelAndClear();
            _punchHandle.TryCancelAndClear();
            _punchVersion++;

            _punch = 0f;
            _curScale = Vector3.one;
            _curOffset = Vector2.zero;
            _toScale = Vector3.one;
            _toOffset = Vector2.zero;
            _currentState = ButtonMotionState.Normal;
            Apply();
        }

        /// <summary>進行中のモーションを止めて Normal の値へ即座に戻す。</summary>
        public override void Stop()
        {
            ResetImmediate();
            base.Stop();
        }

        /// <summary>Normal の基準値（スケール・anchoredPosition）を、対象の現在値から取得し直す。</summary>
        public ButtonMotion CaptureNormal()
        {
            var rt = Target;
            _baseScale = rt.localScale;
            _basePosition = rt.anchoredPosition;
            return this;
        }

        /// <summary>Normal の基準値をコードから上書きする（現在値も Normal として即座に反映する）。</summary>
        public ButtonMotion SetNormal(Vector3 scale, Vector2 anchoredPosition)
        {
            EnsureInitialized();
            _baseScale = scale;
            _basePosition = anchoredPosition;
            Apply();
            return this;
        }

        /// <summary>動かす RectTransform を変更する。変更前の対象は Normal の値へ戻し、新しい対象から Normal を取得する。</summary>
        public ButtonMotion SetTarget(RectTransform target)
        {
            if (_initialized)
            {
                ResetImmediate();
            }

            _target = target;
            _initialized = true;
            CaptureNormal();
            if (isActiveAndEnabled)
            {
                GoTo(Evaluate(), true);
            }
            return this;
        }

        /// <summary>状態の取得元 Selectable を変更する。<c>null</c> を渡すと Disabled 検知とクリック連動を無効にする。</summary>
        public ButtonMotion SetSelectable(Selectable selectable)
        {
            Unsubscribe();
            _selectable = selectable;
            _button = selectable as Button;
            if (isActiveAndEnabled)
            {
                Subscribe();
                Refresh();
            }
            return this;
        }

        /// <summary>ホバー時のスケール倍率（均一）を設定する。</summary>
        public ButtonMotion SetHoverScale(float scale) => SetHoverScale(new Vector3(scale, scale, 1f));

        /// <summary>ホバー時のスケール倍率を設定する。</summary>
        public ButtonMotion SetHoverScale(Vector3 scale)
        {
            SettingsRef.hover.enabled = true;
            SettingsRef.hover.scale = scale;
            return ApplySettingsChange();
        }

        /// <summary>ホバー時の位置オフセットを設定する。</summary>
        public ButtonMotion SetHoverOffset(Vector2 offset)
        {
            SettingsRef.hover.enabled = true;
            SettingsRef.hover.offset = offset;
            return ApplySettingsChange();
        }

        /// <summary>押下時のスケール倍率（均一）を設定する。</summary>
        public ButtonMotion SetPressedScale(float scale) => SetPressedScale(new Vector3(scale, scale, 1f));

        /// <summary>押下時のスケール倍率を設定する。</summary>
        public ButtonMotion SetPressedScale(Vector3 scale)
        {
            SettingsRef.pressed.enabled = true;
            SettingsRef.pressed.scale = scale;
            return ApplySettingsChange();
        }

        /// <summary>押下時の位置オフセット（例：<c>new Vector2(0, -4)</c>）を設定する。</summary>
        public ButtonMotion SetPressedOffset(Vector2 offset)
        {
            SettingsRef.pressed.enabled = true;
            SettingsRef.pressed.offset = offset;
            return ApplySettingsChange();
        }

        /// <summary>無効時のスケール倍率（均一）を設定する。</summary>
        public ButtonMotion SetDisabledScale(float scale)
        {
            SettingsRef.disabled.enabled = true;
            SettingsRef.disabled.scale = new Vector3(scale, scale, 1f);
            return ApplySettingsChange();
        }

        /// <summary>無効時の位置オフセットを設定する。</summary>
        public ButtonMotion SetDisabledOffset(Vector2 offset)
        {
            SettingsRef.disabled.enabled = true;
            SettingsRef.disabled.offset = offset;
            return ApplySettingsChange();
        }

        /// <summary>選択（フォーカス）時のスケール倍率（均一）を設定する。</summary>
        public ButtonMotion SetSelectedScale(float scale)
        {
            SettingsRef.selected.enabled = true;
            SettingsRef.selected.scale = new Vector3(scale, scale, 1f);
            return ApplySettingsChange();
        }

        /// <summary>選択（フォーカス）時の位置オフセットを設定する。</summary>
        public ButtonMotion SetSelectedOffset(Vector2 offset)
        {
            SettingsRef.selected.enabled = true;
            SettingsRef.selected.offset = offset;
            return ApplySettingsChange();
        }

        /// <summary>クリック演出の有効 / 無効を設定する。</summary>
        public ButtonMotion SetClickPunchEnabled(bool enabled)
        {
            SettingsRef.click.enabled = enabled;
            return this;
        }

        /// <summary>クリック演出の強さ・再生時間を設定する（同時に有効化する）。</summary>
        public ButtonMotion SetClickPunch(float strength, float duration)
        {
            SettingsRef.click.enabled = true;
            SettingsRef.click.strength = strength;
            SettingsRef.click.motion = SettingsRef.click.motion.WithDuration(duration);
            return this;
        }

        #endregion

        /// <summary>
        /// クリック演出を開始する。基底の <see cref="HisuiMotionBehaviour.Play"/> から呼ばれる。
        /// 状態遷移のモーションとは独立しており、進行中の状態遷移は妨げない。
        /// </summary>
        protected override void OnPlayCore()
        {
            EnsureInitialized();
            _punchHandle.TryCancelAndClear();
            _punchVersion++;
            _punch = 0f;

            ref var click = ref SettingsRef.click;
            if (!click.enabled || click.motion.duration <= 0f || Mathf.Approximately(click.strength, 0f))
            {
                Apply();
                return;
            }

            _punchStrength = click.strength;
            _punchVibrato = Mathf.Max(0.5f, click.vibrato);

            _punchHandle = HisuiMotionExtensions
                .CreateFloat(0f, 1f, click.motion, IgnoreTimeScale)
                .Bind(this, s_punchTick);
        }

        /// <inheritdoc />
        protected override void OnSettingsChanged()
        {
            ApplySettingsChange();
        }

        private ButtonMotion ApplySettingsChange()
        {
            if (_initialized && isActiveAndEnabled)
            {
                // 現在の状態の目標値を、新しい設定で張り替える。
                GoTo(_currentState, false);
            }
            return this;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_selectable == null)
            {
                TryGetComponent(out _selectable);
            }
            _button = _selectable as Button;
            _onClickAction ??= OnButtonClick;
            CaptureNormal();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _button = _selectable as Button;
            if (_button != null)
            {
                _button.onClick.AddListener(_onClickAction);
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_button != null)
            {
                _button.onClick.RemoveListener(_onClickAction);
            }
            _subscribed = false;
        }

        private void OnButtonClick()
        {
            // 演出完了を待たず、onClick はそのまま通る。無効時は Button 側が発火しないので演出も出ない。
            if (SettingsRef.click.enabled)
            {
                PlayClick();
            }
        }

        private bool IsInteractableNow()
        {
            return _selectable == null || _selectable.IsInteractable();
        }

        private ButtonMotionState Evaluate()
        {
            if (!IsInteractableNow())
            {
                return ButtonMotionState.Disabled;
            }

            if (_pointerDown && _hover)
            {
                return ButtonMotionState.Pressed;
            }

            if (_hover)
            {
                return ButtonMotionState.Hover;
            }

            return _selectedFlag ? ButtonMotionState.Selected : ButtonMotionState.Normal;
        }

        private void GoTo(ButtonMotionState state, bool immediate)
        {
            _currentState = state;

            Vector3 toScale = Vector3.one;
            Vector2 toOffset = Vector2.zero;
            MotionSpec spec = SettingsRef.normalMotion;

            if (state != ButtonMotionState.Normal)
            {
                StateSettings s = GetStateSettings(state);
                spec = s.motion;
                if (s.enabled)
                {
                    toScale = s.scale;
                    toOffset = s.offset;
                }
            }

            // 現在値から張り替える（リセットしない）。
            _stateHandle.TryCancelAndClear();
            _toScale = toScale;
            _toOffset = toOffset;

            if (immediate || spec.duration <= 0f)
            {
                _curScale = toScale;
                _curOffset = toOffset;
                Apply();
                return;
            }

            _fromScale = _curScale;
            _fromOffset = _curOffset;
            _stateHandle = HisuiMotionExtensions
                .CreateFloat(0f, 1f, spec, IgnoreTimeScale)
                .Bind(this, s_stateTick);
        }

        private StateSettings GetStateSettings(ButtonMotionState state)
        {
            switch (state)
            {
                case ButtonMotionState.Hover: return SettingsRef.hover;
                case ButtonMotionState.Pressed: return SettingsRef.pressed;
                case ButtonMotionState.Disabled: return SettingsRef.disabled;
                case ButtonMotionState.Selected: return SettingsRef.selected;
                default: return default;
            }
        }

        private void OnStateTick(float t)
        {
            if (this == null)
            {
                return;
            }

            // オーバーシュート系 Ease を潰さないよう、クランプせず補間する。
            _curScale = Vector3.LerpUnclamped(_fromScale, _toScale, t);
            _curOffset = Vector2.LerpUnclamped(_fromOffset, _toOffset, t);
            Apply();
        }

        private void OnPunchTick(float t)
        {
            if (this == null)
            {
                return;
            }

            // 終端で 0 に収束する減衰振動。t = 1 では必ず 0 になり、基準値へ戻る経路が保たれる。
            _punch = _punchStrength * (1f - t) * Mathf.Sin(Mathf.PI * _punchVibrato * t);
            Apply();
        }

        private void CancelPunch(int version)
        {
            if (_punchVersion != version)
            {
                return;
            }

            _punchHandle.TryCancelAndClear();
            _punchVersion++;
            _punch = 0f;
            Apply();
        }

        private void Apply()
        {
            var rt = Target;
            if (rt == null)
            {
                return;
            }

            rt.localScale = Vector3.Scale(_baseScale, _curScale) * (1f + _punch);
            rt.anchoredPosition = _basePosition + _curOffset;
        }
    }
}
