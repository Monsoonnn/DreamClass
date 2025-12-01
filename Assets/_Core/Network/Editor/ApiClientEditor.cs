#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DreamClass.Network
{
    [CustomEditor(typeof(ApiClient))]
    public class ApiClientEditor : Editor
    {
        private SerializedProperty baseUrlProp;
        private SerializedProperty authTypeProp;
        private SerializedProperty defaultCookieProp;
        private SerializedProperty jwtTokenProp;

        private void OnEnable()
        {
            baseUrlProp = serializedObject.FindProperty("baseUrl");
            authTypeProp = serializedObject.FindProperty("authType");
            defaultCookieProp = serializedObject.FindProperty("defaultCookie");
            jwtTokenProp = serializedObject.FindProperty("jwtToken");
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

            // Authentication Type
            EditorGUILayout.LabelField("Authentication", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(authTypeProp, new GUIContent("Auth Type"));

            AuthType currentAuthType = (AuthType)authTypeProp.enumValueIndex;

            EditorGUILayout.Space();

            // Show fields based on auth type
            switch (currentAuthType)
            {
                case AuthType.Cookie:
                    EditorGUILayout.HelpBox("Using Cookie-based authentication", MessageType.Info);
                    EditorGUILayout.PropertyField(defaultCookieProp, new GUIContent("Cookie"));
                    
                    // Runtime info
                    if (Application.isPlaying)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                        
                        bool isAuth = apiClient.IsAuthenticated();
                        EditorGUILayout.LabelField("Authenticated:", isAuth ? "✓ Yes" : "✗ No");
                        
                        if (!string.IsNullOrEmpty(apiClient.DefaultCookie))
                        {
                            EditorGUILayout.LabelField("Cookie Value:", apiClient.DefaultCookie);
                        }
                    }
                    break;

                case AuthType.JWT:
                    EditorGUILayout.HelpBox("Using JWT (JSON Web Token) authentication", MessageType.Info);
                    EditorGUILayout.PropertyField(jwtTokenProp, new GUIContent("JWT Token"));
                    
                    // Runtime info
                    if (Application.isPlaying)
                    {
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                        
                        bool isAuth = apiClient.IsAuthenticated();
                        EditorGUILayout.LabelField("Authenticated:", isAuth ? "✓ Yes" : "✗ No");
                        
                        if (!string.IsNullOrEmpty(apiClient.JwtToken))
                        {
                            // Show truncated token for security
                            string tokenPreview = apiClient.JwtToken.Length > 20 
                                ? apiClient.JwtToken.Substring(0, 20) + "..." 
                                : apiClient.JwtToken;
                            EditorGUILayout.LabelField("Token Preview:", tokenPreview);
                        }
                    }
                    break;
            }

            // Runtime buttons
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Actions", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Clear Auth"))
                {
                    apiClient.ClearAuth();
                    Debug.Log("[ApiClient] All authentication cleared");
                }
                
                if (currentAuthType == AuthType.Cookie && GUILayout.Button("Clear Cookie"))
                {
                    apiClient.ClearCookie();
                }
                
                if (currentAuthType == AuthType.JWT && GUILayout.Button("Clear JWT"))
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
