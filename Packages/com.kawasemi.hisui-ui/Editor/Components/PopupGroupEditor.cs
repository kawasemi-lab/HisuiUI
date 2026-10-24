using Hisui.UI.Components;
using Hisui.UI.Core;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="PopupGroup"/> 用のカスタムインスペクタ。
    /// <para>
    /// ・子階層の <see cref="Popup"/> を検索して一括登録するボタンを提供。<br/>
    /// ・設計書セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> で条件付き項目を滑らかに開閉する。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(PopupGroup))]
    [CanEditMultipleObjects]
    public sealed class PopupGroupEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _popupsProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _inStaggerIntervalProp;
        private SerializedProperty _inStaggerOrderProp;
        private SerializedProperty _outStaggerIntervalProp;
        private SerializedProperty _outStaggerOrderProp;
        private SerializedProperty _startDelayProp;
        private SerializedProperty _autoOutModeProp;
        private SerializedProperty _autoOutDelayProp;
        private SerializedProperty _deactivateGroupOnAllClosedProp;

        private AnimBool _showStartDelay;
        private AnimBool _showAutoOutDelay;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _popupsProp = serializedObject.FindProperty("_popups");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _inStaggerIntervalProp = _settingsProp.FindPropertyRelative("inStaggerInterval");
                _inStaggerOrderProp = _settingsProp.FindPropertyRelative("inStaggerOrder");
                _outStaggerIntervalProp = _settingsProp.FindPropertyRelative("outStaggerInterval");
                _outStaggerOrderProp = _settingsProp.FindPropertyRelative("outStaggerOrder");
                _startDelayProp = _settingsProp.FindPropertyRelative("startDelay");
                _autoOutModeProp = _settingsProp.FindPropertyRelative("autoOutMode");
                _autoOutDelayProp = _settingsProp.FindPropertyRelative("autoOutDelay");
                _deactivateGroupOnAllClosedProp = _settingsProp.FindPropertyRelative("deactivateGroupOnAllClosed");
            }

            _showStartDelay = new AnimBool(GetCurrentPlayTrigger() == PlayTrigger.OnEnable);
            _showStartDelay.valueChanged.AddListener(Repaint);

            _showAutoOutDelay = new AnimBool(ShouldShowAutoOutDelay(GetCurrentAutoOutMode()));
            _showAutoOutDelay.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showStartDelay?.valueChanged.RemoveListener(Repaint);
            _showAutoOutDelay?.valueChanged.RemoveListener(Repaint);
        }

        private PlayTrigger GetCurrentPlayTrigger()
        {
            if (_playTriggerProp == null || _playTriggerProp.hasMultipleDifferentValues)
            {
                return (PlayTrigger)(-1);
            }
            return (PlayTrigger)_playTriggerProp.enumValueIndex;
        }

        private PopupGroup.GroupAutoOutMode GetCurrentAutoOutMode()
        {
            if (_autoOutModeProp == null || _autoOutModeProp.hasMultipleDifferentValues)
            {
                return (PopupGroup.GroupAutoOutMode)(-1);
            }
            return (PopupGroup.GroupAutoOutMode)_autoOutModeProp.enumValueIndex;
        }

        private static bool ShouldShowAutoOutDelay(PopupGroup.GroupAutoOutMode mode)
        {
            return mode == PopupGroup.GroupAutoOutMode.EachPopupOnInComplete
                || mode == PopupGroup.GroupAutoOutMode.AllAfterLastInComplete;
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

            var playTrigger = GetCurrentPlayTrigger();
            var autoOutMode = GetCurrentAutoOutMode();

            _showStartDelay.target = playTrigger == PlayTrigger.OnEnable;
            _showAutoOutDelay.target = ShouldShowAutoOutDelay(autoOutMode);

            // Target Popups リスト & 子階層検索ボタン
            if (_popupsProp != null)
            {
                EditorGUILayout.PropertyField(_popupsProp, true);
            }

            EditorGUILayout.Space(2f);
            if (GUILayout.Button("子階層の Popup を検索して登録 (Collect Popups in Children)", GUILayout.Height(24f)))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var t in targets)
                {
                    if (t is PopupGroup group)
                    {
                        Undo.RecordObject(group, "Collect Popups in Children");
                        group.CollectChildPopups(includeInactive: true);
                        EditorUtility.SetDirty(group);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                    }
                }
                serializedObject.Update();
            }
            EditorGUILayout.Space(4f);

            // In Stagger
            if (_inStaggerIntervalProp != null) EditorGUILayout.PropertyField(_inStaggerIntervalProp);
            if (_inStaggerOrderProp != null) EditorGUILayout.PropertyField(_inStaggerOrderProp);

            // Out Stagger
            if (_outStaggerIntervalProp != null) EditorGUILayout.PropertyField(_outStaggerIntervalProp);
            if (_outStaggerOrderProp != null) EditorGUILayout.PropertyField(_outStaggerOrderProp);

            // Initial & Lifecycle
            if (EditorGUILayout.BeginFadeGroup(_showStartDelay.faded))
            {
                EditorGUI.indentLevel++;
                if (_startDelayProp != null) EditorGUILayout.PropertyField(_startDelayProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_autoOutModeProp != null) EditorGUILayout.PropertyField(_autoOutModeProp);

            if (EditorGUILayout.BeginFadeGroup(_showAutoOutDelay.faded))
            {
                EditorGUI.indentLevel++;
                if (_autoOutDelayProp != null) EditorGUILayout.PropertyField(_autoOutDelayProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_deactivateGroupOnAllClosedProp != null) EditorGUILayout.PropertyField(_deactivateGroupOnAllClosedProp);

            // Hisui Common
            if (_playTriggerProp != null) EditorGUILayout.PropertyField(_playTriggerProp);
            if (_ignoreTimeScaleProp != null) EditorGUILayout.PropertyField(_ignoreTimeScaleProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
