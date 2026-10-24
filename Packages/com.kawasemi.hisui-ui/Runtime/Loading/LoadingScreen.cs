using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Core;
using LitMotion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hisui.UI.Components
{
    /// <summary>
    /// 画面全体を覆うローディング表示（フェード出入り + スピナー）。
    /// <para>
    /// <b>2つの使い方：</b><br/>
    /// ・シーン遷移：<see cref="LoadSceneAsync(string, LoadSceneMode, Func{CancellationToken, UniTask}, CancellationToken)"/><br/>
    /// ・同一シーン内：<see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/>
    /// </para>
    /// <para>
    /// <b>役割分担：</b>色・スプライトには一切触れない（背景やスピナーの見た目はプレファブ側の <c>Image</c> 等で設定する）。
    /// uPalette にも依存しない。担当するのはフェード・スピナーの動き・表示制御・シーン遷移の流れのみ。
    /// </para>
    /// <para>
    /// <b>基底 API の定義：</b><br/>
    /// ・<see cref="HisuiMotionBehaviour.Play"/> / <see cref="HisuiMotionBehaviour.PlayAsync"/> は <see cref="Show"/> / <see cref="ShowAsync"/> と同義。<br/>
    /// ・<see cref="Stop"/> は進行中のモーション（フェード・スピナー）の停止（スピナーは初期姿勢へ戻る。表示状態そのものは変えない）。<br/>
    /// ・自動再生トリガーは使わない（None 固定。Inspector からも隠す）。
    /// </para>
    /// <para>
    /// <b>強制操作と参照カウント：</b><br/>
    /// <see cref="ShowAsync"/> / <see cref="HideAsync(CancellationToken)"/> / <see cref="SetShownImmediate"/> / <see cref="LoadSceneAsync(string, LoadSceneMode, Func{CancellationToken, UniTask}, CancellationToken)"/> は
    /// <b>参照カウントの対象外</b>で、呼ばれた時点の意図を即座に反映する（<see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> が走っている最中でも強制的に表示／非表示になる）。
    /// 参照カウントは <see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> 同士の重なりにのみ使われる。
    /// </para>
    /// </summary>
    [AddComponentMenu("Hisui/UI/Loading Screen")]
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : HisuiMotionBehaviour<LoadingScreen, LoadingScreen.Settings>
    {
        /// <summary>スピナーの種類。</summary>
        public enum SpinnerType
        {
            /// <summary>スピナーなし。</summary>
            None = 0,

            /// <summary>連続回転（ステップ回転にも対応）。</summary>
            Rotate = 1,

            /// <summary>子要素を順番に拡縮させるループ（色には触れない）。</summary>
            Dots = 2,
        }

        /// <summary>非表示（フェードアウト完了後）の方法。</summary>
        public enum HideMode
        {
            /// <summary><c>CanvasGroup</c> の alpha / blocksRaycasts のみで隠す（GameObject は有効のまま）。</summary>
            CanvasGroupOnly = 0,

            /// <summary>さらに、LoadingScreen が付いている GameObject を <c>SetActive(false)</c> にする。</summary>
            DeactivateGameObject = 1,
        }

        /// <summary><see cref="LoadingScreen"/> の全設定。</summary>
        [Serializable]
        public struct Settings
        {
            /// <summary>Show（フェードイン）のモーション。</summary>
            [Header("Fade")]
            [Tooltip("Show（フェードイン）の Duration / Ease / Delay")]
            public MotionSpec showMotion;

            /// <summary>Hide（フェードアウト）のモーション。</summary>
            [Tooltip("Hide（フェードアウト）の Duration / Ease / Delay")]
            public MotionSpec hideMotion;

            /// <summary>非表示の方法。</summary>
            [Tooltip("フェードアウト完了後の非表示方法")]
            public HideMode hideMode;

            /// <summary>最低表示時間（秒）。フェードイン完了時点を起点とする。</summary>
            [Header("Timing")]
            [Tooltip("最低表示時間（秒）。フェードイン完了時点から計算し、経過前の Hide は待ってからフェードアウトする")]
            [Min(0f)]
            public float minDisplayTime;

            /// <summary>シーン有効化後、afterLoad の前に待つフレーム数。</summary>
            [Tooltip("LoadSceneAsync で新シーン有効化後に待つフレーム数（新シーンの Awake / Start を一巡させる）")]
            [Min(0)]
            public int waitFramesAfterLoad;

            /// <summary>シーンをまたいで残すか。</summary>
            [Header("Scene / Canvas")]
            [Tooltip("true の場合、Awake でルート Canvas を DontDestroyOnLoad にし、LoadingScreen.Instance として参照可能にする")]
            public bool persistAcrossScenes;

            /// <summary>sortingOrder を適用するか。</summary>
            [Tooltip("true の場合、ルート Canvas の sortingOrder を下の値で上書きする")]
            public bool applySortingOrder;

            /// <summary>ルート Canvas の sortingOrder。</summary>
            [Tooltip("ルート Canvas の sortingOrder")]
            public int sortingOrder;

            /// <summary>起動時に表示状態にするか。</summary>
            [Tooltip("起動時の初期状態（true: 表示 / false: 非表示）")]
            public bool initialVisible;

            /// <summary>スピナーの種類。</summary>
            [Header("Spinner")]
            [Tooltip("スピナーの種類")]
            public SpinnerType spinnerType;

            /// <summary>Rotate: 回転速度（deg/sec）。</summary>
            [Tooltip("Rotate: 回転速度（deg/sec）")]
            [Min(0f)]
            public float rotateSpeed;

            /// <summary>Rotate: 時計回りか。</summary>
            [Tooltip("Rotate: true で時計回り")]
            public bool rotateClockwise;

            /// <summary>Rotate: ステップ分割数（0 または 1 で連続回転、N≥2 で N 分割のカクカク回転）。</summary>
            [Tooltip("Rotate: ステップ回転の分割数（0/1 = 連続回転、N≥2 = N 分割でカクカク回す）")]
            [Min(0)]
            public int rotateStepCount;

            /// <summary>Dots: 拡縮させる子要素。</summary>
            [Tooltip("Dots: 順番に拡縮させる子要素のリスト")]
            public RectTransform[] dots;

            /// <summary>Dots: 1 周の時間（秒）。</summary>
            [Tooltip("Dots: 1 周の duration（秒）")]
            [Min(0.01f)]
            public float dotsCycleDuration;

            /// <summary>Dots: 要素ごとの位相のずらし幅（1 周を 1 とした割合。0 以下で 1/要素数）。</summary>
            [Tooltip("Dots: 要素ごとの位相ずらし幅（1周=1.0 の割合。0 以下なら 1/要素数 で自動）")]
            public float dotsPhaseStep;

            /// <summary>Dots: 最小スケール倍率。</summary>
            [Tooltip("Dots: 最小スケール倍率")]
            [Min(0f)]
            public float dotsMinScale;

            /// <summary>Dots: 最大スケール倍率。</summary>
            [Tooltip("Dots: 最大スケール倍率")]
            [Min(0f)]
            public float dotsMaxScale;

            /// <summary>既定設定。</summary>
            public static Settings Default => new Settings
            {
                showMotion = new MotionSpec(0.25f, Ease.OutCubic, 0f),
                hideMotion = new MotionSpec(0.25f, Ease.InCubic, 0f),
                hideMode = HideMode.CanvasGroupOnly,
                minDisplayTime = 0.5f,
                waitFramesAfterLoad = 2,
                persistAcrossScenes = true,
                applySortingOrder = true,
                sortingOrder = 1000,
                initialVisible = false,
                spinnerType = SpinnerType.Rotate,
                rotateSpeed = 360f,
                rotateClockwise = true,
                rotateStepCount = 0,
                dots = null,
                dotsCycleDuration = 1.2f,
                dotsPhaseStep = 0f,
                dotsMinScale = 0.4f,
                dotsMaxScale = 1f,
            };
        }

        [Header("Targets (All Optional)")]
        [Tooltip("フェード・blocksRaycasts 制御対象の CanvasGroup（未割り当てなら無視）")]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Tooltip("スピナーの RectTransform（Rotate 時の回転対象。未割り当てなら無視）")]
        [SerializeField]
        private RectTransform _spinner;

        // ---- static ----
        private static LoadingScreen s_instance;

        private static readonly Action<float, LoadingScreen> s_applyAlpha = static (v, self) => self.ApplyAlpha(v);
        private static readonly Action<float, LoadingScreen> s_applyRotation = static (v, self) => self.ApplyRotation(v);
        private static readonly Action<float, LoadingScreen> s_applyDots = static (v, self) => self.ApplyDots(v);

        // ---- state ----
        private bool _destroyed;
        private bool _isDuplicate;
        private bool _touched;
        private bool _warmedUp;

        private float _alpha;
        private bool _visible;
        private bool _targetShown;
        private float _shownAt = float.NegativeInfinity;
        private int _dirVersion;

        private bool _fadeActive;
        private UniTaskCompletionSource<bool> _fadeTcs;
        private Action _onFadeComplete;

        private int _runCount;
        private float _runMinTime;
        private bool _sceneLoading;

        private CancellationTokenSource _lifeCts;

        private MotionHandle _spinnerHandle;
        private bool _spinnerRunning;
        private bool _rotationCaptured;
        private Quaternion _spinnerBaseRotation = Quaternion.identity;
        private RectTransform[] _dotRefs;
        private Vector3[] _dotBaseScales;
        private int _dotCount;

        /// <summary>
        /// 永続化（<see cref="Settings.persistAcrossScenes"/>）が有効な LoadingScreen への便利参照。無ければ <c>null</c>。
        /// 破棄時に解除される。無くても、参照を直接保持すれば使える。
        /// </summary>
        public static LoadingScreen Instance => s_instance != null ? s_instance : null;

        /// <summary>
        /// 画面が表示されているか。フェードイン中・表示中・フェードアウト中は <c>true</c>、フェードアウト完了（非表示）で <c>false</c>。
        /// </summary>
        public bool IsVisible => _visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
        }

        private void Reset()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            SettingsRef = Settings.Default;
        }

        private void Awake()
        {
            AutoPlayTrigger = PlayTrigger.None;

            if (SettingsRef.persistAcrossScenes)
            {
                if (s_instance != null && s_instance != this)
                {
                    // 後から来たほうを破棄する
                    _isDuplicate = true;
                    var dup = GetRootObject();
                    dup.SetActive(false);
                    Destroy(dup);
                    return;
                }

                s_instance = this;
                DontDestroyOnLoad(GetRootObject());
            }

            ApplyCanvasSettings();

            if (SettingsRef.initialVisible)
            {
                // 起動直後から表示する場合は Awake 時点で即座に表示状態として確立する
                SetShownImmediate(true);
            }
        }

        private void Start()
        {
            if (_isDuplicate || _destroyed || _touched)
            {
                return;
            }

            // initialVisible が false の場合のみ、描画ウォームアップを行ってから非表示にする（設計書 3.1 対策）
            WarmupCanvasWhileVisible();
            SetShownImmediate(false);
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            _destroyed = true;

            if (s_instance == this)
            {
                s_instance = null;
            }

            // 実行中の待機（Show/Hide/RunAsync 等）を解放し、遅延待機をキャンセルする
            ReleaseFadeWaiters();
            if (_lifeCts != null)
            {
                _lifeCts.Cancel();
            }

            base.OnDestroy();
        }

        protected override void OnSettingsChanged()
        {
            ApplyCanvasSettings();
            RefreshSpinner();
        }

        // =====================================================================
        // Public API: Show / Hide
        // =====================================================================

        /// <summary>
        /// 表示を開始する（待たない）。<see cref="ShowAsync"/> を fire-and-forget で呼ぶのと同じ。
        /// </summary>
        public void Show()
        {
            ShowAsync().Forget();
        }

        /// <summary>
        /// 非表示を開始する（待たない）。最低表示時間が経過していなければ、待ってからフェードアウトする。
        /// </summary>
        public void Hide()
        {
            HideAsync().Forget();
        }

        /// <summary>
        /// 非表示を開始する（待たない）。<paramref name="skipMinTime"/> が true なら最低表示時間を待たない。
        /// </summary>
        public void Hide(bool skipMinTime)
        {
            HideAsync(skipMinTime).Forget();
        }

        /// <summary>
        /// GameObject 有効化 → フェードイン。フェード開始時点で <c>blocksRaycasts = true</c> にして入力を即座に塞ぐ。
        /// <para>
        /// Hide 途中に呼ばれた場合は現在の alpha から滑らかに反転する。すでに表示済み（フェードイン中を含む）なら何もせず完了を待つだけ。
        /// 強制操作であり、<see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> の参照カウントとは無関係。
        /// </para>
        /// <para>
        /// 完了・中断（Stop / 上書き / 破棄）・<paramref name="ct"/> のキャンセルのいずれでも例外は投げず静かに戻る。
        /// <paramref name="ct"/> のキャンセルは「待つのをやめる」だけで、開始済みのフェードは完了まで進む。
        /// </para>
        /// </summary>
        public async UniTask ShowAsync(CancellationToken ct = default)
        {
            if (_destroyed || ct.IsCancellationRequested)
            {
                return;
            }

            await WaitFadeAsync(ShowCore(), ct);
        }

        /// <summary>
        /// フェードアウト → 完了後に <c>blocksRaycasts = false</c> と非表示（<see cref="HideMode"/> に従う）。
        /// <para>
        /// フェードイン完了から <see cref="Settings.minDisplayTime"/> が経過していなければ、<b>待ってから</b>フェードアウトを始める。
        /// 待機中に逆方向（Show）が呼ばれた場合、この Hide は破棄される。待機中に <paramref name="ct"/> がキャンセルされた場合は
        /// Hide を取りやめ（表示状態は変えず）、例外を投げず静かに戻る。
        /// 強制操作であり、<see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> の参照カウントとは無関係。
        /// </para>
        /// </summary>
        public UniTask HideAsync(CancellationToken ct = default)
        {
            return HideCoreAsync(false, -1f, ct);
        }

        /// <summary>
        /// <see cref="HideAsync(CancellationToken)"/> のオーバーロード。<paramref name="skipMinTime"/> が true なら最低表示時間を待たずに閉じる。
        /// </summary>
        public UniTask HideAsync(bool skipMinTime, CancellationToken ct = default)
        {
            return HideCoreAsync(skipMinTime, -1f, ct);
        }

        /// <summary>
        /// アニメーションなしで即座に表示／非表示にする。進行中のモーションは停止する。
        /// </summary>
        public void SetShownImmediate(bool shown)
        {
            if (_destroyed || _isDuplicate)
            {
                return;
            }

            _dirVersion++;
            _touched = true;
            Stop();
            _fadeTcs = null;

            var cg = _canvasGroup;

            if (shown)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                if (!_warmedUp)
                {
                    WarmupCanvasWhileVisible();
                }

                _targetShown = true;
                _visible = true;
                ApplyAlpha(1f);
                if (cg != null)
                {
                    cg.blocksRaycasts = true;
                }

                _shownAt = Now();
                StartSpinner();
            }
            else
            {
                _targetShown = false;
                _visible = false;
                ApplyAlpha(0f);
                if (cg != null)
                {
                    cg.blocksRaycasts = false;
                }

                StopSpinner();
                if (SettingsRef.hideMode == HideMode.DeactivateGameObject && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        // =====================================================================
        // Base API overrides
        // =====================================================================

        /// <summary>
        /// 進行中のモーション（フェード・スピナー）を停止する。スピナーは初期姿勢へ戻る。表示状態（alpha 等）はそのまま。
        /// 待機中の <see cref="ShowAsync"/> 等は中断扱いで静かに戻る。
        /// </summary>
        public override void Stop()
        {
            base.Stop();
            ReleaseFadeWaiters();
            StopSpinner();
        }

        /// <summary><see cref="ShowAsync"/> と同義。</summary>
        public override UniTask PlayAsync(CancellationToken ct = default)
        {
            return ShowAsync(ct);
        }

        /// <summary><see cref="HisuiMotionBehaviour.Play"/> は <see cref="Show"/> と同義。</summary>
        protected override void OnPlayCore()
        {
            // 基底の Play() がフェードのハンドルをキャンセル済みなので、待機側の帳尻だけ合わせる
            ReleaseFadeWaiters();
            ShowCore();
        }

        // =====================================================================
        // Public API: RunAsync
        // =====================================================================

        /// <summary>
        /// 同一シーン内で任意の処理を、ロード画面で覆って実行する。
        /// <para>
        /// 流れ：Show（フェードイン完了を待つ）→ <paramref name="work"/> 実行 → 最低表示時間の待機 → Hide。
        /// <paramref name="work"/> は<b>フェードイン完了後</b>に開始される。
        /// </para>
        /// <para>
        /// 例外・キャンセルでも<b>必ず Hide</b> する（例外はそのまま呼び出し元へ伝わる。その場合は最低表示時間を待たずに閉じる）。
        /// フェードイン完了前に <paramref name="ct"/> がキャンセルされた場合は <paramref name="work"/> を実行せず、
        /// <see cref="OperationCanceledException"/> を投げる。
        /// </para>
        /// <para>
        /// <b>参照カウント：</b>複数の RunAsync が同時に走っている間は、最後の 1 つが終わるまで Hide しない。
        /// 2 つ目以降の Show は、すでに表示済みなら何もせず完了を待つだけ。最低表示時間は重なった呼び出しの中で最大のものを採用する。
        /// <see cref="ShowAsync"/> / <see cref="HideAsync(CancellationToken)"/> / <see cref="LoadSceneAsync(string, LoadSceneMode, Func{CancellationToken, UniTask}, CancellationToken)"/> はこのカウントに含まれない。
        /// </para>
        /// <para><see cref="LoadSceneAsync(string, LoadSceneMode, Func{CancellationToken, UniTask}, CancellationToken)"/> の実行中に呼ぶと <see cref="InvalidOperationException"/>。</para>
        /// </summary>
        /// <param name="work">ロード画面で覆っている間に行う処理。</param>
        /// <param name="ct">キャンセルトークン（work にも渡される）。</param>
        /// <param name="minDisplayTimeOverride">指定すると、この呼び出しに限り <see cref="Settings.minDisplayTime"/> を上書きする。</param>
        public async UniTask RunAsync(
            Func<CancellationToken, UniTask> work,
            CancellationToken ct = default,
            float? minDisplayTimeOverride = null)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            await RunCoreAsync<bool>(async token =>
            {
                await work(token);
                return true;
            }, ct, minDisplayTimeOverride);
        }

        /// <summary>
        /// <see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> の戻り値付き版。
        /// </summary>
        public UniTask<T> RunAsync<T>(
            Func<CancellationToken, UniTask<T>> work,
            CancellationToken ct = default,
            float? minDisplayTimeOverride = null)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            return RunCoreAsync(work, ct, minDisplayTimeOverride);
        }

        // =====================================================================
        // Public API: LoadSceneAsync
        // =====================================================================

        /// <summary>
        /// ロード画面を出したままシーンを切り替える。
        /// <para>
        /// 流れ：①Show（フェードイン完了待ち）→ ②<c>SceneManager.LoadSceneAsync</c> 開始（<c>allowSceneActivation = false</c>、0.9 到達まで待つ）
        /// → ③シーン有効化と読み込み完了待ち → ④<see cref="Settings.waitFramesAfterLoad"/> フレーム待機（新シーンの Awake / Start を一巡させる）
        /// → ⑤<paramref name="afterLoad"/> 実行 → ⑥最低表示時間の待機 → Hide。④⑤は Hide の前に行うので、新シーンの最初の描画は見えない。
        /// </para>
        /// <para>
        /// <b>キャンセル：</b>ロードの開始（②）より前（フェードイン待ち中）にキャンセルされたらロードを中止して Hide する。
        /// Unity の <c>AsyncOperation</c> は取り消せず、<c>allowSceneActivation = false</c> のまま放置すると以降のシーンロードが詰まるため、
        /// ②の開始後はキャンセルされても<b>そのままシーン切り替えを完了させて</b> Hide する（<paramref name="afterLoad"/> はスキップ）。
        /// 例外は投げず静かに戻る。
        /// </para>
        /// <para>
        /// 例外が出た場合も必ず Hide してから例外を伝える。LoadingScreen 自体がシーン遷移中に破棄されても例外・リークは出さない。
        /// <see cref="LoadSceneMode.Additive"/> に対応（アンロードは行わない）。
        /// この LoadingScreen が呼べるようにするには <see cref="Settings.persistAcrossScenes"/> が必要。
        /// </para>
        /// <para>実行中に <see cref="RunAsync(Func{CancellationToken, UniTask}, CancellationToken, float?)"/> や別の LoadSceneAsync が呼ばれた場合、
        /// または RunAsync の実行中に呼んだ場合は <see cref="InvalidOperationException"/>。</para>
        /// </summary>
        public UniTask LoadSceneAsync(
            string sceneName,
            LoadSceneMode mode = LoadSceneMode.Single,
            Func<CancellationToken, UniTask> afterLoad = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                throw new ArgumentException("sceneName is null or empty.", nameof(sceneName));
            }

            return LoadSceneCoreAsync(sceneName, -1, mode, afterLoad, ct);
        }

        /// <summary>
        /// <see cref="LoadSceneAsync(string, LoadSceneMode, Func{CancellationToken, UniTask}, CancellationToken)"/> のビルドインデックス指定版。
        /// </summary>
        public UniTask LoadSceneAsync(
            int buildIndex,
            LoadSceneMode mode = LoadSceneMode.Single,
            Func<CancellationToken, UniTask> afterLoad = null,
            CancellationToken ct = default)
        {
            return LoadSceneCoreAsync(null, buildIndex, mode, afterLoad, ct);
        }

        // =====================================================================
        // Core: Run / LoadScene
        // =====================================================================

        private async UniTask<T> RunCoreAsync<T>(
            Func<CancellationToken, UniTask<T>> work,
            CancellationToken ct,
            float? minDisplayTimeOverride)
        {
            if (_sceneLoading)
            {
                throw new InvalidOperationException(
                    "LoadingScreen: RunAsync cannot be called while LoadSceneAsync is running.");
            }

            if (_destroyed)
            {
                // 覆えないが、処理自体は実行する
                return await work(ct);
            }

            if (_runCount == 0)
            {
                _runMinTime = 0f;
            }

            _runCount++;
            float min = minDisplayTimeOverride ?? SettingsRef.minDisplayTime;
            if (min > _runMinTime)
            {
                _runMinTime = min;
            }

            bool clean = false;
            try
            {
                await WaitFadeAsync(ShowCore(), ct);
                ct.ThrowIfCancellationRequested();

                T result = await work(ct);
                clean = true;
                return result;
            }
            finally
            {
                _runCount--;
                if (_runCount == 0 && !_destroyed)
                {
                    float m = _runMinTime;
                    _runMinTime = 0f;
                    await HideCoreAsync(!clean || ct.IsCancellationRequested, m, CancellationToken.None);
                }
            }
        }

        private async UniTask LoadSceneCoreAsync(
            string sceneName,
            int buildIndex,
            LoadSceneMode mode,
            Func<CancellationToken, UniTask> afterLoad,
            CancellationToken ct)
        {
            if (_sceneLoading)
            {
                throw new InvalidOperationException(
                    "LoadingScreen: LoadSceneAsync is already running.");
            }

            if (_runCount > 0)
            {
                throw new InvalidOperationException(
                    "LoadingScreen: LoadSceneAsync cannot be called while RunAsync is running.");
            }

            if (_destroyed)
            {
                throw new InvalidOperationException("LoadingScreen: already destroyed.");
            }

            _sceneLoading = true;
            bool clean = false;
            try
            {
                // 1. Show（フェードイン完了待ち）
                await WaitFadeAsync(ShowCore(), ct);
                if (ct.IsCancellationRequested)
                {
                    // ロード開始前のキャンセル：中止して Hide（finally）
                    return;
                }

                // 2. 読み込み開始（0.9 まで）
                AsyncOperation op = sceneName != null
                    ? SceneManager.LoadSceneAsync(sceneName, mode)
                    : SceneManager.LoadSceneAsync(buildIndex, mode);
                if (op == null)
                {
                    throw new InvalidOperationException(
                        $"LoadingScreen: failed to start loading scene '{(sceneName ?? buildIndex.ToString())}'.");
                }

                op.allowSceneActivation = false;
                while (op.progress < 0.9f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // 3. 有効化して完了待ち
                op.allowSceneActivation = true;
                while (!op.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // 4. 新シーンの Awake / Start を一巡させる
                int frames = Mathf.Max(0, SettingsRef.waitFramesAfterLoad);
                for (int i = 0; i < frames; i++)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // 5. afterLoad（有効化後のキャンセルではスキップ）
                if (afterLoad != null && !ct.IsCancellationRequested)
                {
                    try
                    {
                        await afterLoad(ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                    }
                }

                clean = !ct.IsCancellationRequested;
            }
            finally
            {
                _sceneLoading = false;

                // 6. 最低表示時間の待機 → Hide（破棄済みなら何もしない）
                if (!_destroyed)
                {
                    await HideCoreAsync(!clean, -1f, CancellationToken.None);
                }
            }
        }

        // =====================================================================
        // Core: Show / Hide / Fade
        // =====================================================================

        private UniTask<bool> ShowCore()
        {
            _dirVersion++; // 待機中の Hide を破棄する

            if (_destroyed || _isDuplicate)
            {
                return UniTask.FromResult(false);
            }

            if (_targetShown && _visible && (_fadeActive || _alpha >= 1f))
            {
                return _fadeTcs != null ? _fadeTcs.Task : UniTask.FromResult(true);
            }

            return BeginFade(true);
        }

        private async UniTask HideCoreAsync(bool skipMinTime, float minTime, CancellationToken ct)
        {
            if (_destroyed || ct.IsCancellationRequested)
            {
                return;
            }

            int ver = ++_dirVersion;
            if (!_visible)
            {
                return;
            }

            // すでにフェードアウト中なら、その完了を待つだけ
            if (!_targetShown && _fadeActive)
            {
                await WaitFadeAsync(_fadeTcs.Task, ct);
                return;
            }

            if (_targetShown && !skipMinTime)
            {
                float min = minTime >= 0f ? minTime : SettingsRef.minDisplayTime;
                if (min > 0f)
                {
                    // フェードイン中なら、完了（= 最低表示時間の起点）を待つ
                    if (_fadeActive)
                    {
                        if (!await WaitFadeAsync(_fadeTcs.Task, ct))
                        {
                            return;
                        }

                        if (_destroyed || ver != _dirVersion)
                        {
                            return;
                        }
                    }

                    float remaining = _shownAt + min - Now();
                    if (remaining > 0f)
                    {
                        if (!await DelaySecondsAsync(remaining, ct))
                        {
                            return;
                        }

                        if (_destroyed || ver != _dirVersion)
                        {
                            return;
                        }
                    }
                }
            }

            if (_destroyed)
            {
                return;
            }

            await WaitFadeAsync(BeginFade(false), ct);
        }

        private UniTask<bool> BeginFade(bool show)
        {
            // 進行中のフェードを中断（スピナーは止めない）
            CancelTrackedMotionsAndIncrementVersion();
            ReleaseFadeWaiters();
            _touched = true;

            var cg = _canvasGroup;

            if (show)
            {
                bool wasInactive = !gameObject.activeSelf;
                if (wasInactive)
                {
                    gameObject.SetActive(true);
                    if (_destroyed || _isDuplicate)
                    {
                        return UniTask.FromResult(false);
                    }
                }

                if (wasInactive || !_warmedUp)
                {
                    WarmupCanvasWhileVisible();
                    ApplyAlpha(_alpha);
                }

                _visible = true;
                _shownAt = float.NegativeInfinity;
                if (cg != null)
                {
                    cg.blocksRaycasts = true; // フェード開始時点で入力を塞ぐ
                }

                StartSpinner();
            }

            _targetShown = show;

            var tcs = new UniTaskCompletionSource<bool>();
            _fadeTcs = tcs;
            _fadeActive = true;

            float target = show ? 1f : 0f;
            MotionSpec spec = show ? SettingsRef.showMotion : SettingsRef.hideMotion;
            float distance = Mathf.Abs(target - _alpha);

            if (distance <= 0.0001f || (spec.duration <= 0f && spec.delay <= 0f))
            {
                OnFadeComplete();
                return tcs.Task;
            }

            // 途中反転時は、残りの距離に応じて時間を縮める
            spec.duration = Mathf.Max(0.0001f, spec.duration * distance);

            _onFadeComplete ??= OnFadeComplete;
            var handle = HisuiMotionExtensions.CreateFloat(_alpha, target, in spec, IgnoreTimeScale)
                .WithOnComplete(_onFadeComplete)
                .Bind(this, s_applyAlpha);
            TrackMotion(handle);

            return tcs.Task;
        }

        private void OnFadeComplete()
        {
            if (_destroyed)
            {
                return;
            }

            var tcs = _fadeTcs;
            bool show = _targetShown;
            _fadeActive = false;

            ApplyAlpha(show ? 1f : 0f);

            if (show)
            {
                _shownAt = Now();
            }
            else
            {
                FinishHide();
            }

            tcs?.TrySetResult(true);
        }

        private void FinishHide()
        {
            var cg = _canvasGroup;
            if (cg != null)
            {
                cg.blocksRaycasts = false;
            }

            _visible = false;
            StopSpinner();

            if (SettingsRef.hideMode == HideMode.DeactivateGameObject && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>進行中のフェードの待機側を「中断」として解放する（モーション自体のキャンセルは呼び出し側）。</summary>
        private void ReleaseFadeWaiters()
        {
            if (_fadeActive)
            {
                _fadeActive = false;
                _fadeTcs?.TrySetResult(false);
            }
        }

        /// <summary>フェード完了を待つ。完了なら true、中断・キャンセル・破棄なら false（例外は投げない）。</summary>
        private static async UniTask<bool> WaitFadeAsync(UniTask<bool> task, CancellationToken ct)
        {
            var (canceled, completed) = await task.AttachExternalCancellation(ct).SuppressCancellationThrow();
            return !canceled && completed;
        }

        private async UniTask<bool> DelaySecondsAsync(float seconds, CancellationToken ct)
        {
            if (_destroyed)
            {
                return false;
            }

            _lifeCts ??= new CancellationTokenSource();
            CancellationTokenSource linked = null;
            var token = _lifeCts.Token;
            if (ct.CanBeCanceled)
            {
                linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
                token = linked.Token;
            }

            try
            {
                bool canceled = await UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    IgnoreTimeScale ? Cysharp.Threading.Tasks.DelayType.UnscaledDeltaTime : Cysharp.Threading.Tasks.DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    token).SuppressCancellationThrow();
                return !canceled;
            }
            finally
            {
                linked?.Dispose();
            }
        }

        private float Now()
        {
            return IgnoreTimeScale ? Time.unscaledTime : Time.time;
        }

        private void ApplyAlpha(float value)
        {
            _alpha = Mathf.Clamp01(value);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _alpha;
            }
        }

        private void WarmupCanvasWhileVisible()
        {
            _warmedUp = true;
            var cg = _canvasGroup;
            if (cg != null)
            {
                float prev = cg.alpha;
                cg.alpha = 1f;
                Canvas.ForceUpdateCanvases();
                cg.alpha = prev;
            }
            else
            {
                Canvas.ForceUpdateCanvases();
            }
        }

        // =====================================================================
        // Canvas / Root
        // =====================================================================

        private GameObject GetRootObject()
        {
            var canvas = GetComponentInParent<Canvas>(true);
            var t = canvas != null ? canvas.rootCanvas.transform : transform;
            return t.root.gameObject;
        }

        private void ApplyCanvasSettings()
        {
            if (!SettingsRef.applySortingOrder || _isDuplicate)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                canvas.rootCanvas.sortingOrder = SettingsRef.sortingOrder;
            }
        }

        // =====================================================================
        // Spinner
        // =====================================================================

        private void StartSpinner()
        {
            if (_spinnerRunning)
            {
                return;
            }

            ref var s = ref SettingsRef;
            switch (s.spinnerType)
            {
                case SpinnerType.Rotate:
                    if (_spinner == null || s.rotateSpeed <= 0f)
                    {
                        return;
                    }

                    _spinnerBaseRotation = _spinner.localRotation;
                    _rotationCaptured = true;
                    _spinnerHandle = LMotion.Create(0f, 360f, 360f / s.rotateSpeed)
                        .WithEase(Ease.Linear)
                        .WithLoops(-1, LoopType.Restart)
                        .WithScheduler(CurrentScheduler)
                        .Bind(this, s_applyRotation);
                    _spinnerRunning = true;
                    break;

                case SpinnerType.Dots:
                    if (!CaptureDots())
                    {
                        return;
                    }

                    _spinnerHandle = LMotion.Create(0f, 1f, Mathf.Max(0.01f, s.dotsCycleDuration))
                        .WithEase(Ease.Linear)
                        .WithLoops(-1, LoopType.Restart)
                        .WithScheduler(CurrentScheduler)
                        .Bind(this, s_applyDots);
                    _spinnerRunning = true;
                    break;
            }
        }

        /// <summary>スピナーのモーションを止め、回転・スケールを初期状態へ戻す。</summary>
        private void StopSpinner()
        {
            _spinnerHandle.TryCancelAndClear();
            _spinnerRunning = false;

            if (_rotationCaptured)
            {
                _rotationCaptured = false;
                if (_spinner != null)
                {
                    _spinner.localRotation = _spinnerBaseRotation;
                }
            }

            if (_dotCount > 0)
            {
                for (int i = 0; i < _dotCount; i++)
                {
                    if (_dotRefs[i] != null)
                    {
                        _dotRefs[i].localScale = _dotBaseScales[i];
                    }
                }

                _dotCount = 0;
            }
        }

        private void RefreshSpinner()
        {
            if (!_spinnerRunning)
            {
                return;
            }

            StopSpinner();
            if (_visible)
            {
                StartSpinner();
            }
        }

        private bool CaptureDots()
        {
            var dots = SettingsRef.dots;
            if (dots == null || dots.Length == 0)
            {
                return false;
            }

            if (_dotRefs == null || _dotRefs.Length != dots.Length)
            {
                _dotRefs = new RectTransform[dots.Length];
                _dotBaseScales = new Vector3[dots.Length];
            }

            for (int i = 0; i < dots.Length; i++)
            {
                _dotRefs[i] = dots[i];
                _dotBaseScales[i] = dots[i] != null ? dots[i].localScale : Vector3.one;
            }

            _dotCount = dots.Length;
            return true;
        }

        private void ApplyRotation(float angle)
        {
            if (_spinner == null)
            {
                return;
            }

            ref var s = ref SettingsRef;
            if (s.rotateStepCount >= 2)
            {
                float stepAngle = 360f / s.rotateStepCount;
                angle = Mathf.Floor(angle / stepAngle) * stepAngle;
            }

            _spinner.localRotation = _spinnerBaseRotation * Quaternion.Euler(0f, 0f, s.rotateClockwise ? -angle : angle);
        }

        private void ApplyDots(float t)
        {
            if (_dotCount <= 0)
            {
                return;
            }

            ref var s = ref SettingsRef;
            float step = s.dotsPhaseStep > 0f ? s.dotsPhaseStep : 1f / _dotCount;
            for (int i = 0; i < _dotCount; i++)
            {
                var rt = _dotRefs[i];
                if (rt == null)
                {
                    continue;
                }

                float p = t - i * step;
                p -= Mathf.Floor(p);
                float w = p < 0.5f ? p * 2f : (1f - p) * 2f;
                w = w * w * (3f - 2f * w);
                rt.localScale = _dotBaseScales[i] * Mathf.Lerp(s.dotsMinScale, s.dotsMaxScale, w);
            }
        }

        // =====================================================================
        // Fluent Configuration API
        // =====================================================================

        /// <summary>最低表示時間（秒）を設定する。</summary>
        public LoadingScreen SetMinDisplayTime(float seconds)
        {
            SettingsRef.minDisplayTime = Mathf.Max(0f, seconds);
            return this;
        }

        /// <summary>Show のモーションを設定する。</summary>
        public LoadingScreen SetShowMotion(in MotionSpec spec)
        {
            SettingsRef.showMotion = spec;
            return this;
        }

        /// <summary>Show の Duration / Ease / Delay を設定する。</summary>
        public LoadingScreen SetShowMotion(float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            SettingsRef.showMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>Hide のモーションを設定する。</summary>
        public LoadingScreen SetHideMotion(in MotionSpec spec)
        {
            SettingsRef.hideMotion = spec;
            return this;
        }

        /// <summary>Hide の Duration / Ease / Delay を設定する。</summary>
        public LoadingScreen SetHideMotion(float duration, Ease ease = Ease.InCubic, float delay = 0f)
        {
            SettingsRef.hideMotion = new MotionSpec(duration, ease, delay);
            return this;
        }

        /// <summary>非表示方法を設定する。</summary>
        public LoadingScreen SetHideMode(HideMode mode)
        {
            SettingsRef.hideMode = mode;
            return this;
        }

        /// <summary>シーン有効化後に待つフレーム数を設定する。</summary>
        public LoadingScreen SetWaitFramesAfterLoad(int frames)
        {
            SettingsRef.waitFramesAfterLoad = Mathf.Max(0, frames);
            return this;
        }

        /// <summary>ルート Canvas の sortingOrder を設定し、即座に適用する。</summary>
        public LoadingScreen SetSortingOrder(int order)
        {
            SettingsRef.applySortingOrder = true;
            SettingsRef.sortingOrder = order;
            ApplyCanvasSettings();
            return this;
        }

        /// <summary>起動時の初期状態を設定する（Start 前に設定した場合のみ有効）。</summary>
        public LoadingScreen SetInitialVisible(bool visible)
        {
            SettingsRef.initialVisible = visible;
            return this;
        }

        /// <summary>シーンをまたいで残すかを設定する（Awake 前に設定した場合のみ有効）。</summary>
        public LoadingScreen SetPersistAcrossScenes(bool persist)
        {
            SettingsRef.persistAcrossScenes = persist;
            return this;
        }

        /// <summary>スピナーの種類を設定する。</summary>
        public LoadingScreen SetSpinnerType(SpinnerType type)
        {
            SettingsRef.spinnerType = type;
            RefreshSpinner();
            return this;
        }

        /// <summary>Rotate スピナーの速度・方向・ステップ分割数を設定する。</summary>
        public LoadingScreen SetRotate(float degreesPerSecond, bool clockwise = true, int stepCount = 0)
        {
            ref var s = ref SettingsRef;
            s.rotateSpeed = Mathf.Max(0f, degreesPerSecond);
            s.rotateClockwise = clockwise;
            s.rotateStepCount = Mathf.Max(0, stepCount);
            RefreshSpinner();
            return this;
        }

        /// <summary>Dots スピナーの設定（子要素・1周の時間・位相ずらし幅・スケール範囲）を行う。</summary>
        public LoadingScreen SetDots(
            RectTransform[] dots,
            float cycleDuration = 1.2f,
            float phaseStep = 0f,
            float minScale = 0.4f,
            float maxScale = 1f)
        {
            ref var s = ref SettingsRef;
            s.dots = dots;
            s.dotsCycleDuration = Mathf.Max(0.01f, cycleDuration);
            s.dotsPhaseStep = phaseStep;
            s.dotsMinScale = Mathf.Max(0f, minScale);
            s.dotsMaxScale = Mathf.Max(0f, maxScale);
            RefreshSpinner();
            return this;
        }

        /// <summary>スピナーの RectTransform を設定する。</summary>
        public LoadingScreen SetSpinnerTarget(RectTransform spinner)
        {
            bool wasRunning = _spinnerRunning;
            StopSpinner();
            _spinner = spinner;
            if (wasRunning && _visible)
            {
                StartSpinner();
            }

            return this;
        }

        /// <summary>フェード対象の CanvasGroup を設定する。</summary>
        public LoadingScreen SetCanvasGroup(CanvasGroup canvasGroup)
        {
            _canvasGroup = canvasGroup;
            return this;
        }
    }
}
