using Hisui.UI.Palette;
using UnityEditor;
using UnityEngine;

namespace Hisui.UI.Editor
{
    /// <summary>
    /// <see cref="HisuiColor"/> を Inspector 上で 1 行（モード切替 + ColorPicker または uPalette エントリ選択）で描画する PropertyDrawer。
    /// </summary>
    [CustomPropertyDrawer(typeof(HisuiColor))]
    public sealed class HisuiColorDrawer : PropertyDrawer
    {
        private const float ModeWidth = 78f;
        private const float Spacing = 4f;

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var contentRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var modeRect = new Rect(contentRect.x, contentRect.y, ModeWidth, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(
                contentRect.x + ModeWidth + Spacing,
                contentRect.y,
                Mathf.Max(0f, contentRect.width - ModeWidth - Spacing),
                EditorGUIUtility.singleLineHeight);

            var modeProp = property.FindPropertyRelative(nameof(HisuiColor.mode));
            var directColorProp = property.FindPropertyRelative(nameof(HisuiColor.directColor));
            var entryIdProp = property.FindPropertyRelative("_entryId");

            EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

            var mode = (ColorSourceMode)modeProp.enumValueIndex;
            if (mode == ColorSourceMode.Palette)
            {
                // uPalette 標準の ColorEntryIdDrawer に委譲してドロップダウンを描画する
                EditorGUI.PropertyField(valueRect, entryIdProp, GUIContent.none);
            }
            else
            {
                EditorGUI.PropertyField(valueRect, directColorProp, GUIContent.none);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
