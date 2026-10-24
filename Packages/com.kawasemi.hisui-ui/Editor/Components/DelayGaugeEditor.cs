using Hisui.UI.Components;
using Hisui.UI.Palette;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="DelayGauge"/> 用のカスタムインスペクタ。
    /// <para>
    /// セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> による滑らかなアコーディオン表示を提供する。
    /// カラー設定に uPalette 参照（Palette モード）が含まれている場合のみ、テーマクロスフェード時間設定をフェード表示する。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(DelayGauge))]
    [CanEditMultipleObjects]
    public sealed class DelayGaugeEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _frontImageProp;
        private SerializedProperty _backImageProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _valueProp;
        private SerializedProperty _directionProp;
        private SerializedProperty _damageFrontMotionProp;
        private SerializedProperty _damageHoldDurationProp;
        private SerializedProperty _damageBackMotionProp;
        private SerializedProperty _healLeadMotionProp;
        private SerializedProperty _healHoldDurationProp;
        private SerializedProperty _healFollowMotionProp;
        private SerializedProperty _mainColorProp;
        private SerializedProperty _damageDelayColorProp;
        private SerializedProperty _healDelayColorProp;
        private SerializedProperty _themeCrossfadeDurationProp;

        private AnimBool _showThemeCrossfade;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _frontImageProp = serializedObject.FindProperty("_frontImage");
            _backImageProp = serializedObject.FindProperty("_backImage");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _valueProp = _settingsProp.FindPropertyRelative("value");
                _directionProp = _settingsProp.FindPropertyRelative("direction");
                _damageFrontMotionProp = _settingsProp.FindPropertyRelative("damageFrontMotion");
                _damageHoldDurationProp = _settingsProp.FindPropertyRelative("damageHoldDuration");
                _damageBackMotionProp = _settingsProp.FindPropertyRelative("damageBackMotion");
                _healLeadMotionProp = _settingsProp.FindPropertyRelative("healLeadMotion");
                _healHoldDurationProp = _settingsProp.FindPropertyRelative("healHoldDuration");
                _healFollowMotionProp = _settingsProp.FindPropertyRelative("healFollowMotion");
                _mainColorProp = _settingsProp.FindPropertyRelative("mainColor");
                _damageDelayColorProp = _settingsProp.FindPropertyRelative("damageDelayColor");
                _healDelayColorProp = _settingsProp.FindPropertyRelative("healDelayColor");
                _themeCrossfadeDurationProp = _settingsProp.FindPropertyRelative("themeCrossfadeDuration");
            }

            bool initialPalette = IsAnyColorUsingPalette();
            _showThemeCrossfade = new AnimBool(initialPalette);
            _showThemeCrossfade.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showThemeCrossfade?.valueChanged.RemoveListener(Repaint);
        }

        private bool IsAnyColorUsingPalette()
        {
            return IsPaletteColor(_mainColorProp) ||
                   IsPaletteColor(_damageDelayColorProp) ||
                   IsPaletteColor(_healDelayColorProp);
        }

        private static bool IsPaletteColor(SerializedProperty colorProp)
        {
            if (colorProp == null) return false;
            var modeProp = colorProp.FindPropertyRelative("mode");
            if (modeProp == null || modeProp.hasMultipleDifferentValues) return false;
            return modeProp.enumValueIndex == (int)ColorSourceMode.Palette;
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

            _showThemeCrossfade.target = IsAnyColorUsingPalette();

            // Target Fill Objects (Image.Type.Sliced / Anchor Driven)
            if (_frontImageProp != null) EditorGUILayout.PropertyField(_frontImageProp);
            if (_backImageProp != null) EditorGUILayout.PropertyField(_backImageProp);

            // Value & Direction
            if (_valueProp != null) EditorGUILayout.PropertyField(_valueProp);
            if (_directionProp != null) EditorGUILayout.PropertyField(_directionProp);

            // Damage (Decrease) Motion
            if (_damageFrontMotionProp != null) EditorGUILayout.PropertyField(_damageFrontMotionProp);
            if (_damageHoldDurationProp != null) EditorGUILayout.PropertyField(_damageHoldDurationProp);
            if (_damageBackMotionProp != null) EditorGUILayout.PropertyField(_damageBackMotionProp);

            // Heal (Increase) Motion
            if (_healLeadMotionProp != null) EditorGUILayout.PropertyField(_healLeadMotionProp);
            if (_healHoldDurationProp != null) EditorGUILayout.PropertyField(_healHoldDurationProp);
            if (_healFollowMotionProp != null) EditorGUILayout.PropertyField(_healFollowMotionProp);

            // Colors (Direct or uPalette)
            if (_mainColorProp != null) EditorGUILayout.PropertyField(_mainColorProp);
            if (_damageDelayColorProp != null) EditorGUILayout.PropertyField(_damageDelayColorProp);
            if (_healDelayColorProp != null) EditorGUILayout.PropertyField(_healDelayColorProp);

            // Theme crossfade duration (uPalette 使用時のみ滑らかに開閉)
            if (EditorGUILayout.BeginFadeGroup(_showThemeCrossfade.faded))
            {
                EditorGUI.indentLevel++;
                if (_themeCrossfadeDurationProp != null)
                {
                    EditorGUILayout.PropertyField(_themeCrossfadeDurationProp);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Hisui Common
            if (_playTriggerProp != null) EditorGUILayout.PropertyField(_playTriggerProp);
            if (_ignoreTimeScaleProp != null) EditorGUILayout.PropertyField(_ignoreTimeScaleProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
