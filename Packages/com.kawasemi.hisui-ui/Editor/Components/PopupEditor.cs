using Hisui.UI.Components;
using Hisui.UI.Core;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="Popup"/> 用のカスタムインスペクタ。
    /// <para>
    /// 設計書セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> を使用して
    /// フェード・スケール・スライドの各オプションや Out 個別指定、OnEnable 開始遅延を滑らかに開閉する。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(Popup))]
    [CanEditMultipleObjects]
    public sealed class PopupEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _targetProp;
        private SerializedProperty _closeButtonProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _startHiddenProp;
        private SerializedProperty _deactivateOnCloseProp;
        private SerializedProperty _startDelayProp;
        private SerializedProperty _autoOutDelayProp;
        private SerializedProperty _scaleDurationOnReverseProp;
        private SerializedProperty _inMotionProp;
        private SerializedProperty _outMotionProp;

        private SerializedProperty _useFadeProp;
        private SerializedProperty _inStartAlphaProp;
        private SerializedProperty _separateOutAlphaProp;
        private SerializedProperty _outEndAlphaProp;

        private SerializedProperty _useScaleProp;
        private SerializedProperty _inStartScaleProp;
        private SerializedProperty _separateOutScaleProp;
        private SerializedProperty _outEndScaleProp;

        private SerializedProperty _useSlideProp;
        private SerializedProperty _offsetUnitProp;
        private SerializedProperty _inOffsetProp;
        private SerializedProperty _separateOutOffsetProp;
        private SerializedProperty _outOffsetProp;

        private AnimBool _showStartDelay;
        private AnimBool _showFadeOptions;
        private AnimBool _showSeparateOutAlpha;
        private AnimBool _showScaleOptions;
        private AnimBool _showSeparateOutScale;
        private AnimBool _showSlideOptions;
        private AnimBool _showSeparateOutOffset;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _targetProp = serializedObject.FindProperty("_target");
            _closeButtonProp = serializedObject.FindProperty("_closeButton");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _startHiddenProp = _settingsProp.FindPropertyRelative("startHidden");
                _deactivateOnCloseProp = _settingsProp.FindPropertyRelative("deactivateOnClose");
                _startDelayProp = _settingsProp.FindPropertyRelative("startDelay");
                _autoOutDelayProp = _settingsProp.FindPropertyRelative("autoOutDelay");
                _scaleDurationOnReverseProp = _settingsProp.FindPropertyRelative("scaleDurationOnReverse");
                _inMotionProp = _settingsProp.FindPropertyRelative("inMotion");
                _outMotionProp = _settingsProp.FindPropertyRelative("outMotion");

                _useFadeProp = _settingsProp.FindPropertyRelative("useFade");
                _inStartAlphaProp = _settingsProp.FindPropertyRelative("inStartAlpha");
                _separateOutAlphaProp = _settingsProp.FindPropertyRelative("separateOutAlpha");
                _outEndAlphaProp = _settingsProp.FindPropertyRelative("outEndAlpha");

                _useScaleProp = _settingsProp.FindPropertyRelative("useScale");
                _inStartScaleProp = _settingsProp.FindPropertyRelative("inStartScale");
                _separateOutScaleProp = _settingsProp.FindPropertyRelative("separateOutScale");
                _outEndScaleProp = _settingsProp.FindPropertyRelative("outEndScale");

                _useSlideProp = _settingsProp.FindPropertyRelative("useSlide");
                _offsetUnitProp = _settingsProp.FindPropertyRelative("offsetUnit");
                _inOffsetProp = _settingsProp.FindPropertyRelative("inOffset");
                _separateOutOffsetProp = _settingsProp.FindPropertyRelative("separateOutOffset");
                _outOffsetProp = _settingsProp.FindPropertyRelative("outOffset");
            }

            bool initStartDelay = GetCurrentPlayTrigger() == PlayTrigger.OnEnable;
            _showStartDelay = new AnimBool(initStartDelay);
            _showStartDelay.valueChanged.AddListener(Repaint);

            bool initFade = GetBool(_useFadeProp);
            _showFadeOptions = new AnimBool(initFade);
            _showFadeOptions.valueChanged.AddListener(Repaint);

            _showSeparateOutAlpha = new AnimBool(initFade && GetBool(_separateOutAlphaProp));
            _showSeparateOutAlpha.valueChanged.AddListener(Repaint);

            bool initScale = GetBool(_useScaleProp);
            _showScaleOptions = new AnimBool(initScale);
            _showScaleOptions.valueChanged.AddListener(Repaint);

            _showSeparateOutScale = new AnimBool(initScale && GetBool(_separateOutScaleProp));
            _showSeparateOutScale.valueChanged.AddListener(Repaint);

            bool initSlide = GetBool(_useSlideProp);
            _showSlideOptions = new AnimBool(initSlide);
            _showSlideOptions.valueChanged.AddListener(Repaint);

            _showSeparateOutOffset = new AnimBool(initSlide && GetBool(_separateOutOffsetProp));
            _showSeparateOutOffset.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showStartDelay?.valueChanged.RemoveListener(Repaint);
            _showFadeOptions?.valueChanged.RemoveListener(Repaint);
            _showSeparateOutAlpha?.valueChanged.RemoveListener(Repaint);
            _showScaleOptions?.valueChanged.RemoveListener(Repaint);
            _showSeparateOutScale?.valueChanged.RemoveListener(Repaint);
            _showSlideOptions?.valueChanged.RemoveListener(Repaint);
            _showSeparateOutOffset?.valueChanged.RemoveListener(Repaint);
        }

        private static bool GetBool(SerializedProperty prop)
        {
            return prop != null && !prop.hasMultipleDifferentValues && prop.boolValue;
        }

        private PlayTrigger GetCurrentPlayTrigger()
        {
            if (_playTriggerProp == null || _playTriggerProp.hasMultipleDifferentValues)
            {
                return (PlayTrigger)(-1);
            }
            return (PlayTrigger)_playTriggerProp.enumValueIndex;
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

            bool useFade = GetBool(_useFadeProp);
            bool separateOutAlpha = GetBool(_separateOutAlphaProp);
            bool useScale = GetBool(_useScaleProp);
            bool separateOutScale = GetBool(_separateOutScaleProp);
            bool useSlide = GetBool(_useSlideProp);
            bool separateOutOffset = GetBool(_separateOutOffsetProp);
            var playTrigger = GetCurrentPlayTrigger();

            // AnimBool 目標値の更新
            _showStartDelay.target = playTrigger == PlayTrigger.OnEnable;
            _showFadeOptions.target = useFade;
            _showSeparateOutAlpha.target = useFade && separateOutAlpha;
            _showScaleOptions.target = useScale;
            _showSeparateOutScale.target = useScale && separateOutScale;
            _showSlideOptions.target = useSlide;
            _showSeparateOutOffset.target = useSlide && separateOutOffset;

            // Target & Trigger
            if (_targetProp != null) EditorGUILayout.PropertyField(_targetProp);
            if (_closeButtonProp != null) EditorGUILayout.PropertyField(_closeButtonProp);

            // Initial & Lifecycle
            if (_startHiddenProp != null) EditorGUILayout.PropertyField(_startHiddenProp);
            if (_deactivateOnCloseProp != null) EditorGUILayout.PropertyField(_deactivateOnCloseProp);

            if (EditorGUILayout.BeginFadeGroup(_showStartDelay.faded))
            {
                EditorGUI.indentLevel++;
                if (_startDelayProp != null) EditorGUILayout.PropertyField(_startDelayProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_autoOutDelayProp != null) EditorGUILayout.PropertyField(_autoOutDelayProp);
            if (_scaleDurationOnReverseProp != null) EditorGUILayout.PropertyField(_scaleDurationOnReverseProp);

            // In / Out Motion
            if (_inMotionProp != null) EditorGUILayout.PropertyField(_inMotionProp, true);
            if (_outMotionProp != null) EditorGUILayout.PropertyField(_outMotionProp, true);

            // Fade
            if (_useFadeProp != null) EditorGUILayout.PropertyField(_useFadeProp);
            if (EditorGUILayout.BeginFadeGroup(_showFadeOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_inStartAlphaProp != null) EditorGUILayout.PropertyField(_inStartAlphaProp);
                if (_separateOutAlphaProp != null) EditorGUILayout.PropertyField(_separateOutAlphaProp);

                if (EditorGUILayout.BeginFadeGroup(_showSeparateOutAlpha.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_outEndAlphaProp != null) EditorGUILayout.PropertyField(_outEndAlphaProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Scale
            if (_useScaleProp != null) EditorGUILayout.PropertyField(_useScaleProp);
            if (EditorGUILayout.BeginFadeGroup(_showScaleOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_inStartScaleProp != null) EditorGUILayout.PropertyField(_inStartScaleProp);
                if (_separateOutScaleProp != null) EditorGUILayout.PropertyField(_separateOutScaleProp);

                if (EditorGUILayout.BeginFadeGroup(_showSeparateOutScale.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_outEndScaleProp != null) EditorGUILayout.PropertyField(_outEndScaleProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Slide
            if (_useSlideProp != null) EditorGUILayout.PropertyField(_useSlideProp);
            if (EditorGUILayout.BeginFadeGroup(_showSlideOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_offsetUnitProp != null) EditorGUILayout.PropertyField(_offsetUnitProp);
                if (_inOffsetProp != null) EditorGUILayout.PropertyField(_inOffsetProp);
                if (_separateOutOffsetProp != null) EditorGUILayout.PropertyField(_separateOutOffsetProp);

                if (EditorGUILayout.BeginFadeGroup(_showSeparateOutOffset.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_outOffsetProp != null) EditorGUILayout.PropertyField(_outOffsetProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();

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
