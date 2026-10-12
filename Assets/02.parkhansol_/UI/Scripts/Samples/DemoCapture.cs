using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [v0.7 · 시연 영상용] 게임 화면 + 유니티 소리만 녹화 (마이크·다른 프로그램 소리 없음).
    ///   화면: 매 프레임 끝에 Game 뷰를 JPG로 (인코딩은 백그라운드 스레드), 시각과 함께 path.frames에 기록
    ///   소리: AudioListener 출력(OnAudioFilterRead)을 path.wav로
    /// 결과물은 OZ > UI > Record Demo Videos가 mp4로 합친다. 프로젝트 Temp 폴더에 저장 (깃 제외).
    /// </summary>
    [AddComponentMenu("")]
    public class DemoCapture : MonoBehaviour
    {
        string _path;
        FileStream _frames;
        BinaryWriter _writer;
        readonly object _lock = new object();
        readonly List<float> _audio = new List<float>(48000 * 2 * 30);
        readonly List<Task> _pending = new List<Task>();
        volatile bool _rec;
        int _channels = 2;
        int _sampleRate;
        double _t0;

        public int FrameCount { get; private set; }

        public static DemoCapture Begin(string path)
        {
            var listener = FindAnyObjectByType<AudioListener>();
            var go = listener != null ? listener.gameObject : new GameObject("DemoCapture");
            var c = go.AddComponent<DemoCapture>();
            c.Init(path);
            return c;
        }

        void Init(string path)
        {
            _path = path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _frames = new FileStream(path + ".frames", FileMode.Create, FileAccess.Write);
            _writer = new BinaryWriter(_frames);
            _sampleRate = AudioSettings.outputSampleRate;
            _t0 = Time.realtimeSinceStartupAsDouble;
            _rec = true;
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            var eof = new WaitForEndOfFrame();
            while (_rec)
            {
                yield return eof;
                if (!_rec) break;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                int w = tex.width, h = tex.height;
                var fmt = tex.graphicsFormat;
                byte[] raw = tex.GetRawTextureData<byte>().ToArray();
                Destroy(tex);
                double t = Time.realtimeSinceStartupAsDouble - _t0;
                FrameCount++;
                _pending.Add(Task.Run(() =>
                {
                    byte[] jpg = ImageConversion.EncodeArrayToJPG(raw, fmt, (uint)w, (uint)h, 0, 88);
                    lock (_writer)
                    {
                        _writer.Write(t);
                        _writer.Write(jpg.Length);
                        _writer.Write(jpg);
                    }
                }));
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_rec) return;
            lock (_lock)
            {
                _channels = channels;
                _audio.AddRange(data);
            }
        }

        /// <summary>녹화 끝: 프레임 파일 닫고 소리를 wav로 저장한 뒤 컴포넌트 제거</summary>
        public void End()
        {
            _rec = false;
            Task.WaitAll(_pending.ToArray());
            _writer?.Flush();
            _writer?.Dispose();
            _frames?.Dispose();
            float[] samples;
            int ch;
            lock (_lock) { samples = _audio.ToArray(); ch = _channels; }
            WriteWav(_path + ".wav", samples, ch, _sampleRate);
            Destroy(this);
        }

        static void WriteWav(string path, float[] samples, int channels, int rate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)channels);
                w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
        }
    }
}
