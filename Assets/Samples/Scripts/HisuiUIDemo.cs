using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;
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

    private float _currentHp = 1.0f;
    private bool _isPaused = false;

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
    }

    private void UnregisterButtonListeners()
    {
        if (_decreaseGaugeButton != null) _decreaseGaugeButton.onClick.RemoveListener(DecreaseGauge);
        if (_increaseGaugeButton != null) _increaseGaugeButton.onClick.RemoveListener(IncreaseGauge);
        if (_togglePanelButton != null) _togglePanelButton.onClick.RemoveListener(TogglePanel);
        if (_closePanelButton != null) _closePanelButton.onClick.RemoveListener(ClosePanel);
        if (_playEdgeSlideInButton != null) _playEdgeSlideInButton.onClick.RemoveListener(PlayEdgeSlideIn);
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
        else if (WasKeyPressed(KeyCode.P, Key.P) && !IsModifierHeld())
        {
            TogglePause();
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
