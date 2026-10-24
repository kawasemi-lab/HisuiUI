using Hisui.UI.Components;
using Hisui.UI.Core;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="EdgeSlideIn"/> 用のカスタムインスペクタ。
    /// <para>
    /// セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> を使用して
    /// Custom 方向ベクトルや退場個別方向設定、OnEnable 開始遅延などの条件付き項目を滑らかに開閉する。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(EdgeSlideIn))]
    [CanEditMultipleObjects]
    public sealed class EdgeSlideInEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _targetsProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _directionProp;
        private SerializedProperty _customDirectionProp;
        private SerializedProperty _alternateDirectionProp;
        private SerializedProperty _separateOutDirectionProp;
        private SerializedProperty _outDirectionProp;
        private SerializedProperty _outCustomDirectionProp;
        private SerializedProperty _extraMarginProp;
        private SerializedProperty _startHiddenProp;
        private SerializedProperty _startDelayProp;
        private SerializedProperty _autoOutDelayProp;
        private SerializedProperty _scaleDurationOnReverseProp;
        private SerializedProperty _useFadeProp;
        private SerializedProperty _inPhaseProp;
        private SerializedProperty _outPhaseProp;

        private AnimBool _showInCustomDirection;
        private AnimBool _showSeparateOut;
        private AnimBool _showOutCustomDirection;
        private AnimBool _showStartDelay;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _targetsProp = serializedObject.FindProperty("_targets");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _directionProp = _settingsProp.FindPropertyRelative("direction");
                _customDirectionProp = _settingsProp.FindPropertyRelative("customDirection");
                _alternateDirectionProp = _settingsProp.FindPropertyRelative("alternateDirection");
                _separateOutDirectionProp = _settingsProp.FindPropertyRelative("separateOutDirection");
                _outDirectionProp = _settingsProp.FindPropertyRelative("outDirection");
                _outCustomDirectionProp = _settingsProp.FindPropertyRelative("outCustomDirection");
                _extraMarginProp = _settingsProp.FindPropertyRelative("extraMargin");
                _startHiddenProp = _settingsProp.FindPropertyRelative("startHidden");
                _startDelayProp = _settingsProp.FindPropertyRelative("startDelay");
                _autoOutDelayProp = _settingsProp.FindPropertyRelative("autoOutDelay");
                _scaleDurationOnReverseProp = _settingsProp.FindPropertyRelative("scaleDurationOnReverse");
                _useFadeProp = _settingsProp.FindPropertyRelative("useFade");
                _inPhaseProp = _settingsProp.FindPropertyRelative("inPhase");
                _outPhaseProp = _settingsProp.FindPropertyRelative("outPhase");
            }

            var inDir = GetCurrentDirection();
            _showInCustomDirection = new AnimBool(inDir == EdgeDirection.Custom);
            _showInCustomDirection.valueChanged.AddListener(Repaint);

            bool initialSeparateOut = _separateOutDirectionProp != null && _separateOutDirectionProp.boolValue;
            _showSeparateOut = new AnimBool(initialSeparateOut);
            _showSeparateOut.valueChanged.AddListener(Repaint);

            var outDir = GetCurrentOutDirection();
            _showOutCustomDirection = new AnimBool(initialSeparateOut && outDir == EdgeDirection.Custom);
            _showOutCustomDirection.valueChanged.AddListener(Repaint);

            var playTrigger = GetCurrentPlayTrigger();
            _showStartDelay = new AnimBool(playTrigger == PlayTrigger.OnEnable);
            _showStartDelay.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showInCustomDirection?.valueChanged.RemoveListener(Repaint);
            _showSeparateOut?.valueChanged.RemoveListener(Repaint);
            _showOutCustomDirection?.valueChanged.RemoveListener(Repaint);
            _showStartDelay?.valueChanged.RemoveListener(Repaint);
        }

        private EdgeDirection GetCurrentDirection()
        {
            if (_directionProp == null || _directionProp.hasMultipleDifferentValues)
            {
                return (EdgeDirection)(-1);
            }
            return (EdgeDirection)_directionProp.enumValueIndex;
        }

        private EdgeDirection GetCurrentOutDirection()
        {
            if (_outDirectionProp == null || _outDirectionProp.hasMultipleDifferentValues)
            {
                return (EdgeDirection)(-1);
            }
            return (EdgeDirection)_outDirectionProp.enumValueIndex;
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

            var inDir = GetCurrentDirection();
            bool separateOut = _separateOutDirectionProp != null && (!_separateOutDirectionProp.hasMultipleDifferentValues && _separateOutDirectionProp.boolValue);
            var outDir = GetCurrentOutDirection();
            var playTrigger = GetCurrentPlayTrigger();

            // AnimBool 目標値の更新
            _showInCustomDirection.target = inDir == EdgeDirection.Custom;
            _showSeparateOut.target = separateOut;
            _showOutCustomDirection.target = separateOut && outDir == EdgeDirection.Custom;
            _showStartDelay.target = playTrigger == PlayTrigger.OnEnable;

            // Targets (empty = this RectTransform)
            if (_targetsProp != null) EditorGUILayout.PropertyField(_targetsProp, true);

            // Direction
            if (_directionProp != null) EditorGUILayout.PropertyField(_directionProp);

            // Custom 方向ベクトル（滑らかなアコーディオン開閉）
            if (EditorGUILayout.BeginFadeGroup(_showInCustomDirection.faded))
            {
                EditorGUI.indentLevel++;
                if (_customDirectionProp != null) EditorGUILayout.PropertyField(_customDirectionProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_alternateDirectionProp != null) EditorGUILayout.PropertyField(_alternateDirectionProp);
            if (_separateOutDirectionProp != null) EditorGUILayout.PropertyField(_separateOutDirectionProp);

            // Separate Out Direction 連動設定（滑らかなアコーディオン開閉）
            if (EditorGUILayout.BeginFadeGroup(_showSeparateOut.faded))
            {
                EditorGUI.indentLevel++;
                if (_outDirectionProp != null) EditorGUILayout.PropertyField(_outDirectionProp);

                // Out 方向が Custom の場合のベクトル設定
                if (EditorGUILayout.BeginFadeGroup(_showOutCustomDirection.faded))
                {
                    EditorGUI.indentLevel++;
                    if (_outCustomDirectionProp != null) EditorGUILayout.PropertyField(_outCustomDirectionProp);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFadeGroup();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_extraMarginProp != null) EditorGUILayout.PropertyField(_extraMarginProp);

            // Initial State
            if (_startHiddenProp != null) EditorGUILayout.PropertyField(_startHiddenProp);

            // Start Delay（PlayTrigger.OnEnable 時のみ滑らかに開閉）
            if (EditorGUILayout.BeginFadeGroup(_showStartDelay.faded))
            {
                EditorGUI.indentLevel++;
                if (_startDelayProp != null) EditorGUILayout.PropertyField(_startDelayProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_autoOutDelayProp != null) EditorGUILayout.PropertyField(_autoOutDelayProp);
            if (_scaleDurationOnReverseProp != null) EditorGUILayout.PropertyField(_scaleDurationOnReverseProp);

            // Fade
            if (_useFadeProp != null) EditorGUILayout.PropertyField(_useFadeProp);

            // In / Out Phase Settings
            if (_inPhaseProp != null) EditorGUILayout.PropertyField(_inPhaseProp, true);
            if (_outPhaseProp != null) EditorGUILayout.PropertyField(_outPhaseProp, true);

            // Hisui Common
            if (_playTriggerProp != null) EditorGUILayout.PropertyField(_playTriggerProp);
            if (_ignoreTimeScaleProp != null) EditorGUILayout.PropertyField(_ignoreTimeScaleProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
