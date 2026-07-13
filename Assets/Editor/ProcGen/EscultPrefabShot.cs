using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Escult.ProcGen
{
    /// <summary>
    /// Editor-only utility: render a level prefab to a PNG with a framed orthographic camera,
    /// so tiles/decoration can be inspected and before/after-compared without entering play mode.
    /// </summary>
    public static class EscultPrefabShot
    {
        // Optional crop framing (world units); set via CaptureCrop.
        static bool useCrop; static Vector2 cropCenter, cropSize;
        public static string CaptureCrop(string prefabPath, string outPng, Vector2 center, Vector2 size, int pixelsPerCell = 90)
        {
            useCrop = true; cropCenter = center; cropSize = size;
            try { return Capture(prefabPath, outPng, pixelsPerCell); }
            finally { useCrop = false; }
        }

        public static string Capture(string prefabPath, string outPng, int pixelsPerCell = 40)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "prefab not found: " + prefabPath;

            var prevScene = SceneManager.GetActiveScene().path;
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);
            GameObject inst = null;
            Camera camGo = null;
            RenderTexture rt = null;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, temp);
                inst.SetActive(true);
                foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);

                // bounds over all tilemaps
                var maps = inst.GetComponentsInChildren<Tilemap>(true);
                Bounds b = new Bounds();
                bool first = true;
                foreach (var tm in maps)
                {
                    tm.CompressBounds();
                    var local = tm.localBounds;
                    var wc = tm.transform.TransformPoint(local.center);
                    var wb = new Bounds(wc, Vector3.Scale(local.size, tm.transform.lossyScale));
                    if (first) { b = wb; first = false; } else b.Encapsulate(wb);
                }
                if (first) return "no tilemaps to frame";
                if (useCrop) b = new Bounds(new Vector3(cropCenter.x, cropCenter.y, 0), new Vector3(cropSize.x, cropSize.y, 0));

                var camObj = new GameObject("ShotCam");
                camObj.transform.SetParent(temp == SceneManager.GetActiveScene() ? null : null);
                SceneManager.MoveGameObjectToScene(camObj, temp);
                camGo = camObj.AddComponent<Camera>();
                camGo.orthographic = true;
                float pad = 0.6f;
                float halfH = b.extents.y + pad;
                float halfW = b.extents.x + pad;
                camGo.transform.position = new Vector3(b.center.x, b.center.y, -10f);
                camGo.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
                camGo.clearFlags = CameraClearFlags.SolidColor;

                int w = Mathf.RoundToInt((b.size.x + pad * 2) * pixelsPerCell / 0.64f);
                int h = Mathf.RoundToInt((b.size.y + pad * 2) * pixelsPerCell / 0.64f);
                w = Mathf.Clamp(w, 64, 3000); h = Mathf.Clamp(h, 64, 3000);
                float aspect = (float)w / h;
                camGo.orthographicSize = Mathf.Max(halfH, halfW / aspect);

                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                camGo.targetTexture = rt;
                camGo.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;

                Directory.CreateDirectory(Path.GetDirectoryName(outPng));
                File.WriteAllBytes(outPng, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return $"captured {w}x{h} -> {outPng}";
            }
            finally
            {
                if (camGo != null) { camGo.targetTexture = null; Object.DestroyImmediate(camGo.gameObject); }
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (inst != null) Object.DestroyImmediate(inst);
                EditorSceneManager.CloseScene(temp, true);
            }
        }
    }
}
