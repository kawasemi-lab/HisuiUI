using Hisui.UI.Components;
using LitMotion;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="Typewriter"/> 用のカスタムインスペクタ。
    /// <para>
    /// 設計ガイドライン セクション 3.6 に準拠し、<see cref="AnimBool"/> と <see cref="EditorGUILayout.BeginFadeGroup"/> による
    /// カスタムカーブ設定の滑らかな開閉表示、およびインスペクタ上からのプレビュー再生機能を提供します。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(Typewriter))]
    [CanEditMultipleObjects]
    public sealed class TypewriterEditor : UnityEditor.Editor
    {
        private SerializedProperty _scriptProp;
        private SerializedProperty _defaultTextProp;
        private SerializedProperty _playTriggerProp;
        private SerializedProperty _ignoreTimeScaleProp;

        private SerializedProperty _settingsProp;
        private SerializedProperty _charactersPerSecondProp;
        private SerializedProperty _startDelayProp;
        private SerializedProperty _easeProp;
        private SerializedProperty _customCurveProp;
        private SerializedProperty _preventJitterProp;

        private AnimBool _showCustomCurve;

        private void OnEnable()
        {
            _scriptProp = serializedObject.FindProperty("m_Script");
            _defaultTextProp = serializedObject.FindProperty("_defaultText");
            _playTriggerProp = serializedObject.FindProperty("_playTrigger");
            _ignoreTimeScaleProp = serializedObject.FindProperty("_ignoreTimeScale");

            _settingsProp = serializedObject.FindProperty("_settings");
            if (_settingsProp != null)
            {
                _charactersPerSecondProp = _settingsProp.FindPropertyRelative("charactersPerSecond");
                _startDelayProp = _settingsProp.FindPropertyRelative("startDelay");
                _easeProp = _settingsProp.FindPropertyRelative("ease");
                _customCurveProp = _settingsProp.FindPropertyRelative("customCurve");
                _preventJitterProp = _settingsProp.FindPropertyRelative("preventJitter");
            }

            bool isCustomCurve = _easeProp != null && _easeProp.enumValueIndex == (int)Ease.CustomAnimationCurve;
            _showCustomCurve = new AnimBool(isCustomCurve);
            _showCustomCurve.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            _showCustomCurve?.valueChanged.RemoveListener(Repaint);
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

            // Preview & Status Bar (単一選択時)
            if (!serializedObject.isEditingMultipleObjects && target is Typewriter typewriter)
            {
                DrawPreviewControls(typewriter);
            }

            // Text Source
            if (_defaultTextProp != null)
            {
                EditorGUILayout.PropertyField(_defaultTextProp);
            }

            // Settings Fields
            if (_charactersPerSecondProp != null) EditorGUILayout.PropertyField(_charactersPerSecondProp);
            if (_startDelayProp != null) EditorGUILayout.PropertyField(_startDelayProp);

            if (_easeProp != null)
            {
                EditorGUILayout.PropertyField(_easeProp);
                _showCustomCurve.target = _easeProp.enumValueIndex == (int)Ease.CustomAnimationCurve;
            }

            // Custom Curve (AnimBool + BeginFadeGroup で滑らかに開閉)
            if (EditorGUILayout.BeginFadeGroup(_showCustomCurve.faded))
            {
                EditorGUI.indentLevel++;
                if (_customCurveProp != null)
                {
                    EditorGUILayout.PropertyField(_customCurveProp);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();

            if (_preventJitterProp != null) EditorGUILayout.PropertyField(_preventJitterProp);

            // Hisui Common Settings
            if (_playTriggerProp != null) EditorGUILayout.PropertyField(_playTriggerProp);
            if (_ignoreTimeScaleProp != null) EditorGUILayout.PropertyField(_ignoreTimeScaleProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPreviewControls(Typewriter typewriter)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Typewriter Controls", EditorStyles.boldLabel);

                    string statusText = typewriter.IsPlaying
                        ? $"Playing ({typewriter.CurrentVisibleCharacters} / {typewriter.TotalCharacters})"
                        : $"Idle ({typewriter.CurrentVisibleCharacters} / {typewriter.TotalCharacters})";

                    EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel, GUILayout.Width(130));
                }

                if (typewriter.TotalCharacters > 0)
                {
                    float progress = (float)typewriter.CurrentVisibleCharacters / typewriter.TotalCharacters;
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 6), progress, string.Empty);
                    EditorGUILayout.Space(2);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(typewriter.IsPlaying ? "Restart" : "Play", EditorStyles.miniButtonLeft))
                    {
                        typewriter.Play();
                    }

                    using (new EditorGUI.DisabledScope(!typewriter.IsPlaying))
                    {
                        if (GUILayout.Button("Stop", EditorStyles.miniButtonMid))
                        {
                            typewriter.Stop();
                        }

                        if (GUILayout.Button("Skip", EditorStyles.miniButtonMid))
                        {
                            typewriter.Complete();
                        }
                    }

                    if (GUILayout.Button("Clear", EditorStyles.miniButtonRight))
                    {
                        typewriter.Clear();
                    }
                }
            }
            EditorGUILayout.Space(2);
        }
    }
}
