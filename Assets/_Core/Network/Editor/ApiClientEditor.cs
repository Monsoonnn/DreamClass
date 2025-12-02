#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DreamClass.Network
{
    [CustomEditor(typeof(ApiClient))]
    public class ApiClientEditor : Editor
    {
        private SerializedProperty baseUrlProp;
        private SerializedProperty authDataProp;

        private void OnEnable()
        {
            baseUrlProp = serializedObject.FindProperty("baseUrl");
            authDataProp = serializedObject.FindProperty("authData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ApiClient apiClient = (ApiClient)target;

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("API Client Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Server Configuration
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(baseUrlProp, new GUIContent("Base URL"));
            EditorGUILayout.Space();

            // Authentication Data
            EditorGUILayout.LabelField("Authentication", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(authDataProp, new GUIContent("Auth Data"));

            if (authDataProp.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("⚠️ AuthData ScriptableObject is not assigned! Create one via: Assets > Create > DreamClass > Auth Data", MessageType.Warning);
            }

            AuthData authData = authDataProp.objectReferenceValue as AuthData;
            if (authData != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Auth Type: {authData.AuthType}", EditorStyles.miniLabel);

                // Show current auth status
                switch (authData.AuthType)
                {
                    case AuthType.Cookie:
                        EditorGUILayout.HelpBox("Using Cookie-based authentication", MessageType.Info);

                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

                            bool isAuth = authData.IsAuthenticated();
                            EditorGUILayout.LabelField("Authenticated:", isAuth ? "✓ Yes" : "✗ No");

                            if (!string.IsNullOrEmpty(authData.Cookie))
                            {
                                EditorGUILayout.LabelField("Cookie Value:", authData.Cookie);
                            }
                        }
                        break;

                    case AuthType.JWT:
                        EditorGUILayout.HelpBox("Using JWT (JSON Web Token) authentication", MessageType.Info);

                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);

                            bool isAuth = authData.IsAuthenticated();
                            EditorGUILayout.LabelField("Authenticated:", isAuth ? "✓ Yes" : "✗ No");

                            if (!string.IsNullOrEmpty(authData.JwtToken))
                            {
                                // Show truncated token for security
                                string tokenPreview = authData.JwtToken.Length > 20
                                    ? authData.JwtToken.Substring(0, 20) + "..."
                                    : authData.JwtToken;
                                EditorGUILayout.LabelField("Token Preview:", tokenPreview);
                            }
                        }
                        break;
                }

                // Runtime buttons
                if (Application.isPlaying && authData != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Runtime Actions", EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Clear Auth"))
                    {
                        apiClient.ClearAuth();
                        Debug.Log("[ApiClient] All authentication cleared from ScriptableObject");
                    }

                    if (authData.AuthType == AuthType.Cookie && GUILayout.Button("Clear Cookie"))
                    {
                        apiClient.ClearCookie();
                    }

                    if (authData.AuthType == AuthType.JWT && GUILayout.Button("Clear JWT"))
                    {
                        apiClient.ClearJwtToken();
                    }

                    EditorGUILayout.EndHorizontal();
                }

                serializedObject.ApplyModifiedProperties();
            }
        }
    }
#endif
}
