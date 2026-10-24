using Hisui.UI.Components;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="LoadingScreen"/> 用のカスタムインスペクタ。
    /// <para>
    /// Unity 標準のコンポーネント（Selectable.Transition 等）と同様に <see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> を使用し、
    /// 項目の出し分けを滑らかなアニメーションで切り替える。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(LoadingScreen))]
    [CanEditMultipleObjects]
    public sealed class LoadingScreenEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _ignoreTimeScaleProp;
        private SerializedProperty _canvasGroupProp;
        private SerializedProperty _spinnerProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _showMotionProp;
        private SerializedProperty _hideMotionProp;
        private SerializedProperty _hideModeProp;
        private SerializedProperty _minDisplayTimeProp;
        private SerializedProperty _waitFramesAfterLoadProp;
        private SerializedProperty _persistAcrossScenesProp;
        private SerializedProperty _applySortingOrderProp;
        private SerializedProperty _sortingOrderProp;
        private SerializedProperty _initialVisibleProp;
        private SerializedProperty _spinnerTypeProp;
        private SerializedProperty _rotateSpeedProp;
        private SerializedProperty _rotateClockwiseProp;
        private SerializedProperty _rotateStepCountProp;
        private SerializedProperty _dotsProp;
        private SerializedProperty _dotsCycleDurationProp;
        private SerializedProperty _dotsPhaseStepProp;
        private SerializedProperty _dotsMinScaleProp;
        private SerializedProperty _dotsMaxScaleProp;

        private AnimBool _showSortingOrder;
        private AnimBool _showRotateOptions;
        private AnimBool _showDotsOptions;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");
            _canvasGroupProp = serializedObject.FindProperty("_canvasGroup");
            _spinnerProp = serializedObject.FindProperty("_spinner");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _showMotionProp = _settingsProp.FindPropertyRelative("showMotion");
                _hideMotionProp = _settingsProp.FindPropertyRelative("hideMotion");
                _hideModeProp = _settingsProp.FindPropertyRelative("hideMode");
                _minDisplayTimeProp = _settingsProp.FindPropertyRelative("minDisplayTime");
                _waitFramesAfterLoadProp = _settingsProp.FindPropertyRelative("waitFramesAfterLoad");
                _persistAcrossScenesProp = _settingsProp.FindPropertyRelative("persistAcrossScenes");
                _applySortingOrderProp = _settingsProp.FindPropertyRelative("applySortingOrder");
                _sortingOrderProp = _settingsProp.FindPropertyRelative("sortingOrder");
                _initialVisibleProp = _settingsProp.FindPropertyRelative("initialVisible");
                _spinnerTypeProp = _settingsProp.FindPropertyRelative("spinnerType");
                _rotateSpeedProp = _settingsProp.FindPropertyRelative("rotateSpeed");
                _rotateClockwiseProp = _settingsProp.FindPropertyRelative("rotateClockwise");
                _rotateStepCountProp = _settingsProp.FindPropertyRelative("rotateStepCount");
                _dotsProp = _settingsProp.FindPropertyRelative("dots");
                _dotsCycleDurationProp = _settingsProp.FindPropertyRelative("dotsCycleDuration");
                _dotsPhaseStepProp = _settingsProp.FindPropertyRelative("dotsPhaseStep");
                _dotsMinScaleProp = _settingsProp.FindPropertyRelative("dotsMinScale");
                _dotsMaxScaleProp = _settingsProp.FindPropertyRelative("dotsMaxScale");
            }

            // Unity 標準と同じ AnimBool アニメーション制御の初期化
            bool initialSorting = _applySortingOrderProp != null && _applySortingOrderProp.boolValue;
            _showSortingOrder = new AnimBool(initialSorting);
            _showSortingOrder.valueChanged.AddListener(Repaint);

            var spinnerType = GetCurrentSpinnerType();
            _showRotateOptions = new AnimBool(spinnerType == LoadingScreen.SpinnerType.Rotate);
            _showRotateOptions.valueChanged.AddListener(Repaint);

            _showDotsOptions = new AnimBool(spinnerType == LoadingScreen.SpinnerType.Dots);
            _showDotsOptions.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showSortingOrder?.valueChanged.RemoveListener(Repaint);
            _showRotateOptions?.valueChanged.RemoveListener(Repaint);
            _showDotsOptions?.valueChanged.RemoveListener(Repaint);
        }

        private LoadingScreen.SpinnerType GetCurrentSpinnerType()
        {
            if (_spinnerTypeProp == null || _spinnerTypeProp.hasMultipleDifferentValues)
            {
                return (LoadingScreen.SpinnerType)(-1);
            }
            return (LoadingScreen.SpinnerType)_spinnerTypeProp.enumValueIndex;
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

            var spinnerType = GetCurrentSpinnerType();

            // AnimBool の目標値を更新
            _showSortingOrder.target = _applySortingOrderProp != null &&
                (!_applySortingOrderProp.hasMultipleDifferentValues && _applySortingOrderProp.boolValue);
            _showRotateOptions.target = spinnerType == LoadingScreen.SpinnerType.Rotate;
            _showDotsOptions.target = spinnerType == LoadingScreen.SpinnerType.Dots;

            // Targets
            if (_canvasGroupProp != null)
            {
                EditorGUILayout.PropertyField(_canvasGroupProp);
            }

            // Spinner ターゲット（Rotate 時のみ滑らかに開閉）
            if (EditorGUILayout.BeginFadeGroup(_showRotateOptions.faded))
            {
                if (_spinnerProp != null)
                {
                    EditorGUILayout.PropertyField(_spinnerProp);
                }
            }
            EditorGUILayout.EndFadeGroup();

            // Fade
            if (_showMotionProp != null) EditorGUILayout.PropertyField(_showMotionProp);
            if (_hideMotionProp != null) EditorGUILayout.PropertyField(_hideMotionProp);
            if (_hideModeProp != null) EditorGUILayout.PropertyField(_hideModeProp);

            // Timing
            if (_minDisplayTimeProp != null) EditorGUILayout.PropertyField(_minDisplayTimeProp);
            if (_waitFramesAfterLoadProp != null) EditorGUILayout.PropertyField(_waitFramesAfterLoadProp);

            // Scene / Canvas
            if (_persistAcrossScenesProp != null) EditorGUILayout.PropertyField(_persistAcrossScenesProp);
            if (_applySortingOrderProp != null)
            {
                EditorGUILayout.PropertyField(_applySortingOrderProp);
            }

            // ApplySortingOrder 連動の SortingOrder（滑らかなアニメーション開閉）
            if (EditorGUILayout.BeginFadeGroup(_showSortingOrder.faded))
            {
                EditorGUI.indentLevel++;
                if (_sortingOrderProp != null)
                {
                    EditorGUILayout.PropertyField(_sortingOrderProp);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_initialVisibleProp != null) EditorGUILayout.PropertyField(_initialVisibleProp);

            // Spinner
            if (_spinnerTypeProp != null)
            {
                EditorGUILayout.PropertyField(_spinnerTypeProp);
            }

            // Rotate 設定（滑らかなアニメーション開閉）
            if (EditorGUILayout.BeginFadeGroup(_showRotateOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_rotateSpeedProp != null) EditorGUILayout.PropertyField(_rotateSpeedProp);
                if (_rotateClockwiseProp != null) EditorGUILayout.PropertyField(_rotateClockwiseProp);
                if (_rotateStepCountProp != null) EditorGUILayout.PropertyField(_rotateStepCountProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Dots 設定（滑らかなアニメーション開閉）
            if (EditorGUILayout.BeginFadeGroup(_showDotsOptions.faded))
            {
                EditorGUI.indentLevel++;
                if (_dotsProp != null) EditorGUILayout.PropertyField(_dotsProp, true);
                if (_dotsCycleDurationProp != null) EditorGUILayout.PropertyField(_dotsCycleDurationProp);
                if (_dotsPhaseStepProp != null) EditorGUILayout.PropertyField(_dotsPhaseStepProp);
                if (_dotsMinScaleProp != null) EditorGUILayout.PropertyField(_dotsMinScaleProp);
                if (_dotsMaxScaleProp != null) EditorGUILayout.PropertyField(_dotsMaxScaleProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            // Hisui Common
            if (_ignoreTimeScaleProp != null)
            {
                EditorGUILayout.PropertyField(_ignoreTimeScaleProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
