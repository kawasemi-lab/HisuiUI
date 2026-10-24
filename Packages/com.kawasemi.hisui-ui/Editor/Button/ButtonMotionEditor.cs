using Hisui.UI.Components;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="ButtonMotion"/> 用のカスタムインスペクタ。
    /// <para>
    /// セクション 3.6 に準拠し、各ボタン状態（Hover / Pressed / Disabled / Selected）および Click Punch の
    /// 有効化トグルに連動して <see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> による滑らかなアコーディオン表示を行う。
    /// また、使用しない基底プロパティ（<c>_playTrigger</c>）をインスペクタから除外して整理する。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(ButtonMotion))]
    [CanEditMultipleObjects]
    public sealed class ButtonMotionEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _selectableProp;
        private SerializedProperty _targetProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _normalMotionProp;
        private SerializedProperty _hoverProp;
        private SerializedProperty _pressedProp;
        private SerializedProperty _disabledProp;
        private SerializedProperty _selectedProp;
        private SerializedProperty _clickProp;

        private AnimBool _showHoverOptions;
        private AnimBool _showPressedOptions;
        private AnimBool _showDisabledOptions;
        private AnimBool _showSelectedOptions;
        private AnimBool _showClickOptions;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _selectableProp = serializedObject.FindProperty("_selectable");
            _targetProp = serializedObject.FindProperty("_target");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _normalMotionProp = _settingsProp.FindPropertyRelative("normalMotion");
                _hoverProp = _settingsProp.FindPropertyRelative("hover");
                _pressedProp = _settingsProp.FindPropertyRelative("pressed");
                _disabledProp = _settingsProp.FindPropertyRelative("disabled");
                _selectedProp = _settingsProp.FindPropertyRelative("selected");
                _clickProp = _settingsProp.FindPropertyRelative("click");
            }

            _showHoverOptions = CreateStateAnimBool(_hoverProp);
            _showPressedOptions = CreateStateAnimBool(_pressedProp);
            _showDisabledOptions = CreateStateAnimBool(_disabledProp);
            _showSelectedOptions = CreateStateAnimBool(_selectedProp);
            _showClickOptions = CreateStateAnimBool(_clickProp);
        }

        private AnimBool CreateStateAnimBool(SerializedProperty property)
        {
            bool initial = false;
            if (property != null)
            {
                var enabledProp = property.FindPropertyRelative("enabled");
                if (enabledProp != null)
                {
                    initial = enabledProp.boolValue;
                }
            }
            var anim = new AnimBool(initial);
            anim.valueChanged.AddListener(Repaint);
            return anim;
        }

        private void OnDisable()
        {
            _showHoverOptions?.valueChanged.RemoveListener(Repaint);
            _showPressedOptions?.valueChanged.RemoveListener(Repaint);
            _showDisabledOptions?.valueChanged.RemoveListener(Repaint);
            _showSelectedOptions?.valueChanged.RemoveListener(Repaint);
            _showClickOptions?.valueChanged.RemoveListener(Repaint);
        }

        private static void UpdateAnimBoolTarget(AnimBool animBool, SerializedProperty property)
        {
            if (animBool == null || property == null) return;
            var enabledProp = property.FindPropertyRelative("enabled");
            if (enabledProp != null)
            {
                animBool.target = !enabledProp.hasMultipleDifferentValues && enabledProp.boolValue;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_scriptProp != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(_scriptProp);
                }
            }

            // AnimBool 目標値の更新
            UpdateAnimBoolTarget(_showHoverOptions, _hoverProp);
            UpdateAnimBoolTarget(_showPressedOptions, _pressedProp);
            UpdateAnimBoolTarget(_showDisabledOptions, _disabledProp);
            UpdateAnimBoolTarget(_showSelectedOptions, _selectedProp);
            UpdateAnimBoolTarget(_showClickOptions, _clickProp);

            // Targets (Selectable / RectTransform)
            if (_selectableProp != null) EditorGUILayout.PropertyField(_selectableProp);
            if (_targetProp != null) EditorGUILayout.PropertyField(_targetProp);

            // Normal Return Motion
            if (_normalMotionProp != null) EditorGUILayout.PropertyField(_normalMotionProp, true);

            // States (Hover / Pressed / Disabled / Selected) - 滑らかなアコーディオン開閉
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
            DrawState("Hover", _hoverProp, _showHoverOptions);
            DrawState("Pressed", _pressedProp, _showPressedOptions);
            DrawState("Disabled", _disabledProp, _showDisabledOptions);
            DrawState("Selected", _selectedProp, _showSelectedOptions);

            // Click Punch - 滑らかなアコーディオン開閉
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Click Punch", EditorStyles.boldLabel);
            DrawClickPunch(_clickProp, _showClickOptions);

            // Hisui Common（ButtonMotion では使用しない _playTrigger は非表示にして整理）
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Hisui Common", EditorStyles.boldLabel);
            if (_ignoreTimeScaleProp != null) EditorGUILayout.PropertyField(_ignoreTimeScaleProp);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawState(string label, SerializedProperty stateProp, AnimBool animBool)
        {
            if (stateProp == null) return;
            var enabledProp = stateProp.FindPropertyRelative("enabled");
            var scaleProp = stateProp.FindPropertyRelative("scale");
            var offsetProp = stateProp.FindPropertyRelative("offset");
            var motionProp = stateProp.FindPropertyRelative("motion");

            if (enabledProp != null)
            {
                EditorGUILayout.PropertyField(enabledProp, new GUIContent(label, enabledProp.tooltip));
            }

            if (EditorGUILayout.BeginFadeGroup(animBool.faded))
            {
                EditorGUI.indentLevel++;
                if (scaleProp != null) EditorGUILayout.PropertyField(scaleProp);
                if (offsetProp != null) EditorGUILayout.PropertyField(offsetProp);
                if (motionProp != null) EditorGUILayout.PropertyField(motionProp, true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();
        }

        private static void DrawClickPunch(SerializedProperty clickProp, AnimBool animBool)
        {
            if (clickProp == null) return;
            var enabledProp = clickProp.FindPropertyRelative("enabled");
            var strengthProp = clickProp.FindPropertyRelative("strength");
            var motionProp = clickProp.FindPropertyRelative("motion");
            var vibratoProp = clickProp.FindPropertyRelative("vibrato");

            if (enabledProp != null)
            {
                EditorGUILayout.PropertyField(enabledProp, new GUIContent("Enabled", enabledProp.tooltip));
            }

            if (EditorGUILayout.BeginFadeGroup(animBool.faded))
            {
                EditorGUI.indentLevel++;
                if (strengthProp != null) EditorGUILayout.PropertyField(strengthProp);
                if (motionProp != null) EditorGUILayout.PropertyField(motionProp, true);
                if (vibratoProp != null) EditorGUILayout.PropertyField(vibratoProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();
        }
    }
}
