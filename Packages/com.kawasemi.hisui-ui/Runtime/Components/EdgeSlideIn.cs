using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Hisui.UI.Components
{
    /// <summary>
    /// UI 要素を「画面外から」登場させ、また画面端へ退場させる演出コンポーネント（タイトルロゴ・サイドメニュー・リザルト表示など）。
    /// <para>
    /// <b>PanelTransition との違い：</b>PanelTransition はパネルサイズ基準または絶対値で移動するが、
    /// EdgeSlideIn は <see cref="ScreenEdgeUtility"/> によりルート Canvas の矩形から対象が完全に外れる位置を
    /// <b>再生の直前に毎回</b>自動計算する（解像度変更・画面回転・Canvas Scaler の変化に追従）。
    /// </para>
    /// <para>
    /// <b>仕様：</b><br/>
    /// ・対応 Canvas は Screen Space Overlay / Screen Space Camera。World Space は対象外。<br/>
    /// ・<see cref="Targets"/> が空の場合は自分自身が対象。複数の場合は stagger（Forward / Reverse / FromCenter）で時間をずらす。<br/>
    /// ・In（登場）と Out（退場）は別々の <see cref="PhaseSettings"/>（duration / ease / delay / stagger）を持つ。<br/>
    /// ・到着位置は最初に取得した <see cref="RectTransform.anchoredPosition"/>（<see cref="CaptureRestPosition"/> で再取得）。<br/>
    /// ・逆方向の再生が途中で呼ばれた場合は現在位置から滑らかに反転する。stagger 中でまだ動き出していない Target は動き出さずそのまま待機する。<br/>
    /// ・フェードは Target 自身の <see cref="CanvasGroup"/>、無ければ <see cref="Graphic"/> の alpha を使う（どちらも無ければフェードしない。CanvasGroup は要求しない）。
    /// </para>
    /// <para>
    /// <b>オーバーシュートについて：</b>OutBack / Elastic などのオーバーシュートする Ease を選んだ場合、
    /// 到着位置を行き過ぎたり、開始側（画面外）をさらに越えて Target が一瞬画面外へ飛び出すことがある。これは意図した挙動として許容している
    /// （位置は <see cref="Vector2.LerpUnclamped"/> 相当で補間し、クランプしない。alpha のみ 0–1 にクランプする）。
    /// </para>
    /// <para>
    /// 親に LayoutGroup がある場合は <c>anchoredPosition</c> が毎回上書きされるため、ラッパー（空の RectTransform）を挟んで
    /// LayoutGroup の子をラッパーにし、本コンポーネントの Target はラッパーの子にすること。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Edge Slide In")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class EdgeSlideIn : HisuiMotionBehaviour<EdgeSlideIn, EdgeSlideIn.Settings>
    {
        /// <summary>
        /// In または Out 1 フェーズ分の設定。
        /// </summary>
        [Serializable]
        public struct PhaseSettings
        {
            /// <summary>Duration / Ease / Delay。Ease は OutBack などオーバーシュートするものも使える。</summary>
            [Tooltip("再生時間・イージング・開始遅延（OutBack 等のオーバーシュートも可。画面外へ飛び出す場合がある）")]
            public MotionSpec motion;

            /// <summary>複数 Target の開始時間の間隔（秒）。0 で全 Target 同時。</summary>
            [Tooltip("複数 Target の開始時間の間隔（秒）。0 で同時")]
            [Min(0f)]
            public float staggerInterval;

            /// <summary>stagger の順序（リストの順序基準）。</summary>
            [Tooltip("stagger の順序（Forward / Reverse / FromCenter）")]
            public StaggerOrder staggerOrder;
        }

        /// <summary>
        /// <see cref="EdgeSlideIn"/> の全設定。
        /// </summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>登場方向。</summary>
            [Header("Direction")]
            [Tooltip("どの画面端から登場するか")]
            public EdgeDirection direction;

            /// <summary><see cref="EdgeDirection.Custom"/> のときの方向ベクトル（正規化される）。</summary>
            [Tooltip("Custom 選択時の方向ベクトル（例: (-1, 1) で左上から）")]
            public Vector2 customDirection;

            /// <summary>true の場合、奇数番目（0 始まりで 1, 3, ...）の Target は方向を反転する（左右交互の登場など）。</summary>
            [Tooltip("true の場合、奇数番目の Target は方向を反転する（左右交互など）")]
            public bool alternateDirection;

            /// <summary>true の場合、退場の方向を <see cref="outDirection"/> で別指定する。false の場合は登場と同じ方向（来た方向）へ戻る。</summary>
            [Tooltip("true の場合、退場方向を別に指定する（例: 左から来て右へ退場）。false なら来た方向へ戻る")]
            public bool separateOutDirection;

            /// <summary>退場方向（<see cref="separateOutDirection"/> が true のとき有効）。</summary>
            [Tooltip("退場方向（Separate Out Direction が ON のとき有効）")]
            public EdgeDirection outDirection;

            /// <summary>退場方向が <see cref="EdgeDirection.Custom"/> のときの方向ベクトル（正規化される）。</summary>
            [Tooltip("退場方向が Custom のときの方向ベクトル")]
            public Vector2 outCustomDirection;

            /// <summary>画面端から追加で離す余白（Canvas ローカル単位）。影やアウトラインのはみ出し対策。</summary>
            [Tooltip("画面端から追加で離す余白。影やアウトラインがはみ出して見えるのを避ける")]
            [Min(0f)]
            public float extraMargin;

            /// <summary>再生前は画面外（非表示）にしておく。OnEnable の時点で配置してチラつきを防ぐ。</summary>
            [Header("Initial State")]
            [Tooltip("再生前は画面外で非表示にしておく（OnEnable 時点で配置）")]
            public bool startHidden;

            /// <summary>自動再生が OnEnable のときの開始遅延（秒）。</summary>
            [Tooltip("自動再生トリガーが OnEnable のときの開始遅延（秒）")]
            [Min(0f)]
            public float startDelay;

            /// <summary>
            /// In 完了後に自動で退場するまでの待機秒数。負の値（-1）で自動退場しない。0 以上なら In 完了からその秒数待って Out を開始する。
            /// </summary>
            [Tooltip("In 完了後に自動退場するまでの待機秒数。-1 で自動退場しない、0 以上で In 完了からその秒数後に退場")]
            public float autoOutDelay;

            /// <summary>途中で反転した際、残り距離に応じて Duration を短縮する。</summary>
            [Tooltip("途中で反転したとき、残り距離に応じて再生時間を短縮する")]
            public bool scaleDurationOnReverse;

            /// <summary>フェードを併用するか（CanvasGroup または Graphic の alpha）。</summary>
            [Header("Fade")]
            [Tooltip("alpha フェードを併用する（Target 自身の CanvasGroup、無ければ Graphic）")]
            public bool useFade;

            /// <summary>登場（In）の設定。</summary>
            [Header("In / Out")]
            [Tooltip("登場（In）の設定")]
            public PhaseSettings inPhase;

            /// <summary>退場（Out）の設定。</summary>
            [Tooltip("退場（Out）の設定")]
            public PhaseSettings outPhase;

            /// <summary>標準的なデフォルト設定。</summary>
            public static Settings Default => new Settings
            {
                direction = EdgeDirection.Left,
                customDirection = new Vector2(-1f, 1f),
                alternateDirection = false,
                separateOutDirection = false,
                outDirection = EdgeDirection.Right,
                outCustomDirection = new Vector2(1f, -1f),
                extraMargin = 0f,
                startHidden = true,
                startDelay = 0f,
                autoOutDelay = -1f,
                scaleDurationOnReverse = true,
                useFade = false,
                inPhase = new PhaseSettings
                {
                    motion = new MotionSpec(0.5f, Ease.OutCubic, 0f),
                    staggerInterval = 0.08f,
                    staggerOrder = StaggerOrder.Forward,
                },
                outPhase = new PhaseSettings
                {
                    motion = new MotionSpec(0.35f, Ease.InCubic, 0f),
                    staggerInterval = 0.05f,
                    staggerOrder = StaggerOrder.Reverse,
                },
            };
        }

        private struct TargetState
        {
            public RectTransform rt;
            public Vector2 rest;
            public Vector2 inOffset;    // 到着位置 → 登場元（画面外）の差分
            public Vector2 outOffset;   // 到着位置 → 退場先（画面外）の差分
            public Vector2 pos;         // 現在の anchoredPosition
            public Vector2 fromPos;
            public Vector2 toPos;
            public bool atOutSide;      // 画面外で待機中の位置が退場先側かどうか
            public float progress;      // 可視度（0 = 非表示, 1 = 到着。フェードと待機判定に使う。オーバーシュート時は範囲外もあり得る）
            public float from;
            public float to;
            public float start;
            public float duration;
            public bool moving;
            public CanvasGroup canvasGroup;
            public Graphic graphic;
            public float baseAlpha;
        }

        [Header("Targets (empty = this RectTransform)")]
        [Tooltip("登場させる RectTransform。空の場合は自分自身")]
        [SerializeField]
        private List<RectTransform> _targets = new List<RectTransform>();

        private TargetState[] _states = Array.Empty<TargetState>();
        private int _stateCount;
        private bool _lastWasIn;
        private MotionSpec _activeSpec;
        private Vector2 _lastCanvasSize;
        private bool _hasCanvasSize;
        private Action _onCompleteDelegate;
        private bool _autoOutArmed;
        private float _autoOutRemaining;

        private static readonly Action<float, EdgeSlideIn> s_tickCallback = static (t, self) => self.Tick(t);

        /// <summary>登場させる Target のリスト。空の場合は自分自身が対象。変更後は次回の再生時に反映される。</summary>
        public List<RectTransform> Targets => _targets;

        /// <summary>登場方向。</summary>
        public EdgeDirection Direction
        {
            get => SettingsRef.direction;
            set => SettingsRef.direction = value;
        }

        /// <summary>最後に要求された状態が In（登場）側かどうか。</summary>
        public bool IsIn => _lastWasIn;

        private void Reset()
        {
            SettingsRef = Settings.Default;
        }

        /// <inheritdoc />
        protected override void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureStates();

            if (SettingsRef.startHidden)
            {
                // 画面内に一瞬見えるチラつきを防ぐため、OnEnable の時点で登場元の画面外へ配置する。
                SetHidden(false);
            }

            if (AutoPlayTrigger == PlayTrigger.OnEnable)
            {
                CancelTrackedMotionsAndIncrementVersion();
                StartTransition(true, Mathf.Max(0f, SettingsRef.startDelay));
            }
        }

        /// <inheritdoc />
        public override void Stop()
        {
            _autoOutArmed = false;
            base.Stop();
        }

        private void Update()
        {
            if (_autoOutArmed)
            {
                _autoOutRemaining -= IgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                if (_autoOutRemaining <= 0f)
                {
                    _autoOutArmed = false;
                    PlayOut();
                    return;
                }
            }

            // 画面外で待機中に解像度などが変わっても、画面内に見えてしまわないよう追従する。
            if (_stateCount == 0 || IsPlaying || !AllHidden())
            {
                return;
            }

            if (CanvasSizeChanged())
            {
                RecalculateOffsets();
                ApplyAll();
            }
        }

        /// <summary><see cref="HisuiMotionBehaviour.Play"/> は <see cref="PlayIn"/> と同義。</summary>
        protected override void OnPlayCore()
        {
            StartTransition(true, 0f);
        }

        /// <summary>
        /// 登場（画面外 → 到着位置）を開始する。<see cref="HisuiMotionBehaviour.Play"/> と同義。
        /// 退場の途中であれば現在位置から滑らかに反転する。
        /// </summary>
        public void PlayIn()
        {
            Play();
        }

        /// <summary>
        /// 退場（到着位置 → 画面端の外）を開始する。登場の途中であれば現在位置から滑らかに反転する。
        /// </summary>
        public void PlayOut()
        {
            CancelTrackedMotionsAndIncrementVersion();
            StartTransition(false, 0f);
        }

        /// <summary>
        /// 登場を開始し、<b>全 Target が到着した時点</b>で完了する。中断・上書き・破棄時も例外を投げず静かに完了する。
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
        /// 退場を開始し、全 Target が画面外へ戻った時点で完了する。中断・上書き・破棄時も例外を投げず静かに完了する。
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

        /// <summary>演出なしで、全 Target を到着位置（登場済み）にする。</summary>
        public void SetInImmediate()
        {
            _autoOutArmed = false;
            CancelTrackedMotionsAndIncrementVersion();
            EnsureStates();
            for (int i = 0; i < _stateCount; i++)
            {
                _states[i].progress = 1f;
                _states[i].moving = false;
                _states[i].pos = _states[i].rest;
            }
            _lastWasIn = true;
            ApplyAll();
        }

        /// <summary>演出なしで、全 Target を画面外（退場済み。退場先の側）にする。画面外位置はこの時点で再計算する。</summary>
        public void SetOutImmediate()
        {
            SetHidden(true);
        }

        private void SetHidden(bool atOutSide)
        {
            _autoOutArmed = false;
            CancelTrackedMotionsAndIncrementVersion();
            EnsureStates();
            RecalculateOffsets();
            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                st.progress = 0f;
                st.moving = false;
                st.atOutSide = atOutSide;
                st.pos = st.rest + (atOutSide ? st.outOffset : st.inOffset);
            }
            _lastWasIn = false;
            ApplyAll();
        }

        /// <summary>
        /// 到着位置（基準位置）として、各 Target の現在の <c>anchoredPosition</c>（と alpha）を取得し直す。
        /// 画面外へ退避中に呼ぶと退避後の位置を拾ってしまうため、<see cref="SetInImmediate"/> の後など、
        /// 到着位置にある状態で呼ぶこと。
        /// </summary>
        public EdgeSlideIn CaptureRestPosition()
        {
            if (_stateCount == 0)
            {
                EnsureStates();
                return this;
            }

            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (st.rt == null)
                {
                    continue;
                }

                st.rest = st.rt.anchoredPosition;
                if (st.progress >= 0.999f)
                {
                    st.baseAlpha = ReadAlpha(in st);
                    st.pos = st.rest;
                }
            }
            return this;
        }

        private void StartTransition(bool toIn, float extraDelay)
        {
            _autoOutArmed = false;
            EnsureStates();
            RecalculateOffsets();

            ref var s = ref SettingsRef;
            PhaseSettings phase = toIn ? s.inPhase : s.outPhase;
            float toProgress = toIn ? 1f : 0f;

            // すでに到着済みの状態から In を呼んだ場合、および完全に非表示の状態から In を呼んだ場合は、
            // 登場元の画面外から演出する。Out → In の反転（まだ動き出していない Target を含む）では現在位置から戻る。
            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (toIn && ((_lastWasIn && st.progress >= 0.9999f) || st.progress <= 0.0001f))
                {
                    st.progress = 0f;
                    st.atOutSide = false;
                    st.pos = st.rest + st.inOffset;
                }
            }

            _lastWasIn = toIn;

            int movingCount = 0;
            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                st.from = st.progress;
                st.to = toProgress;
                st.fromPos = st.pos;
                st.toPos = toIn ? st.rest : st.rest + st.outOffset;
                st.moving = st.rt != null && Mathf.Abs(toProgress - st.from) > 0.0001f;
                if (st.moving)
                {
                    movingCount++;
                }
            }

            // 画面外位置の再計算結果を反映（現在の progress を保ったまま）
            ApplyAll();

            if (movingCount == 0)
            {
                return;
            }

            _activeSpec = phase.motion;
            float total = 0f;
            int rank = 0;
            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (!st.moving)
                {
                    continue;
                }

                float distance = Mathf.Abs(st.to - st.from);
                st.duration = Mathf.Max(0f, phase.motion.duration * (s.scaleDurationOnReverse ? Mathf.Min(1f, distance) : 1f));
                st.start = extraDelay
                    + phase.motion.delay
                    + StaggerUtility.GetDelay(rank, movingCount, phase.staggerOrder, phase.staggerInterval);
                total = Mathf.Max(total, st.start + st.duration);
                rank++;
            }

            if (total <= 0f)
            {
                FinishTransition();
                return;
            }

            _onCompleteDelegate ??= FinishTransition;

            // 全 Target を 1 本の線形タイマーで駆動する。Target ごとの開始時刻・Ease は Tick 内で評価するため、
            // 反転時に動き出していない Target は何も起きず、PlayInAsync は最後の Target の到着で完了する。
            var driver = HisuiMotionExtensions
                .CreateFloat(0f, total, new MotionSpec(total, Ease.Linear, 0f), IgnoreTimeScale)
                .WithOnComplete(_onCompleteDelegate)
                .Bind(this, s_tickCallback);
            TrackMotion(driver);
        }

        private void Tick(float time)
        {
            if (this == null)
            {
                return;
            }

            if (CanvasSizeChanged())
            {
                RecalculateOffsets();
                ApplyAll();
            }

            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (!st.moving)
                {
                    continue;
                }

                float local = time - st.start;
                if (local < 0f)
                {
                    continue; // まだ動き出していない
                }

                float e = st.duration <= 0f ? 1f : EvaluateEase(Mathf.Clamp01(local / st.duration));
                // 位置はクランプしない（OutBack 等のオーバーシュートを保つ）
                st.progress = st.from + (st.to - st.from) * e;
                st.pos = Vector2.LerpUnclamped(st.fromPos, st.toPos, e);
                ApplyTarget(ref st);
            }
        }

        private void FinishTransition()
        {
            if (this == null)
            {
                return;
            }

            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (!st.moving)
                {
                    continue;
                }

                st.progress = st.to;
                st.pos = st.toPos;
                st.atOutSide = st.to <= 0.5f;
                st.moving = false;
                ApplyTarget(ref st);
            }

            // In が完了したら、設定に応じて自動退場を予約する（PlayInAsync の完了には影響しない）。
            float autoOut = SettingsRef.autoOutDelay;
            if (_lastWasIn && autoOut >= 0f)
            {
                _autoOutArmed = true;
                _autoOutRemaining = autoOut;
            }
        }

        private float EvaluateEase(float t)
        {
            if (_activeSpec.ease == Ease.CustomAnimationCurve)
            {
                var curve = _activeSpec.customCurve;
                return curve != null && curve.length > 0 ? curve.Evaluate(t) : t;
            }

            return EaseUtility.Evaluate(t, _activeSpec.ease);
        }

        #region State

        private RectTransform ResolveTarget(int index)
        {
            return _targets.Count == 0 ? (RectTransform)transform : _targets[index];
        }

        private void EnsureStates()
        {
            int count = _targets.Count == 0 ? 1 : _targets.Count;

            bool match = _stateCount == count;
            for (int i = 0; match && i < count; i++)
            {
                if (_states[i].rt != ResolveTarget(i))
                {
                    match = false;
                }
            }

            if (match)
            {
                return;
            }

            bool first = _stateCount == 0;
            if (first)
            {
                // 初回のみ、レイアウト・メッシュ・Canvas Scaler を確定させてから到着位置を取得する。
                Canvas.ForceUpdateCanvases();
                _lastWasIn = !SettingsRef.startHidden;
            }

            var newStates = new TargetState[count];
            for (int i = 0; i < count; i++)
            {
                var rt = ResolveTarget(i);
                int found = -1;
                for (int k = 0; k < _stateCount; k++)
                {
                    if (rt != null && _states[k].rt == rt)
                    {
                        found = k;
                        break;
                    }
                }

                if (found >= 0)
                {
                    newStates[i] = _states[found];
                }
                else
                {
                    newStates[i] = CreateState(rt);
                }
            }

            _states = newStates;
            _stateCount = count;
            _hasCanvasSize = false;
        }

        private TargetState CreateState(RectTransform rt)
        {
            var st = new TargetState
            {
                rt = rt,
                progress = SettingsRef.startHidden ? 0f : 1f,
            };

            if (rt == null)
            {
                return st;
            }

            st.rest = rt.anchoredPosition;
            st.pos = st.rest;
            if (rt.TryGetComponent(out CanvasGroup cg))
            {
                st.canvasGroup = cg;
            }
            else if (rt.TryGetComponent(out Graphic g))
            {
                st.graphic = g;
            }

            float a = ReadAlpha(in st);
            // 既に透明な状態で取得された場合に、永久に見えなくなるのを避ける。
            st.baseAlpha = a > 0.001f ? a : 1f;
            return st;
        }

        private Vector2 DirectionFor(int index, bool forOut)
        {
            ref var s = ref SettingsRef;
            Vector2 d = forOut && s.separateOutDirection
                ? ScreenEdgeUtility.GetDirectionVector(s.outDirection, s.outCustomDirection)
                : ScreenEdgeUtility.GetDirectionVector(s.direction, s.customDirection);
            return s.alternateDirection && (index & 1) == 1 ? -d : d;
        }

        /// <summary>
        /// 全 Target の画面外位置（登場元・退場先）を現在の Canvas 状態から計算し直す。Target は一時的に到着位置へ置いて計測し、現在位置へ戻す。
        /// 完全に非表示 / 到着済みで停止中の Target の位置、および動作中 Target の始点・終点も新しい値へ更新する。
        /// </summary>
        private void RecalculateOffsets()
        {
            float margin = SettingsRef.extraMargin;
            bool separate = SettingsRef.separateOutDirection;
            for (int i = 0; i < _stateCount; i++)
            {
                ref var st = ref _states[i];
                if (st.rt == null)
                {
                    continue;
                }

                Vector2 current = st.rt.anchoredPosition;
                st.rt.anchoredPosition = st.rest;
                st.inOffset = ScreenEdgeUtility.CalculateOffscreenOffset(st.rt, DirectionFor(i, false), margin);
                st.outOffset = separate
                    ? ScreenEdgeUtility.CalculateOffscreenOffset(st.rt, DirectionFor(i, true), margin)
                    : st.inOffset;
                st.rt.anchoredPosition = current;

                Vector2 hiddenPos = st.rest + (st.atOutSide ? st.outOffset : st.inOffset);
                if (st.progress <= 0.0001f)
                {
                    st.pos = hiddenPos;
                }
                else if (st.progress >= 0.9999f)
                {
                    st.pos = st.rest;
                }

                if (st.moving)
                {
                    st.toPos = st.to >= 0.5f ? st.rest : st.rest + st.outOffset;
                    if (st.from <= 0.0001f)
                    {
                        st.fromPos = hiddenPos;
                    }
                    else if (st.from >= 0.9999f)
                    {
                        st.fromPos = st.rest;
                    }
                }
            }

            _hasCanvasSize = false;
            CanvasSizeChanged(); // 現在のサイズを記録
        }

        private bool CanvasSizeChanged()
        {
            RectTransform probe = _stateCount > 0 ? _states[0].rt : null;
            if (!ScreenEdgeUtility.TryGetRootCanvasRect(probe, out var rootRect))
            {
                return false;
            }

            Vector2 size = rootRect.rect.size;
            if (!_hasCanvasSize)
            {
                _hasCanvasSize = true;
                _lastCanvasSize = size;
                return false;
            }

            if ((size - _lastCanvasSize).sqrMagnitude > 0.0001f)
            {
                _lastCanvasSize = size;
                return true;
            }

            return false;
        }

        private bool AllHidden()
        {
            for (int i = 0; i < _stateCount; i++)
            {
                if (_states[i].rt != null && _states[i].progress > 0.0001f)
                {
                    return false;
                }
            }
            return true;
        }

        private void ApplyAll()
        {
            for (int i = 0; i < _stateCount; i++)
            {
                ApplyTarget(ref _states[i]);
            }
        }

        private void ApplyTarget(ref TargetState st)
        {
            if (st.rt == null)
            {
                return;
            }

            float p = st.progress;
            st.rt.anchoredPosition = st.pos;

            if (SettingsRef.useFade)
            {
                WriteAlpha(in st, st.baseAlpha * Mathf.Clamp01(p));
            }
        }

        private static float ReadAlpha(in TargetState st)
        {
            if (st.canvasGroup != null) return st.canvasGroup.alpha;
            if (st.graphic != null) return st.graphic.color.a;
            return 1f;
        }

        private static void WriteAlpha(in TargetState st, float alpha)
        {
            if (st.canvasGroup != null)
            {
                st.canvasGroup.alpha = alpha;
            }
            else if (st.graphic != null)
            {
                Color c = st.graphic.color;
                if (!Mathf.Approximately(c.a, alpha))
                {
                    c.a = alpha;
                    st.graphic.color = c;
                }
            }
        }

        #endregion

        #region Fluent Configuration API

        /// <summary>登場方向を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetDirection(EdgeDirection direction)
        {
            SettingsRef.direction = direction;
            return this;
        }

        /// <summary><see cref="EdgeDirection.Custom"/> の方向ベクトルを設定し、方向を Custom にする（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetDirection(Vector2 customDirection)
        {
            SettingsRef.direction = EdgeDirection.Custom;
            SettingsRef.customDirection = customDirection;
            return this;
        }

        /// <summary>退場方向を個別に指定する（例: 左から来て右へ退場）。メソッドチェーン対応。</summary>
        public EdgeSlideIn SetOutDirection(EdgeDirection direction)
        {
            SettingsRef.separateOutDirection = true;
            SettingsRef.outDirection = direction;
            return this;
        }

        /// <summary>退場方向をカスタムベクトルで個別指定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetOutDirection(Vector2 customDirection)
        {
            SettingsRef.separateOutDirection = true;
            SettingsRef.outDirection = EdgeDirection.Custom;
            SettingsRef.outCustomDirection = customDirection;
            return this;
        }

        /// <summary>退場方向を登場と同じ（来た方向へ戻る）に戻す（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetOutDirectionSameAsIn()
        {
            SettingsRef.separateOutDirection = false;
            return this;
        }

        /// <summary>奇数番目の Target の方向を反転するかを設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetAlternateDirection(bool alternate)
        {
            SettingsRef.alternateDirection = alternate;
            return this;
        }

        /// <summary>In / Out 両方の stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetStagger(float interval, StaggerOrder order = StaggerOrder.Forward)
        {
            SetInStagger(interval, order);
            SetOutStagger(interval, order);
            return this;
        }

        /// <summary>In の stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetInStagger(float interval, StaggerOrder order = StaggerOrder.Forward)
        {
            SettingsRef.inPhase.staggerInterval = Mathf.Max(0f, interval);
            SettingsRef.inPhase.staggerOrder = order;
            return this;
        }

        /// <summary>Out の stagger 間隔と順序を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetOutStagger(float interval, StaggerOrder order = StaggerOrder.Forward)
        {
            SettingsRef.outPhase.staggerInterval = Mathf.Max(0f, interval);
            SettingsRef.outPhase.staggerOrder = order;
            return this;
        }

        /// <summary>画面端から追加で離す余白を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetExtraMargin(float margin)
        {
            SettingsRef.extraMargin = Mathf.Max(0f, margin);
            return this;
        }

        /// <summary>In のモーションを設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetInMotion(in MotionSpec spec)
        {
            SettingsRef.inPhase.motion = spec;
            return this;
        }

        /// <summary>In の Duration / Ease / Delay を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetInMotion(float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            SettingsRef.inPhase.motion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>Out のモーションを設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetOutMotion(in MotionSpec spec)
        {
            SettingsRef.outPhase.motion = spec;
            return this;
        }

        /// <summary>Out の Duration / Ease / Delay を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetOutMotion(float duration, Ease ease = Ease.InCubic, float delay = 0f)
        {
            SettingsRef.outPhase.motion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>フェード併用の有無を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetFade(bool enabled)
        {
            SettingsRef.useFade = enabled;
            return this;
        }

        /// <summary>再生前に画面外で非表示にしておくかを設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetStartHidden(bool startHidden)
        {
            SettingsRef.startHidden = startHidden;
            return this;
        }

        /// <summary>OnEnable 自動再生時の開始遅延（秒）を設定する（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetStartDelay(float delay)
        {
            SettingsRef.startDelay = Mathf.Max(0f, delay);
            return this;
        }

        /// <summary>In 完了後の自動退場までの待機秒数を設定する。-1 で自動退場しない（メソッドチェーン対応）。</summary>
        public EdgeSlideIn SetAutoOut(float delay)
        {
            SettingsRef.autoOutDelay = delay < 0f ? -1f : delay;
            return this;
        }

        /// <summary>Target のリストを置き換える（メソッドチェーン対応）。次回の再生時に反映される。</summary>
        public EdgeSlideIn SetTargets(IEnumerable<RectTransform> targets)
        {
            _targets.Clear();
            if (targets != null)
            {
                _targets.AddRange(targets);
            }
            return this;
        }

        #endregion
    }
}
