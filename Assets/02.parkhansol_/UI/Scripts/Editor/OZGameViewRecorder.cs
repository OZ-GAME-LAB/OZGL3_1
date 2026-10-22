using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Record Game View (켜기/끄기)
    /// 플레이 중 Game 뷰를 초당 fps장 PNG로 저장 → 프로젝트/Recordings/yyMMdd_HHmmss/f0000.png
    /// (Assets 밖이라 임포트·깃 대상 아님). 플레이를 멈추면 자동으로 꺼진다.
    /// 영상으로: ffmpeg -framerate 15 -i f%04d.png -pix_fmt yuv420p out.mp4
    /// </summary>
    internal static class OZGameViewRecorder
    {
        const string MenuPath = "OZ/UI/Record Game View (toggle)";
        const float Fps = 15f;
        static string _dir;
        static int _frame;
        static double _next;

        internal static bool Recording => _dir != null;
        internal static string Folder => _dir;

        [MenuItem(MenuPath, priority = 60)]
        static void Toggle() { if (Recording) Stop(); else Start(); }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate() { Menu.SetChecked(MenuPath, Recording); return true; }

        internal static void Start()
        {
            if (Recording) return;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _dir = Path.Combine(root, "Recordings", DateTime.Now.ToString("yyMMdd_HHmmss"));
            Directory.CreateDirectory(_dir);
            _frame = 0;
            _next = 0;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
            Debug.Log("[OZ Rec] 녹화 시작 → " + _dir);
        }

        internal static void Stop()
        {
            if (!Recording) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            Debug.Log($"[OZ Rec] 녹화 끝 ({_frame}장) → {_dir}");
            _dir = null;
        }

        static void OnPlayMode(PlayModeStateChange s) { if (s == PlayModeStateChange.ExitingPlayMode) Stop(); }

        /// <summary>
        /// OZ > UI > Encode Last Recording → 가장 최근 Recordings/폴더의 PNG를 960x540 MP4로 묶는다 (Recordings/폴더이름.mp4)
        /// fps는 실제 찍힌 간격(파일 시각)으로 계산 → 재생 속도가 실제와 같다.
        /// </summary>
        [MenuItem("OZ/UI/Encode Last Recording (mp4)", priority = 61)]
        internal static void EncodeLast()
        {
            string root = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Recordings");
            if (!Directory.Exists(root)) { Debug.LogWarning("[OZ Rec] Recordings 폴더 없음"); return; }
            var dirs = Directory.GetDirectories(root);
            if (dirs.Length == 0) { Debug.LogWarning("[OZ Rec] 녹화 없음"); return; }
            Array.Sort(dirs, StringComparer.Ordinal);
            Encode(dirs[dirs.Length - 1], 960, 540);
        }

        internal static string Encode(string dir, int width, int height)
        {
            var files = Directory.GetFiles(dir, "f*.png");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length < 2) { Debug.LogWarning("[OZ Rec] 프레임이 부족함: " + dir); return null; }
            double secs = (File.GetLastWriteTimeUtc(files[files.Length - 1]) - File.GetLastWriteTimeUtc(files[0])).TotalSeconds;
            int fps = Mathf.Clamp(Mathf.RoundToInt((float)((files.Length - 1) / Math.Max(0.1, secs))), 5, 30);
            string outPath = dir.TrimEnd('\\', '/') + ".mp4";
            if (File.Exists(outPath)) File.Delete(outPath);

            var attr = new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(fps),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
            };
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var dst = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            try
            {
                using (var enc = new UnityEditor.Media.MediaEncoder(outPath, attr))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        if (i % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("OZ Rec", $"mp4 만드는 중 {i}/{files.Length}", (float)i / files.Length)) break;
                        if (!src.LoadImage(File.ReadAllBytes(files[i]))) continue;
                        Graphics.Blit(src, rt);
                        RenderTexture.active = rt;
                        dst.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        dst.Apply();
                        enc.AddFrame(dst);
                    }
                }
                Debug.Log($"[OZ Rec] mp4 완료 ({files.Length}장, {fps}fps) → {outPath}");
                return outPath;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(src);
                UnityEngine.Object.DestroyImmediate(dst);
                EditorUtility.ClearProgressBar();
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _next) return;
            _next = now + 1.0 / Fps;
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, $"f{_frame:0000}.png"));
            _frame++;
        }
    }
}
