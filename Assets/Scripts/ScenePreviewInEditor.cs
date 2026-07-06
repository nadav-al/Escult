using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement; // Required for SceneManager

[InitializeOnLoad]
public class SceneCameraPreview : EditorWindow
{
    private static Texture2D previewTexture; // Texture for rendering the scene camera preview
    private static Camera previewCamera; // Camera for rendering the scene camera preview
    private static bool isPreviewing; // Flag to toggle the scene camera preview

    [MenuItem("Window/Scene Camera Preview")]
    private static void Init()
    {
        SceneCameraPreview window = GetWindow<SceneCameraPreview>("Scene Camera Preview");
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += Update; // Register the Update method to be called in every editor frame
    }

    private void OnDisable()
    {
        EditorApplication.update -= Update; // Unregister the Update method
    }

    private void Update()
    {
        if (isPreviewing)
        {
            Repaint(); // Trigger a repaint of the editor window to continuously update the preview
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Begin a vertical group with a help box style

        EditorGUI.indentLevel++; // Increase the indent level for better readability

        isPreviewing = EditorGUILayout.ToggleLeft("Scene Camera Preview", isPreviewing); // Toggle for enabling/disabling the scene camera preview

        if (isPreviewing)
        {
            if (GUILayout.Button("Update Preview")) // Button to update the preview
            {
                UpdatePreview();
            }

            if (previewTexture != null) // Show the preview texture if available
            {
                Rect previewRect = GUILayoutUtility.GetAspectRect(1f); // Get a square rect for the preview
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture); // Draw the preview texture
            }
        }

        EditorGUI.indentLevel--; // Decrease the indent level

        EditorGUILayout.EndVertical(); // End the vertical group
    }

    private void UpdatePreview()
    {
        SceneView sceneView = SceneView.lastActiveSceneView; // Get the active scene view
        if (sceneView != null && sceneView.camera != null)
        {
            Camera sceneCamera = sceneView.camera; // Get the camera from the active scene view
            string sceneName = SceneManager.GetActiveScene().name; // Get the name of the active scene using SceneManager

            // Render the scene view to a texture
            RenderTexture renderTexture = new RenderTexture(256, 256, 24); // Create a new render texture for rendering the preview
            sceneCamera.targetTexture = renderTexture; // Set the render texture as the target texture for the camera
            sceneCamera.Render(); // Render the scene view to the render texture
            RenderTexture.active = renderTexture; // Set the render texture as the active render texture
            previewTexture = new Texture2D(256, 256); // Create a new texture for storing the rendered image
            previewTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); // Read the pixels from the render texture into the texture
            previewTexture.Apply(); // Apply the changes to the texture
            sceneCamera.targetTexture = null; // Reset the target texture of the camera
            RenderTexture.active = null; // Reset the active render texture

            // Clean up the temporary render texture
            DestroyImmediate(renderTexture); // Destroy the temporary render texture to avoid memory leaks
        }
    }
}
