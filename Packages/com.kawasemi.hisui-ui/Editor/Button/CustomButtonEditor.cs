using Hisui.UI.Components;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.UI;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="CustomButton"/> 用のカスタムインスペクタ。
    /// 連打防止の有効化トグルに連動して <see cref="AnimBool"/> による滑らかなアコーディオン表示を行います。
    /// </summary>
    [CustomEditor(typeof(CustomButton))]
    [CanEditMultipleObjects]
    public class CustomButtonEditor : ButtonEditor
    {
        private SerializedProperty _useCooldownProp;
        private SerializedProperty _cooldownTimeProp;
        private SerializedProperty _disableOnClickProp;
        private SerializedProperty _restoreModeProp;
        private SerializedProperty _deselectOnPointerClickProp;
        private SerializedProperty _soundKeyProp;

        private AnimBool _showCooldownOptions;
        private AnimBool _showDisableOnClickOptions;

        protected override void OnEnable()
        {
            base.OnEnable();

            _useCooldownProp = serializedObject.FindProperty("_useCooldown");
            _cooldownTimeProp = serializedObject.FindProperty("_cooldownTime");
            _disableOnClickProp = serializedObject.FindProperty("_disableOnClick");
            _restoreModeProp = serializedObject.FindProperty("_restoreMode");
            _deselectOnPointerClickProp = serializedObject.FindProperty("_deselectOnPointerClick");
            _soundKeyProp = serializedObject.FindProperty("_soundKey");

            bool initialCooldown = _useCooldownProp != null && _useCooldownProp.boolValue;
            _showCooldownOptions = new AnimBool(initialCooldown);
            _showCooldownOptions.valueChanged.AddListener(Repaint);

            bool initialDisableOnClick = _disableOnClickProp != null && _disableOnClickProp.boolValue;
            _showDisableOnClickOptions = new AnimBool(initialDisableOnClick);
            _showDisableOnClickOptions.valueChanged.AddListener(Repaint);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _showCooldownOptions?.valueChanged.RemoveListener(Repaint);
            _showDisableOnClickOptions?.valueChanged.RemoveListener(Repaint);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Custom Button 独自設定
            EditorGUILayout.LabelField("Custom Button Settings", EditorStyles.boldLabel);

            if (_useCooldownProp != null)
            {
                EditorGUILayout.PropertyField(_useCooldownProp);
                _showCooldownOptions.target = !_useCooldownProp.hasMultipleDifferentValues && _useCooldownProp.boolValue;

                if (EditorGUILayout.BeginFadeGroup(_showCooldownOptions.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_cooldownTimeProp != null)
                    {
                        EditorGUILayout.PropertyField(_cooldownTimeProp);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();
            }

            if (_disableOnClickProp != null)
            {
                EditorGUILayout.PropertyField(_disableOnClickProp);
                _showDisableOnClickOptions.target = !_disableOnClickProp.hasMultipleDifferentValues && _disableOnClickProp.boolValue;

                if (EditorGUILayout.BeginFadeGroup(_showDisableOnClickOptions.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_restoreModeProp != null)
                    {
                        EditorGUILayout.PropertyField(_restoreModeProp);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();
            }

            if (_deselectOnPointerClickProp != null)
            {
                EditorGUILayout.PropertyField(_deselectOnPointerClickProp);
            }

            if (_soundKeyProp != null)
            {
                EditorGUILayout.PropertyField(_soundKeyProp);
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();

            // 標準 Button インスペクタ（Interactable, Transition, Navigation, OnClick 等）
            base.OnInspectorGUI();
        }
    }
}
