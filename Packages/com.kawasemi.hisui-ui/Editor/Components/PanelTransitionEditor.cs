using Hisui.UI.Components;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="PanelTransition"/> 用のカスタムインスペクタ。
    /// <para>
    /// セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> を使用して
    /// フェード・スケール・スライド演出設定の開閉を滑らかにアニメーションさせる。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(PanelTransition))]
    [CanEditMultipleObjects]
    public sealed class PanelTransitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _rectTransformProp;
        private SerializedProperty _canvasGroupProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _initialStateProp;
        private SerializedProperty _interactableTimingProp;
        private SerializedProperty _deactivateOnCloseProp;
        private SerializedProperty _scaleDurationOnReverseProp;
        private SerializedProperty _openMotionProp;
        private SerializedProperty _closeMotionProp;
        private SerializedProperty _useFadeProp;
        private SerializedProperty _openAlphaProp;
        private SerializedProperty _closedAlphaProp;
        private SerializedProperty _useScaleProp;
        private SerializedProperty _closedScaleProp;
        private SerializedProperty _useSlideProp;
        private SerializedProperty _slideDirectionProp;
        private SerializedProperty _slideUnitProp;
        private SerializedProperty _slideDistanceProp;
        private SerializedProperty _customSlideVectorProp;

        private AnimBool _showFadeOptions;
        private AnimBool _showScaleOptions;
        private AnimBool _showSlideOptions;
        private AnimBool _showStandardSlideOptions;
        private AnimBool _showCustomSlideOptions;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _rectTransformProp = serializedObject.FindProperty("_rectTransform");
            _canvasGroupProp = serializedObject.FindProperty("_canvasGroup");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _initialStateProp = _settingsProp.FindPropertyRelative("initialState");
                _interactableTimingProp = _settingsProp.FindPropertyRelative("interactableTiming");
                _deactivateOnCloseProp = _settingsProp.FindPropertyRelative("deactivateOnClose");
                _scaleDurationOnReverseProp = _settingsProp.FindPropertyRelative("scaleDurationOnReverse");
                _openMotionProp = _settingsProp.FindPropertyRelative("openMotion");
                _closeMotionProp = _settingsProp.FindPropertyRelative("closeMotion");
                _useFadeProp = _settingsProp.FindPropertyRelative("useFade");
                _openAlphaProp = _settingsProp.FindPropertyRelative("openAlpha");
                _closedAlphaProp = _settingsProp.FindPropertyRelative("closedAlpha");
                _useScaleProp = _settingsProp.FindPropertyRelative("useScale");
                _closedScaleProp = _settingsProp.FindPropertyRelative("closedScale");
                _useSlideProp = _settingsProp.FindPropertyRelative("useSlide");
                _slideDirectionProp = _settingsProp.FindPropertyRelative("slideDirection");
                _slideUnitProp = _settingsProp.FindPropertyRelative("slideUnit");
                _slideDistanceProp = _settingsProp.FindPropertyRelative("slideDistance");
                _customSlideVectorProp = _settingsProp.FindPropertyRelative("customSlideVector");
            }

            bool initialFade = _useFadeProp != null && _useFadeProp.boolValue;
            _showFadeOptions = new AnimBool(initialFade);
            _showFadeOptions.valueChanged.AddListener(Repaint);

            bool initialScale = _useScaleProp != null && _useScaleProp.boolValue;
            _showScaleOptions = new AnimBool(initialScale);
            _showScaleOptions.valueChanged.AddListener(Repaint);

            bool initialSlide = _useSlideProp != null && _useSlideProp.boolValue;
            _showSlideOptions = new AnimBool(initialSlide);
            _showSlideOptions.valueChanged.AddListener(Repaint);

            var dir = GetCurrentSlideDirection();
            bool initialStandardSlide = initialSlide && dir != PanelTransition.SlideDirection.Custom;
            _showStandardSlideOptions = new AnimBool(initialStandardSlide);
            _showStandardSlideOptions.valueChanged.AddListener(Repaint);

            bool initialCustomSlide = initialSlide && dir == PanelTransition.SlideDirection.Custom;
            _showCustomSlideOptions = new AnimBool(initialCustomSlide);
            _showCustomSlideOptions.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showFadeOptions?.valueChanged.RemoveListener(Repaint);
            _showScaleOptions?.valueChanged.RemoveListener(Repaint);
            _showSlideOptions?.valueChanged.RemoveListener(Repaint);
            _showStandardSlideOptions?.valueChanged.RemoveListener(Repaint);
            _showCustomSlideOptions?.valueChanged.RemoveListener(Repaint);
        }

        private PanelTransition.SlideDirection GetCurrentSlideDirection()
        {
            if (_slideDirectionProp == null || _slideDirectionProp.hasMultipleDifferentValues)
            {
                return (PanelTransition.SlideDirection)(-1);
            }
            return (PanelTransition.SlideDirection)_slideDirectionProp.enumValueIndex;
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

            bool useSlide = _useSlideProp != null && (!_useSlideProp.hasMultipleDifferentValues && _useSlideProp.boolValue);
            var slideDirection = GetCurrentSlideDirection();

            // AnimBool 目標値の更新
            _showFadeOptions.target = _useFadeProp != null && (!_useFadeProp.hasMultipleDifferentValues && _useFadeProp.boolValue);
            _showScaleOptions.target = _useScaleProp != null && (!_useScaleProp.hasMultipleDifferentValues && _useScaleProp.boolValue);
            _showSlideOptions.target = useSlide;
            _showStandardSlideOptions.target = useSlide && slideDirection != PanelTransition.SlideDirection.Custom && slideDirection != (PanelTransition.SlideDirection)(-1);
            _showCustomSlideOptions.target = useSlide && slideDirection == PanelTransition.SlideDirection.Custom;

            // Target Components
            if (_rectTransformProp != null) EditorGUILayout.PropertyField(_rectTransformProp);
            if (_canvasGroupProp != null) EditorGUILayout.PropertyField(_canvasGroupProp);

            // Initial & Lifecycle
            if (_initialStateProp != null) EditorGUILayout.PropertyField(_initialStateProp);
            if (_interactableTimingProp != null) EditorGUILayout.PropertyField(_interactableTimingProp);
            if (_deactivateOnCloseProp != null) EditorGUILayout.PropertyField(_deactivateOnCloseProp);
            if (_scaleDurationOnReverseProp != null) EditorGUILayout.PropertyField(_scaleDurationOnReverseProp);

            // Open / Close Motion
            if (_openMotionProp != null) EditorGUILayout.PropertyField(_openMotionProp);
            if (_closeMotionProp != null) EditorGUILayout.PropertyField(_closeMotionProp);

            // Fade (滑らかなアコーディオン開閉)
            if (_useFadeProp != null) EditorGUILayout.PropertyField(_useFadeProp);
            if (EditorGUILayout.BeginFadeGroup(_showFadeOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_openAlphaProp != null) EditorGUILayout.PropertyField(_openAlphaProp);
                if (_closedAlphaProp != null) EditorGUILayout.PropertyField(_closedAlphaProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Scale (滑らかなアコーディオン開閉)
            if (_useScaleProp != null) EditorGUILayout.PropertyField(_useScaleProp);
            if (EditorGUILayout.BeginFadeGroup(_showScaleOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_closedScaleProp != null) EditorGUILayout.PropertyField(_closedScaleProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Slide (滑らかなアコーディオン開閉)
            if (_useSlideProp != null) EditorGUILayout.PropertyField(_useSlideProp);
            if (EditorGUILayout.BeginFadeGroup(_showSlideOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_slideDirectionProp != null) EditorGUILayout.PropertyField(_slideDirectionProp);

                // 標準方向の移動量設定（パネル比 / ピクセル距離）
                if (EditorGUILayout.BeginFadeGroup(_showStandardSlideOptions.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_slideUnitProp != null) EditorGUILayout.PropertyField(_slideUnitProp);
                    if (_slideDistanceProp != null) EditorGUILayout.PropertyField(_slideDistanceProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();

                // Custom 方向時のカスタムベクトル設定
                if (EditorGUILayout.BeginFadeGroup(_showCustomSlideOptions.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_customSlideVectorProp != null) EditorGUILayout.PropertyField(_customSlideVectorProp);
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
