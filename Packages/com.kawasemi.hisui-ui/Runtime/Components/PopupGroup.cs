using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;

namespace Hisui.UI.Components
{
    /// <summary>
    /// 複数の <see cref="Popup"/> コンポーネントを束ね、時間差（Stagger）付きの登場・退場やグループ自動退場を制御するコンポーネント。
    /// <para>
    /// <b>特徴：</b><br/>
    /// ・配下の各 <see cref="Popup"/> は自身の閉じる <c>Button</c> 等で個別に独立して退場可能。<br/>
    /// ・グループからの <see cref="PlayOut"/> 時は、すでに個別退場済みの要素をスキップし、現在表示中の要素のみを Stagger 付きで退場させる。<br/>
    /// ・時間経過による自動退場（Auto Out）は「各要素の In 完了から個別に順次退場」と「全要素の In 完了後に一括で Stagger 退場」を選択可能。<br/>
    /// ・すべての <see cref="Popup"/> が退場完了した時点でのグループ自身の非アクティブ化（<see cref="Settings.deactivateGroupOnAllClosed"/>）に対応。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Popup Group")]
    [DisallowMultipleComponent]
    public sealed class PopupGroup : HisuiMotionBehaviour<PopupGroup, PopupGroup.Settings>
    {
        /// <summary>
        /// 複数 <see cref="Popup"/> に対する時間経過自動退場（Auto Out）の動作モード。
        /// </summary>
        public enum GroupAutoOutMode
        {
            /// <summary>各 <see cref="Popup"/> 個別に設定された <see cref="Popup.Settings.autoOutDelay"/> に従って個別に退場する。</summary>
            UseEachPopupSetting = 0,

            /// <summary>グループ側の <see cref="Settings.autoOutDelay"/> 秒数を使用し、各 <see cref="Popup"/> が自身の In 完了時点から個別にカウントして順次退場する。</summary>
            EachPopupOnInComplete = 1,

            /// <summary>すべての <see cref="Popup"/> の In が完了してから <see cref="Settings.autoOutDelay"/> 秒待機し、残っている要素を Stagger 付きで一括退場させる。</summary>
            AllAfterLastInComplete = 2,

            /// <summary>時間経過による自動退場を行わない（各 <see cref="Popup"/> の自動退場も抑止し、Button またはスクリプトからの退場のみとする）。</summary>
            Disabled = 3,
        }

        /// <summary>
        /// <see cref="PopupGroup"/> の全設定を保持するシリアライズ可能構造体。
        /// </summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>登場（In）時の各 <see cref="Popup"/> の開始時間間隔（秒）。0 で同時。</summary>
            [Header("In Stagger")]
            [Tooltip("登場（In）時の各 Popup の開始時間間隔（秒）。0 で同時")]
            [Min(0f)]
            public float inStaggerInterval;

            /// <summary>登場（In）時の Stagger 順序。</summary>
            [Tooltip("登場（In）時の Stagger 順序（Forward / Reverse / FromCenter）")]
            public StaggerOrder inStaggerOrder;

            /// <summary>退場（Out）時の各 <see cref="Popup"/> の開始時間間隔（秒）。0 で同時。</summary>
            [Header("Out Stagger")]
            [Tooltip("退場（Out）時の各 Popup の開始時間間隔（秒）。0 で同時")]
            [Min(0f)]
            public float outStaggerInterval;

            /// <summary>退場（Out）時の Stagger 順序。</summary>
            [Tooltip("退場（Out）時の Stagger 順序（Forward / Reverse / FromCenter）")]
            public StaggerOrder outStaggerOrder;

            /// <summary>自動再生トリガーが OnEnable のときの開始遅延（秒）。</summary>
            [Header("Initial & Lifecycle")]
            [Tooltip("自動再生トリガーが OnEnable のときの開始遅延（秒）")]
            [Min(0f)]
            public float startDelay;

            /// <summary>時間経過による自動退場（Auto Out）の動作モード。</summary>
            [Tooltip("自動退場の動作モード（各Popup設定を使用 / 各In完了から順次退場 / 全In完了後に一括退場 / 無効）")]
            public GroupAutoOutMode autoOutMode;

            /// <summary>
            /// <see cref="autoOutMode"/> が <see cref="GroupAutoOutMode.EachPopupOnInComplete"/> または <see cref="GroupAutoOutMode.AllAfterLastInComplete"/> のときの待機秒数。
            /// </summary>
            [Tooltip("自動退場までの待機秒数（EachPopupOnInComplete または AllAfterLastInComplete 選択時に使用）")]
            [Min(0f)]
            public float autoOutDelay;

            /// <summary>配下のすべての <see cref="Popup"/> が退場完了した時点で、この <see cref="PopupGroup"/> の GameObject も SetActive(false) にするか。</summary>
            [Tooltip("配下のすべての Popup が退場完了した時点で、この PopupGroup の GameObject も SetActive(false) にするか")]
            public bool deactivateGroupOnAllClosed;

            /// <summary>標準的なデフォルト設定。</summary>
            public static Settings Default => new Settings
            {
                inStaggerInterval = 0.08f,
                inStaggerOrder = StaggerOrder.Forward,
                outStaggerInterval = 0.05f,
                outStaggerOrder = StaggerOrder.Reverse,
                startDelay = 0f,
                autoOutMode = GroupAutoOutMode.UseEachPopupSetting,
                autoOutDelay = 2.0f,
                deactivateGroupOnAllClosed = false,
            };
        }

        [Header("Target Popups")]
        [Tooltip("管理対象の Popup リスト。空の場合は Awake 時に子階層から自動収集される。インスペクタ下部のボタンで事前登録も可能")]
        [SerializeField]
        private List<Popup> _popups = new List<Popup>();

        private bool _initialized;
        private bool _lastWasIn;
        private bool _suppressOnEnableAutoPlay;
        private bool _groupAutoOutArmed;
        private float _groupAutoOutRemaining;
        private Action _onGroupDriverCompleteDelegate;

        private static readonly Action<float, PopupGroup> s_noopTickCallback = static (_, _) => { };

        /// <summary>管理対象の <see cref="Popup"/> リスト。</summary>
        public List<Popup> Popups => _popups;

        /// <summary>最後に要求されたグループ状態が In（登場）側かどうか。</summary>
        public bool IsIn => _lastWasIn;

        /// <inheritdoc />
        public override bool IsPlaying => base.IsPlaying || AnyPopupPlaying();

        private void Reset()
        {
            SettingsRef = Settings.Default;
            CollectChildPopups(includeInactive: true);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();

            if (_suppressOnEnableAutoPlay)
            {
                return;
            }

            if (AutoPlayTrigger == PlayTrigger.OnEnable)
            {
                CancelTrackedMotionsAndIncrementVersion();
                StartGroupTransition(toIn: true, extraDelay: Mathf.Max(0f, SettingsRef.startDelay));
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            UnregisterPopupEvents();
            base.OnDestroy();
        }

        /// <inheritdoc />
        public override void Stop()
        {
            _groupAutoOutArmed = false;
            base.Stop();

            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p != null)
                {
                    p.Stop();
                }
            }
        }

        private void Update()
        {
            if (!_groupAutoOutArmed)
            {
                return;
            }

            _groupAutoOutRemaining -= IgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_groupAutoOutRemaining <= 0f)
            {
                _groupAutoOutArmed = false;
                PlayOut();
            }
        }

        /// <summary>
        /// 子階層（自身を除く、または自身を含む配下すべて）から <see cref="Popup"/> コンポーネントを検索し、<see cref="Popups"/> リストに登録する。
        /// </summary>
        /// <param name="includeInactive">非アクティブな子オブジェクトも対象にするか。</param>
        /// <returns>メソッドチェーン用の自身への参照。</returns>
        public PopupGroup CollectChildPopups(bool includeInactive = true)
        {
            if (Application.isPlaying && _initialized)
            {
                UnregisterPopupEvents();
            }

            _popups.Clear();
            GetComponentsInChildren(includeInactive, _popups);

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                if (_popups[i] == null)
                {
                    _popups.RemoveAt(i);
                }
            }

            if (Application.isPlaying && _initialized)
            {
                RegisterPopupEvents();
                SyncPopupGroupSettings();
            }

            return this;
        }

        /// <summary><see cref="HisuiMotionBehaviour.Play"/> は <see cref="PlayIn"/> と同義。</summary>
        protected override void OnPlayCore()
        {
            PrepareGroupForInIfInactive();
            StartGroupTransition(toIn: true, extraDelay: 0f);
        }

        /// <summary>
        /// 配下の全 <see cref="Popup"/> を Stagger 付きで登場させる。<see cref="HisuiMotionBehaviour.Play"/> と同義。
        /// 非アクティブになっている要素も自動的にアクティブ化して登場させる。
        /// </summary>
        public void PlayIn()
        {
            PrepareGroupForInIfInactive();
            Play();
        }

        /// <summary>
        /// 現在表示中（または登場中）の <see cref="Popup"/> を Stagger 付きで退場させる。
        /// すでに個別の Button 等で退場済みの要素はスキップされる。
        /// </summary>
        public void PlayOut()
        {
            EnsureInitialized();

            if (!gameObject.activeInHierarchy)
            {
                SetOutImmediate();
                return;
            }

            CancelTrackedMotionsAndIncrementVersion();
            StartGroupTransition(toIn: false, extraDelay: 0f);
        }

        /// <summary>
        /// 配下の全 <see cref="Popup"/> の登場を開始し、すべての要素が到着するまで待機する。
        /// 中断・上書き・破棄時も例外を投げず静かに完了する。
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
        /// 現在表示中の <see cref="Popup"/> の退場を開始し、すべての要素が退場完了するまで待機する。
        /// 中断・上書き・破棄時も例外を投げず静かに完了する。
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

        /// <summary>演出なしで、配下の全 <see cref="Popup"/> を即座に到着（In 完了）状態にする。</summary>
        public void SetInImmediate()
        {
            _groupAutoOutArmed = false;
            CancelTrackedMotionsAndIncrementVersion();
            PrepareGroupForInIfInactive();

            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p != null)
                {
                    p.SetInImmediate();
                }
            }

            _lastWasIn = true;
        }

        /// <summary>演出なしで、配下の全 <see cref="Popup"/> を即座に退場（Out 完了）状態にする。</summary>
        public void SetOutImmediate()
        {
            _groupAutoOutArmed = false;
            CancelTrackedMotionsAndIncrementVersion();
            EnsureInitialized();

            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p != null)
                {
                    p.SetOutImmediate();
                }
            }

            _lastWasIn = false;

            if (SettingsRef.deactivateGroupOnAllClosed && Application.isPlaying && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            if (_popups.Count == 0)
            {
                CollectChildPopups(includeInactive: true);
            }

            RegisterPopupEvents();
            SyncPopupGroupSettings();
        }

        private void RegisterPopupEvents()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p == null)
                {
                    continue;
                }

                p.ManagedByGroup = true;
                p.OnOutCompleted -= OnChildPopupOutCompleted;
                p.OnOutCompleted += OnChildPopupOutCompleted;
            }
        }

        private void UnregisterPopupEvents()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p == null)
                {
                    continue;
                }

                p.OnOutCompleted -= OnChildPopupOutCompleted;
            }
        }

        private void SyncPopupGroupSettings()
        {
            ref var s = ref SettingsRef;

            Popup.GroupAutoOutOverride overrideMode;
            float overrideDelay = s.autoOutDelay;

            switch (s.autoOutMode)
            {
                case GroupAutoOutMode.EachPopupOnInComplete:
                    overrideMode = Popup.GroupAutoOutOverride.UseGroupDelay;
                    break;
                case GroupAutoOutMode.AllAfterLastInComplete:
                case GroupAutoOutMode.Disabled:
                    overrideMode = Popup.GroupAutoOutOverride.Suppress;
                    break;
                default:
                    overrideMode = Popup.GroupAutoOutOverride.None;
                    break;
            }

            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p == null)
                {
                    continue;
                }

                p.ManagedByGroup = true;
                p.SetGroupAutoOutOverride(overrideMode, overrideDelay);
            }
        }

        private void PrepareGroupForInIfInactive()
        {
            if (!gameObject.activeSelf)
            {
                _suppressOnEnableAutoPlay = true;
                gameObject.SetActive(true);
                _suppressOnEnableAutoPlay = false;
            }

            EnsureInitialized();
        }

        private void StartGroupTransition(bool toIn, float extraDelay)
        {
            _groupAutoOutArmed = false;
            EnsureInitialized();
            SyncPopupGroupSettings();

            ref var s = ref SettingsRef;
            _lastWasIn = toIn;

            int movingCount = 0;
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p == null)
                {
                    continue;
                }

                if (toIn || p.IsVisibleOrEntering)
                {
                    movingCount++;
                }
            }

            if (movingCount == 0)
            {
                if (!toIn)
                {
                    CheckAllClosedAndDeactivateIfNeeded();
                }
                return;
            }

            float maxTotalDuration = 0f;
            int rank = 0;

            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p == null)
                {
                    continue;
                }

                if (!toIn && !p.IsVisibleOrEntering)
                {
                    continue;
                }

                float staggerDelay = toIn
                    ? StaggerUtility.GetDelay(rank, movingCount, s.inStaggerOrder, s.inStaggerInterval)
                    : StaggerUtility.GetDelay(rank, movingCount, s.outStaggerOrder, s.outStaggerInterval);

                float totalDelay = extraDelay + staggerDelay;

                if (toIn)
                {
                    p.PlayInWithExtraDelay(totalDelay);
                    ref var ps = ref p.SettingsRef;
                    maxTotalDuration = Mathf.Max(maxTotalDuration, totalDelay + ps.inMotion.delay + ps.inMotion.duration);
                }
                else
                {
                    p.PlayOutWithExtraDelay(totalDelay);
                    ref var ps = ref p.SettingsRef;
                    maxTotalDuration = Mathf.Max(maxTotalDuration, totalDelay + ps.outMotion.delay + ps.outMotion.duration);
                }

                rank++;
            }

            if (maxTotalDuration <= 0f)
            {
                OnGroupDriverComplete();
                return;
            }

            _onGroupDriverCompleteDelegate ??= OnGroupDriverComplete;

            var driver = HisuiMotionExtensions
                .CreateFloat(0f, maxTotalDuration, new MotionSpec(maxTotalDuration, Ease.Linear, 0f), IgnoreTimeScale)
                .WithOnComplete(_onGroupDriverCompleteDelegate)
                .Bind(this, s_noopTickCallback);

            TrackMotion(driver);
        }

        private void OnGroupDriverComplete()
        {
            if (this == null)
            {
                return;
            }

            ref var s = ref SettingsRef;
            if (_lastWasIn && s.autoOutMode == GroupAutoOutMode.AllAfterLastInComplete && s.autoOutDelay >= 0f)
            {
                if (!AllPopupsHidden())
                {
                    _groupAutoOutArmed = true;
                    _groupAutoOutRemaining = s.autoOutDelay;
                }
            }
            else if (!_lastWasIn)
            {
                CheckAllClosedAndDeactivateIfNeeded();
            }
        }

        private void OnChildPopupOutCompleted(Popup popup)
        {
            if (this == null)
            {
                return;
            }

            CheckAllClosedAndDeactivateIfNeeded();
        }

        private void CheckAllClosedAndDeactivateIfNeeded()
        {
            if (!AllPopupsHidden())
            {
                return;
            }

            _groupAutoOutArmed = false;
            _lastWasIn = false;

            if (SettingsRef.deactivateGroupOnAllClosed && Application.isPlaying && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private bool AnyPopupPlaying()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p != null && p.IsPlaying)
                {
                    return true;
                }
            }
            return false;
        }

        private bool AllPopupsHidden()
        {
            for (int i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                if (p != null && p.IsVisibleOrEntering)
                {
                    return false;
                }
            }
            return true;
        }

        /// <inheritdoc />
        protected override void OnSettingsChanged()
        {
            if (_initialized)
            {
                SyncPopupGroupSettings();
            }
        }

        #region Fluent Configuration API

        /// <summary>In / Out 両方の Stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetStagger(float interval, StaggerOrder order = StaggerOrder.Forward)
        {
            SetInStagger(interval, order);
            SetOutStagger(interval, order);
            return this;
        }

        /// <summary>登場（In）の Stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetInStagger(float interval, StaggerOrder order = StaggerOrder.Forward)
        {
            SettingsRef.inStaggerInterval = Mathf.Max(0f, interval);
            SettingsRef.inStaggerOrder = order;
            return this;
        }

        /// <summary>退場（Out）の Stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetOutStagger(float interval, StaggerOrder order = StaggerOrder.Reverse)
        {
            SettingsRef.outStaggerInterval = Mathf.Max(0f, interval);
            SettingsRef.outStaggerOrder = order;
            return this;
        }

        /// <summary>OnEnable 自動再生時の開始遅延（秒）を設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetStartDelay(float delay)
        {
            SettingsRef.startDelay = Mathf.Max(0f, delay);
            return this;
        }

        /// <summary>グループの自動退場モードと待機秒数を設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetAutoOut(GroupAutoOutMode mode, float delay = 2.0f)
        {
            SettingsRef.autoOutMode = mode;
            SettingsRef.autoOutDelay = Mathf.Max(0f, delay);
            if (_initialized)
            {
                SyncPopupGroupSettings();
            }
            return this;
        }

        /// <summary>全 Popup 退場完了時に PopupGroup 自身の GameObject を SetActive(false) にするかを設定する（メソッドチェーン対応）。</summary>
        public PopupGroup SetDeactivateGroupOnAllClosed(bool deactivate)
        {
            SettingsRef.deactivateGroupOnAllClosed = deactivate;
            return this;
        }

        /// <summary>管理対象の <see cref="Popup"/> リストを置き換える（メソッドチェーン対応）。</summary>
        public PopupGroup SetPopups(IEnumerable<Popup> popups)
        {
            if (Application.isPlaying && _initialized)
            {
                UnregisterPopupEvents();
            }

            _popups.Clear();
            if (popups != null)
            {
                _popups.AddRange(popups);
            }

            if (Application.isPlaying && _initialized)
            {
                RegisterPopupEvents();
                SyncPopupGroupSettings();
            }

            return this;
        }

        #endregion
    }
}
