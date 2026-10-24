using System.Threading;
using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HisuiUIDemo : MonoBehaviour
{
    [Header("Components to Test")]
    [SerializeField] private DelayGauge _gauge;
    [SerializeField] private PanelTransition _panel;
    [SerializeField] private EdgeSlideIn _edgeSlideIn;
    [SerializeField] private Popup _popup;

    [Header("LoadingScreen Demo")]
    [Tooltip("LoadingScreen コンポーネント（未指定時は LoadingScreen.Instance を自動使用）")]
    [SerializeField] private LoadingScreen _loadingScreen;
    [Tooltip("ローディング画面で覆って表示/非表示を切り替える全画面パネル")]
    [SerializeField] private GameObject _fullScreenPanel;

    [Header("Gauge Settings")]
    [SerializeField] private float _gaugeStep = 0.2f;

    [Header("uGUI Buttons (Optional / Auto-Binding)")]
    [Tooltip("Gaugeを減らすボタン")]
    [SerializeField] private Button _decreaseGaugeButton;
    [Tooltip("Gaugeを増やすボタン")]
    [SerializeField] private Button _increaseGaugeButton;
    [Tooltip("Panelを開く/閉じるボタン（トグル）")]
    [SerializeField] private Button _togglePanelButton;
    [Tooltip("Panelの中に置く、Panelを閉じるボタン")]
    [SerializeField] private Button _closePanelButton;
    [Tooltip("EdgeSlideInを再生するボタン")]
    [SerializeField] private Button _playEdgeSlideInButton;
    [Tooltip("Popup単体を再生するボタン")]
    [SerializeField] private Button _playPopupButton;

    [Header("LoadingScreen Buttons")]
    [Tooltip("サブシーン (BuildIndex=1) へ遷移するボタン")]
    [SerializeField] private Button _loadSubSceneButton;
    [Tooltip("ローディング画面を出して全画面パネルを表示するボタン")]
    [SerializeField] private Button _showFullScreenPanelButton;
    [Tooltip("ローディング画面を出して全画面パネルを閉じるボタン")]
    [SerializeField] private Button _hideFullScreenPanelButton;

    private float _currentHp = 1.0f;
    private bool _isPaused = false;

    private LoadingScreen TargetLoadingScreen => _loadingScreen != null ? _loadingScreen : LoadingScreen.Instance;

    private void Awake()
    {
        RegisterButtonListeners();
    }

    private void Start()
    {
        if (_gauge != null)
        {
            _currentHp = _gauge.Value;
            _gauge.OnValueChanged += OnGaugeValueChanged;
        }

        // InitialVisible = true などで起動時からローディング画面が出ている場合、
        // 起動初期化（擬似ロード）を行ってから自動でフェードアウトする
        HandleStartupLoadingAsync().Forget();
    }

    private async UniTaskVoid HandleStartupLoadingAsync()
    {
        var loading = TargetLoadingScreen;
        if (loading != null && loading.IsVisible)
        {
            Debug.Log("[HisuiUI Demo] InitialVisible is true. Simulating startup loading...");
            var ct = this.GetCancellationTokenOnDestroy();

            // 起動時の初期化シミュレーション（最低表示時間は LoadingScreen 側が自動で保証）
            await UniTask.Delay(500, cancellationToken: ct).SuppressCancellationThrow();

            if (loading != null)
            {
                Debug.Log("[HisuiUI Demo] Startup loading completed. Hiding LoadingScreen...");
                await loading.HideAsync(ct);
            }
        }
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();

        if (_gauge != null)
        {
            _gauge.OnValueChanged -= OnGaugeValueChanged;
        }
    }

    private void RegisterButtonListeners()
    {
        if (_decreaseGaugeButton != null) _decreaseGaugeButton.onClick.AddListener(DecreaseGauge);
        if (_increaseGaugeButton != null) _increaseGaugeButton.onClick.AddListener(IncreaseGauge);
        if (_togglePanelButton != null) _togglePanelButton.onClick.AddListener(TogglePanel);
        if (_closePanelButton != null) _closePanelButton.onClick.AddListener(ClosePanel);
        if (_playEdgeSlideInButton != null) _playEdgeSlideInButton.onClick.AddListener(PlayEdgeSlideIn);
        if (_playPopupButton != null) _playPopupButton.onClick.AddListener(PlayPopup);

        if (_loadSubSceneButton != null) _loadSubSceneButton.onClick.AddListener(LoadSubScene);
        if (_showFullScreenPanelButton != null) _showFullScreenPanelButton.onClick.AddListener(ShowFullScreenPanelWithLoading);
        if (_hideFullScreenPanelButton != null) _hideFullScreenPanelButton.onClick.AddListener(HideFullScreenPanelWithLoading);
    }

    private void UnregisterButtonListeners()
    {
        if (_decreaseGaugeButton != null) _decreaseGaugeButton.onClick.RemoveListener(DecreaseGauge);
        if (_increaseGaugeButton != null) _increaseGaugeButton.onClick.RemoveListener(IncreaseGauge);
        if (_togglePanelButton != null) _togglePanelButton.onClick.RemoveListener(TogglePanel);
        if (_closePanelButton != null) _closePanelButton.onClick.RemoveListener(ClosePanel);
        if (_playEdgeSlideInButton != null) _playEdgeSlideInButton.onClick.RemoveListener(PlayEdgeSlideIn);
        if (_playPopupButton != null) _playPopupButton.onClick.RemoveListener(PlayPopup);

        if (_loadSubSceneButton != null) _loadSubSceneButton.onClick.RemoveListener(LoadSubScene);
        if (_showFullScreenPanelButton != null) _showFullScreenPanelButton.onClick.RemoveListener(ShowFullScreenPanelWithLoading);
        if (_hideFullScreenPanelButton != null) _hideFullScreenPanelButton.onClick.RemoveListener(HideFullScreenPanelWithLoading);
    }

    private void OnGaugeValueChanged(float newValue)
    {
        _currentHp = newValue;
    }

    private void Update()
    {
        // キーボード操作の検出（新旧 Input System 両対応）
        if (WasKeyPressed(KeyCode.Space, Key.Space))
        {
            TogglePanel();
        }
        else if (WasKeyPressed(KeyCode.D, Key.D))
        {
            DecreaseGauge();
        }
        else if (WasKeyPressed(KeyCode.H, Key.H))
        {
            IncreaseGauge();
        }
        else if (WasKeyPressed(KeyCode.R, Key.R))
        {
            ResetGauge();
        }
        else if (WasKeyPressed(KeyCode.E, Key.E))
        {
            PlayEdgeSlideIn();
        }
        else if (WasKeyPressed(KeyCode.U, Key.U))
        {
            PlayPopup();
        }
        else if (WasKeyPressed(KeyCode.L, Key.L))
        {
            LoadSubScene();
        }
        else if (WasKeyPressed(KeyCode.O, Key.O))
        {
            ShowFullScreenPanelWithLoading();
        }
        else if (WasKeyPressed(KeyCode.C, Key.C))
        {
            HideFullScreenPanelWithLoading();
        }
        else if (WasKeyPressed(KeyCode.P, Key.P) && !IsModifierHeld())
        {
            TogglePause();
        }
    }

    // --- LoadingScreen 操作 ---

    /// <summary>
    /// サブシーン（BuildIndex = 1）にローディング画面を伴って遷移する。
    /// </summary>
    public void LoadSubScene()
    {
        LoadSubSceneAsync().Forget();
    }

    /// <summary>
    /// サブシーン（BuildIndex = 1）への遷移を非同期で実行する。
    /// </summary>
    public async UniTask LoadSubSceneAsync(CancellationToken ct = default)
    {
        var loading = TargetLoadingScreen;
        Debug.Log("[HisuiUI Demo] Load SubScene (BuildIndex = 1)...");

        if (loading != null)
        {
            await loading.LoadSceneAsync(1, LoadSceneMode.Single, null, ct);
        }
        else
        {
            Debug.LogWarning("[HisuiUI Demo] LoadingScreen not found. Loading scene directly.");
            await SceneManager.LoadSceneAsync(1, LoadSceneMode.Single).WithCancellation(ct);
        }
    }

    /// <summary>
    /// ローディング画面を出して全画面パネルを表示（SetActive(true)）する。
    /// </summary>
    public void ShowFullScreenPanelWithLoading()
    {
        ShowFullScreenPanelWithLoadingAsync().Forget();
    }

    /// <summary>
    /// ローディング画面を出して全画面パネルを表示（SetActive(true)）する非同期処理。
    /// フェードイン完了後に SetActive(true) され、画面が覆われた状態でパネルが表示されます。
    /// </summary>
    public async UniTask ShowFullScreenPanelWithLoadingAsync(CancellationToken ct = default)
    {
        if (_fullScreenPanel == null)
        {
            Debug.LogWarning("[HisuiUI Demo] _fullScreenPanel is not assigned.");
            return;
        }

        var loading = TargetLoadingScreen;
        if (loading != null)
        {
            Debug.Log("[HisuiUI Demo] Showing FullScreenPanel with LoadingScreen...");
            await loading.RunAsync(async token =>
            {
                _fullScreenPanel.SetActive(true);
                // 1フレーム待ってレイアウト等の初期化を完了させる
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }, ct);
        }
        else
        {
            _fullScreenPanel.SetActive(true);
        }
    }

    /// <summary>
    /// ローディング画面を出して全画面パネルを非表示（SetActive(false)）にする。
    /// </summary>
    public void HideFullScreenPanelWithLoading()
    {
        HideFullScreenPanelWithLoadingAsync().Forget();
    }

    /// <summary>
    /// ローディング画面を出して全画面パネルを非表示（SetActive(false)）にする非同期処理。
    /// フェードイン完了後に SetActive(false) され、画面が覆われた状態でパネルが閉じられます。
    /// </summary>
    public async UniTask HideFullScreenPanelWithLoadingAsync(CancellationToken ct = default)
    {
        if (_fullScreenPanel == null)
        {
            Debug.LogWarning("[HisuiUI Demo] _fullScreenPanel is not assigned.");
            return;
        }

        var loading = TargetLoadingScreen;
        if (loading != null)
        {
            Debug.Log("[HisuiUI Demo] Hiding FullScreenPanel with LoadingScreen...");
            await loading.RunAsync(async token =>
            {
                _fullScreenPanel.SetActive(false);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }, ct);
        }
        else
        {
            _fullScreenPanel.SetActive(false);
        }
    }

    // --- Panel 操作 ---

    /// <summary>
    /// Panel を開閉トグルする（開いていたら閉じる、閉じていたら開く）
    /// </summary>
    public void TogglePanel()
    {
        if (_panel == null) return;
        Debug.Log($"[HisuiUI Demo] Toggle Panel (Currently Open: {_panel.IsOpen})");
        _panel.Toggle();
    }

    /// <summary>
    /// Panel を開く
    /// </summary>
    public void OpenPanel()
    {
        if (_panel == null) return;
        Debug.Log("[HisuiUI Demo] Open Panel");
        _panel.Open();
    }

    /// <summary>
    /// Panel を閉じる（Panel 内の閉じるボタン用）
    /// </summary>
    public void ClosePanel()
    {
        if (_panel == null) return;
        Debug.Log("[HisuiUI Demo] Close Panel");
        _panel.Close();
    }

    // --- Gauge 操作 ---

    /// <summary>
    /// Gauge を減らす
    /// </summary>
    public void DecreaseGauge()
    {
        TakeDamage(_gaugeStep);
    }

    /// <summary>
    /// Gauge を増やす
    /// </summary>
    public void IncreaseGauge()
    {
        Heal(_gaugeStep);
    }

    public void TakeDamage(float amount)
    {
        if (_gauge == null) return;
        _currentHp = Mathf.Max(0f, _currentHp - amount);
        Debug.Log($"[HisuiUI Demo] Decrease Gauge! New Value: {_currentHp:F2}");
        _gauge.SetValue(_currentHp);
    }

    public void Heal(float amount)
    {
        if (_gauge == null) return;
        _currentHp = Mathf.Min(1f, _currentHp + amount);
        Debug.Log($"[HisuiUI Demo] Increase Gauge! New Value: {_currentHp:F2}");
        _gauge.SetValue(_currentHp);
    }

    public void ResetGauge()
    {
        if (_gauge == null) return;
        _currentHp = 1.0f;
        Debug.Log("[HisuiUI Demo] Gauge Reset (Immediate)");
        _gauge.SetValue(1.0f, immediate: true);
    }

    // --- EdgeSlideIn 操作 ---

    /// <summary>
    /// EdgeSlideIn の登場アニメーションを再生する
    /// </summary>
    public void PlayEdgeSlideIn()
    {
        if (_edgeSlideIn == null) return;
        Debug.Log("[HisuiUI Demo] Play EdgeSlideIn");
        _edgeSlideIn.PlayIn();
    }

    // --- Popup 操作 ---

    /// <summary>
    /// Popup の登場アニメーションを再生する
    /// </summary>
    public void PlayPopup()
    {
        if (_popup == null) return;
        Debug.Log("[HisuiUI Demo] Play Popup");
        _popup.PlayIn();
    }

    // --- その他 ---

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;
        Debug.Log($"[HisuiUI Demo] Time.timeScale = {Time.timeScale}");
    }

    private bool WasKeyPressed(KeyCode oldKey, Key newKey)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current[newKey].wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(oldKey);
    }

    private bool IsModifierHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCommandKey.isPressed || Keyboard.current.rightCommandKey.isPressed;
        }
#endif
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
    }
}
