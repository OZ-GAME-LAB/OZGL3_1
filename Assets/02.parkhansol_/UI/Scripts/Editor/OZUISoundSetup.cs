using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// UI 효과음 세팅: UI/Audio/UISoundSet_Default.asset 생성 (비어 있는 항목만 채움 → 손으로 바꾼 건 유지).
    /// 소리: Kenney "Interface Sounds" (CC0, www.kenney.nl) — 요즘 휴대폰 UI처럼 짧고 맑은 소리 위주로 골라 현대 판타지 "시스템 창" 톤에 맞춤.
    /// 볼륨은 소리마다 체감 크기(RMS)를 맞춘 값.
    /// </summary>
    internal static class OZUISoundSetup
    {
        public const string Folder = OZPaths.UI + "/Audio";
        public const string ClipFolder = Folder + "/KenneyInterface";
        public const string SetPath = Folder + "/UISoundSet_Default.asset";

        // (UISound, 파일, 볼륨, 연타 방지 초, 음높이 흔들림)
        static readonly (string sound, string file, float vol, float cooldown, float jitter)[] Table =
        {
            ("Hover", "select_001", 0.42f, 0.06f, 0.04f),
            ("Click", "click_003", 0.62f, 0.04f, 0.04f),
            ("Confirm", "confirmation_003", 0.53f, 0.04f, 0.0f),
            ("Back", "back_002", 0.51f, 0.04f, 0.0f),
            ("Tab", "toggle_004", 0.41f, 0.04f, 0.0f),
            ("Slider", "tick_004", 0.24f, 0.05f, 0.06f),
            ("Error", "error_004", 0.51f, 0.12f, 0.0f),
            ("Open", "maximize_007", 0.35f, 0.04f, 0.0f),
            ("Close", "minimize_007", 0.31f, 0.04f, 0.0f),
            ("SystemAlarm", "glass_001", 1.08f, 0.25f, 0.0f),
            ("RankUp", "confirmation_002", 0.63f, 0.04f, 0.0f),
            ("QuestUpdate", "glass_005", 0.76f, 0.15f, 0.03f),
            ("QuestComplete", "confirmation_004", 0.38f, 0.04f, 0.0f),
            ("Toast", "drop_001", 0.74f, 0.1f, 0.03f),
            ("ToastWarning", "question_003", 0.25f, 0.04f, 0.0f),
            ("GateSealed", "maximize_009", 0.36f, 0.04f, 0.0f),
            ("StageIntro", "maximize_006", 0.54f, 0.04f, 0.0f),
            ("LoadDone", "glass_006", 0.73f, 0.04f, 0.0f),
            ("BossAppear", "maximize_008", 0.53f, 0.04f, 0.0f),
            ("BossDefeat", "bong_001", 0.79f, 0.04f, 0.0f),
            ("Death", "minimize_008", 0.47f, 0.04f, 0.0f),
            ("Unlock", "select_004", 0.89f, 0.04f, 0.0f),
            ("Reset", "drop_004", 0.93f, 0.04f, 0.0f),
            ("ItemPick", "select_002", 0.5f, 0.04f, 0.04f),
            ("ItemPlace", "drop_002", 0.72f, 0.04f, 0.04f),
            ("ItemUse", "glass_002", 0.71f, 0.04f, 0.0f),
            ("SkillReady", "glass_003", 0.26f, 0.1f, 0.0f),
            ("DialogueNext", "click_005", 0.32f, 0.04f, 0.04f),
        };

        [MenuItem("OZ/UI/Setup UI Sounds", priority = 8)]
        public static UISoundSet Run()
        {
            OZFontBuilder.EnsureFolder(Folder);
            var set = AssetDatabase.LoadAssetAtPath<UISoundSet>(SetPath);
            bool created = set == null;
            if (created)
            {
                set = ScriptableObject.CreateInstance<UISoundSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var list = new List<UISoundSet.Entry>(set.entries ?? new UISoundSet.Entry[0]);
            int added = 0;
            foreach (var t in Table)
            {
                var id = (UISound)Enum.Parse(typeof(UISound), t.sound);
                var e = list.Find(x => x != null && x.sound == id);
                if (e != null && e.clips != null && e.clips.Length > 0 && e.clips[0] != null) continue; // 손댄 항목 유지
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ClipFolder}/{t.file}.wav");
                if (clip == null) { Debug.LogWarning("[OZ UI] 효과음 없음: " + t.file); continue; }
                if (e == null) { e = new UISoundSet.Entry { sound = id }; list.Add(e); }
                e.clips = new[] { clip };
                e.volume = t.vol;
                e.cooldown = t.cooldown;
                e.pitchJitter = t.jitter;
                e.note = "Kenney Interface Sounds (CC0) / " + t.file;
                added++;
            }
            list.Sort((a, b) => ((int)a.sound).CompareTo((int)b.sound));
            set.entries = list.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[OZ UI] UI 효과음 세팅 → {SetPath} (새로 채운 항목 {added}개)", set);
            return set;
        }
    }

    /// <summary>UI 효과음 가져오기 설정: 모노 · 메모리에 풀어 둠(짧은 소리라 지연 최소) · PCM</summary>
    internal class OZAudioImportProcessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(OZUISoundSetup.Folder)) return;
            var imp = (AudioImporter)assetImporter;
            imp.forceToMono = true;
            imp.loadInBackground = false;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
        }
    }
}
