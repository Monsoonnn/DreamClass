using UnityEngine;
using UnityEditor;

namespace DreamClass.Account.Editor
{
    [CustomEditor(typeof(UserProfileSO))]
    public class UserProfileSOEditor : UnityEditor.Editor
    {
        private UserProfileSO profile;
        private bool showAvatarPreview = true;
        private float previewSize = 128f;

        private void OnEnable()
        {
            profile = (UserProfileSO)target;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Avatar Preview Section
            showAvatarPreview = EditorGUILayout.Foldout(showAvatarPreview, "Avatar Preview", true, EditorStyles.foldoutHeader);

            if (showAvatarPreview)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Preview size slider
                previewSize = EditorGUILayout.Slider("Preview Size", previewSize, 64f, 256f);

                EditorGUILayout.Space(5);

                // Avatar URL info
                if (!string.IsNullOrEmpty(profile.avatarUrl))
                {
                    EditorGUILayout.LabelField("URL:", profile.avatarUrl, EditorStyles.wordWrappedLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("No avatar URL set", MessageType.Info);
                }

                EditorGUILayout.Space(5);

                // Draw avatar preview
                DrawAvatarPreview();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Profile Status Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Profile Status", EditorStyles.boldLabel);

            if (profile.HasProfile)
            {
                EditorGUILayout.HelpBox($"✓ Profile loaded: {profile.userName} ({profile.playerId})", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("✗ No profile loaded", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            // Clear button (only in Play mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Clear Profile", GUILayout.Height(30)))
                {
                    profile.Clear();
                    Repaint();
                }

                EditorGUILayout.EndHorizontal();
            }

            // Repaint if playing to show runtime changes
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void DrawAvatarPreview()
        {
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
            previewRect.x = (EditorGUIUtility.currentViewWidth - previewSize) / 2f;

            // Draw background
            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 1f));

            if (profile.avatarTexture != null)
            {
                // Draw texture
                GUI.DrawTexture(previewRect, profile.avatarTexture, ScaleMode.ScaleToFit);

                // Texture info
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Size: {profile.avatarTexture.width} x {profile.avatarTexture.height}", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField($"Format: {profile.avatarTexture.format}", EditorStyles.centeredGreyMiniLabel);
            }
            else if (profile.avatarSprite != null && profile.avatarSprite.texture != null)
            {
                // Draw sprite texture
                GUI.DrawTexture(previewRect, profile.avatarSprite.texture, ScaleMode.ScaleToFit);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Sprite: {profile.avatarSprite.name}", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Draw placeholder
                GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                };

                GUI.Label(previewRect, "No Avatar", centeredStyle);

                if (Application.isPlaying && !string.IsNullOrEmpty(profile.avatarUrl))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("Avatar is loading or failed to load...", MessageType.Info);
                }
            }
        }

        // Preview icon in project window
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            if (profile != null && profile.avatarTexture != null)
            {
                Texture2D preview = new Texture2D(width, height);
                EditorUtility.CopySerialized(profile.avatarTexture, preview);
                return preview;
            }

            return base.RenderStaticPreview(assetPath, subAssets, width, height);
        }
    }
}
