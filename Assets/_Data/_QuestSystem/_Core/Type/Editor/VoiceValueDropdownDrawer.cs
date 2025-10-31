#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DreamClass.QuestSystem {
    [CustomPropertyDrawer(typeof(VoiceValueDropdownAttribute))]
    public class VoiceValueDropdownDrawer : PropertyDrawer {
        public override void OnGUI( Rect position, SerializedProperty property, GUIContent label ) {
            SerializedProperty voiceEnumSourceProp =
                property.serializedObject.FindProperty("voiceEnumSource");

            EditorGUI.BeginProperty(position, label, property);

            if (voiceEnumSourceProp != null && voiceEnumSourceProp.objectReferenceValue is NPCCore.VoiceEnumSource source) {
                var values = source.values;

                if (values != null && values.Length > 0) {
                    int currentIndex = Mathf.Max(0, System.Array.IndexOf(values, property.stringValue));
                    int newIndex = EditorGUI.Popup(position, label.text, currentIndex, values);

                    if (newIndex >= 0 && newIndex < values.Length) {
                        property.stringValue = values[newIndex];
                    }
                } else {
                    EditorGUI.HelpBox(position, "VoiceEnumSource has no values.", MessageType.Warning);
                }
            } else {
                EditorGUI.HelpBox(position, "Assign a VoiceEnumSource first.", MessageType.Info);
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
