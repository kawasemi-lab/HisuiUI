using Cysharp.Threading.Tasks;
using Hisui.UI.Components;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HisuiUIDemo : MonoBehaviour
{
    [Header("Components to Test")]
    [SerializeField] private DelayGauge _gauge;
    [SerializeField] private PanelTransition _panel;

    private float _currentHp = 1.0f;
    private bool _isPaused = false;

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
        if (_gauge != null)
        {
            _gauge.OnValueChanged -= OnGaugeValueChanged;
        }
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
            TakeDamage(0.2f);
        }
        else if (WasKeyPressed(KeyCode.H, Key.H))
        {
            Heal(0.2f);
        }
        else if (WasKeyPressed(KeyCode.R, Key.R))
        {
            ResetGauge();
        }
        else if (WasKeyPressed(KeyCode.P, Key.P) && !IsModifierHeld())
        {
            TogglePause();
        }
    }

    public void TogglePanel()
    {
        if (_panel == null) return;
        Debug.Log($"[HisuiUI Demo] Toggle Panel (Currently Open: {_panel.IsOpen})");
        _panel.Toggle();
    }

    public void TakeDamage(float amount)
    {
        if (_gauge == null) return;
        _currentHp = Mathf.Max(0f, _currentHp - amount);
        Debug.Log($"[HisuiUI Demo] Damage! New HP: {_currentHp:F2}");
        _gauge.SetValue(_currentHp);
    }

    public void Heal(float amount)
    {
        if (_gauge == null) return;
        _currentHp = Mathf.Min(1f, _currentHp + amount);
        Debug.Log($"[HisuiUI Demo] Heal! New HP: {_currentHp:F2}");
        _gauge.SetValue(_currentHp);
    }

    public void ResetGauge()
    {
        if (_gauge == null) return;
        _currentHp = 1.0f;
        Debug.Log("[HisuiUI Demo] Gauge Reset (Immediate)");
        _gauge.SetValue(1.0f, immediate: true);
    }

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

    // ゲーム画面（Game ビュー）上に操作用ボタンを表示
    private void OnGUI()
    {
        using (new GUILayout.AreaScope(new Rect(20, 20, 240, 320), "HisuiUI Test Controller", GUI.skin.window))
        {
            GUILayout.Label($"Current HP: {_currentHp:P0}");
            GUILayout.Label($"Panel Open: {(_panel != null && _panel.IsOpen)}");
            GUILayout.Label($"TimeScale: {Time.timeScale:F1}");
            GUILayout.Space(6);

            if (GUILayout.Button("Damage -20% (D)"))
            {
                TakeDamage(0.2f);
            }

            if (GUILayout.Button("Heal +20% (H)"))
            {
                Heal(0.2f);
            }

            if (GUILayout.Button("Reset HP (R)"))
            {
                ResetGauge();
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Toggle Panel (Space)"))
            {
                TogglePanel();
            }

            GUILayout.Space(6);

            GUI.color = _isPaused ? Color.yellow : Color.white;
            if (GUILayout.Button(_isPaused ? "Resume Time (P)" : "Pause Time (P)"))
            {
                TogglePause();
            }
            GUI.color = Color.white;
        }
    }
}
