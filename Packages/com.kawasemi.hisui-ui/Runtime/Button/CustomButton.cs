using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hisui.UI.Components
{
    /// <summary>
    /// クリック後に無効化（interactable = false）されたボタンを、再び有効に戻す契機。
    /// </summary>
    public enum InteractableRestoreMode
    {
        /// <summary>スクリプトから明示的に interactable = true や RestoreInteractable() を呼ぶまで無効を維持します。</summary>
        Manual = 0,

        /// <summary>GameObject が再アクティブ化（OnEnable）されたときに自動で interactable = true に復帰します。</summary>
        OnEnable = 1,
    }

    /// <summary>
    /// 連打防止・フォーカス解除・サウンド連携を備えたカスタムボタン。
    /// <para>
    /// <b>役割分担：</b><br/>
    /// ・スケールや位置オフセットなどの演出は <see cref="ButtonMotion"/> が担当します。<br/>
    /// ・本コンポーネントは「連打防止（クールダウン）」「クリック後の無効化」「マウスクリック時の選択（フォーカス）自動解除」「サウンド連携フック」を担当します。<br/>
    /// ・標準の <see cref="Button"/> を継承しているため、既存の Button からそのまま置き換え可能です。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Custom Button")]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CustomButton : Button
    {
        // =========================================================
        // Inspector Fields
        // =========================================================

        [Header("連打防止")]
        [Tooltip("連打防止を有効にするか")]
        [SerializeField] private bool _useCooldown = true;

        [Tooltip("クリックを受け付ける最短間隔（秒）。TimeScale に依存せず判定されます")]
        [SerializeField, Min(0f)] private float _cooldownTime = 0.3f;

        [Header("インタラクティブ制御")]
        [Tooltip("クリック時に自動で interactable を false にするか")]
        [SerializeField] private bool _disableOnClick = false;

        [Tooltip("クリック後に無効化されたボタンを再び有効（interactable = true）に戻す契機")]
        [SerializeField] private InteractableRestoreMode _restoreMode = InteractableRestoreMode.OnEnable;

        [Header("フォーカス")]
        [Tooltip("マウスクリック（ポインタ操作）時に Selected 状態（フォーカス枠など）を自動解除するか")]
        [SerializeField] private bool _deselectOnPointerClick = true;

        [Header("サウンド連携")]
        [Tooltip("同じオブジェクト等のサウンドコンポーネント（IButtonSoundHandler 実装）に渡す識別キー（任意）")]
        [SerializeField] private string _soundKey = string.Empty;

        // =========================================================
        // Private State
        // =========================================================

        private float _lastClickTime = float.NegativeInfinity;
        private IButtonSoundHandler _soundHandler;

        // =========================================================
        // Events
        // =========================================================

        /// <summary>
        /// 連打防止チェックを通過した有効なクリックが行われたときに発火する C# イベント。
        /// </summary>
        public event Action<CustomButton> OnValidClick;

        // =========================================================
        // Properties
        // =========================================================

        /// <summary>連打防止が有効かどうか。</summary>
        public bool UseCooldown
        {
            get => _useCooldown;
            set => _useCooldown = value;
        }

        /// <summary>クリックを受け付ける最短間隔（秒）。</summary>
        public float CooldownTime
        {
            get => _cooldownTime;
            set => _cooldownTime = Mathf.Max(0f, value);
        }

        /// <summary>クリック時に自動で interactable を false にするかどうか。</summary>
        public bool DisableOnClick
        {
            get => _disableOnClick;
            set => _disableOnClick = value;
        }

        /// <summary>クリック後に無効化されたボタンを再び有効に戻す契機。</summary>
        public InteractableRestoreMode RestoreMode
        {
            get => _restoreMode;
            set => _restoreMode = value;
        }

        /// <summary>マウスクリック時に Selected 状態を自動解除するかどうか。</summary>
        public bool DeselectOnPointerClick
        {
            get => _deselectOnPointerClick;
            set => _deselectOnPointerClick = value;
        }

        /// <summary>サウンドハンドラーに渡す識別キー。</summary>
        public string SoundKey
        {
            get => _soundKey;
            set => _soundKey = value;
        }

        /// <summary>サウンドハンドラーの参照（手動差し替え可能）。</summary>
        public IButtonSoundHandler SoundHandler
        {
            get => _soundHandler;
            set => _soundHandler = value;
        }

        /// <summary>現在クールダウン中（連打防止待機中）かどうか。</summary>
        public bool IsInCooldown => _useCooldown && (Time.unscaledTime - _lastClickTime < _cooldownTime);

        /// <summary>クールダウンが明けるまでの残り時間（秒）。クールダウン中でない場合は 0。</summary>
        public float RemainingCooldownTime
        {
            get
            {
                if (!_useCooldown) return 0f;
                float elapsed = Time.unscaledTime - _lastClickTime;
                return elapsed < _cooldownTime ? _cooldownTime - elapsed : 0f;
            }
        }

        // =========================================================
        // Lifecycle
        // =========================================================

        protected override void Awake()
        {
            base.Awake();
            if (_soundHandler == null)
            {
                _soundHandler = GetComponent<IButtonSoundHandler>();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // 再表示時に自動で押せるように復帰する設定の場合
            if (Application.isPlaying && _disableOnClick && _restoreMode == InteractableRestoreMode.OnEnable)
            {
                interactable = true;
            }
        }

        // =========================================================
        // Event Handlers
        // =========================================================

        /// <summary>
        /// ポインタ（マウス / タッチ）によるクリック時の処理。
        /// 連打防止チェック → サウンド再生 → OnValidClick 発火 → base.OnPointerClick（onClick 発火） → 選択解除 → 無効化 を実行します。
        /// </summary>
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;

            // 連打防止チェック
            if (IsInCooldown)
            {
                // クールダウン中でクリックを無視する場合でも、ポインタ操作による選択（フォーカス）は解除する
                TryDeselect();
                return;
            }
            _lastClickTime = Time.unscaledTime;

            // サウンド連携
            PlayClickSound();

            // イベント通知
            OnValidClick?.Invoke(this);

            // 標準の onClick.Invoke() を呼ぶ
            base.OnPointerClick(eventData);

            // マウスクリック後の Selected 状態解除（フォーカスが残り続けるのを防ぐ）
            TryDeselect();

            // 押下後の自動無効化
            if (_disableOnClick)
            {
                interactable = false;
            }
        }

        private void TryDeselect()
        {
            if (_deselectOnPointerClick && EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject == gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        /// <summary>
        /// キーボード・ゲームパッドの決定キー（Submit）による押下時の処理。
        /// ゲームパッド等のフォーカス遷移を損なわないよう、フォーカス解除は行いません。
        /// </summary>
        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;

            // 連打防止チェック
            if (IsInCooldown) return;
            _lastClickTime = Time.unscaledTime;

            // サウンド連携
            PlayClickSound();

            // イベント通知
            OnValidClick?.Invoke(this);

            // 標準の onClick.Invoke() を呼ぶ
            base.OnSubmit(eventData);

            // 押下後の自動無効化
            if (_disableOnClick)
            {
                interactable = false;
            }
        }

        // =========================================================
        // Public & Virtual Methods
        // =========================================================

        /// <summary>
        /// クリック時のサウンド再生処理。
        /// 仮想メソッドのため、派生クラスで独自のサウンドマネージャーを直接呼ぶ形にオーバーライドすることも可能です。
        /// </summary>
        public virtual void PlayClickSound()
        {
            _soundHandler?.OnPlayClickSound(this);
        }

        /// <summary>
        /// クールダウンタイマーを即座にリセットし、直後からクリックを受け付け可能にします。
        /// </summary>
        public void ResetCooldown()
        {
            _lastClickTime = float.NegativeInfinity;
        }

        /// <summary>
        /// 連打防止の設定を変更します。
        /// </summary>
        public CustomButton SetCooldown(bool enabled, float cooldownTime = 0.3f)
        {
            _useCooldown = enabled;
            _cooldownTime = Mathf.Max(0f, cooldownTime);
            return this;
        }

        /// <summary>
        /// クリック後の自動無効化設定を変更します。
        /// </summary>
        public CustomButton SetDisableOnClick(bool enabled, InteractableRestoreMode restoreMode = InteractableRestoreMode.OnEnable)
        {
            _disableOnClick = enabled;
            _restoreMode = restoreMode;
            return this;
        }

        /// <summary>
        /// ボタンの interactable を true に復帰させます。
        /// </summary>
        public void RestoreInteractable()
        {
            interactable = true;
        }

        /// <summary>
        /// interactable の設定を変更します。
        /// </summary>
        public CustomButton SetInteractable(bool value)
        {
            interactable = value;
            return this;
        }

        /// <summary>
        /// マウスクリック時の選択解除設定を変更します。
        /// </summary>
        public CustomButton SetDeselectOnPointerClick(bool enabled)
        {
            _deselectOnPointerClick = enabled;
            return this;
        }

        /// <summary>
        /// サウンドキーを設定します。
        /// </summary>
        public CustomButton SetSoundKey(string soundKey)
        {
            _soundKey = soundKey;
            return this;
        }

        /// <summary>
        /// ボタンが有効クリックされるまで非同期で待機します。
        /// <para>
        /// HisuiUI の設計方針に従い、GameObject 破棄や <paramref name="ct"/> による中断時にも
        /// 例外（<see cref="OperationCanceledException"/>）をスローせず静かに早期リターンします。
        /// </para>
        /// </summary>
        /// <param name="ct">キャンセレーショントークン</param>
        public async UniTask WaitClickAsync(CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested || !this) return;

            var tcs = new UniTaskCompletionSource();
            void HandleClick(CustomButton _) => tcs.TrySetResult();

            OnValidClick += HandleClick;

            // ct 中断またはオブジェクト破棄時に待機を静かに終了する
            var destroyCt = this.GetCancellationTokenOnDestroy();
            CancellationTokenRegistration reg1 = default;
            CancellationTokenRegistration reg2 = default;

            if (ct.CanBeCanceled)
            {
                reg1 = ct.Register(static state => ((UniTaskCompletionSource)state).TrySetResult(), tcs);
            }
            if (destroyCt.CanBeCanceled)
            {
                reg2 = destroyCt.Register(static state => ((UniTaskCompletionSource)state).TrySetResult(), tcs);
            }

            try
            {
                await tcs.Task;
            }
            finally
            {
                reg1.Dispose();
                reg2.Dispose();
                OnValidClick -= HandleClick;
            }
        }
    }
}
