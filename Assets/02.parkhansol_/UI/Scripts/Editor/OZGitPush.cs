using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Git Push (02.parkhansol_ 만)
    /// 규칙: 02.parkhansol_ 폴더만 커밋 (팀원 폴더·ProjectSettings 제외), 메시지 "parkhansol_내용".
    /// 순서: add → commit → pull --rebase --autostash → push. 로그: 프로젝트/Logs/OZ_GitPush.txt
    ///
    /// OZ > UI > Git Push + main (MainScene 포함): 위 + Assets/Scenes/MainScene.unity 도 커밋하고,
    ///   origin/main이 내 브랜치의 조상일 때만(fast-forward) main에도 올린다. 브랜치 전환 없음 → 열린 Unity 파일이 바뀌지 않음.
    ///   팀원이 main에 먼저 올린 게 있으면 main 푸시는 멈추고(브랜치만 올라감) 로그에 남긴다.
    ///
    /// v0.7: 메뉴를 누르면 커밋 메시지 입력 창이 뜬다 (전에는 메시지가 코드에 고정돼 있어 매번 같은 문구로 올라감).
    ///   마지막으로 쓴 메시지는 Logs/OZ_CommitMessage.txt에 남고 다음에 기본값으로 채워짐 (깃 제외 폴더).
    /// </summary>
    internal static class OZGitPush
    {
        internal const string MessagePrefix = "parkhansol_";
        internal static string MessageFile => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "OZ_CommitMessage.txt"));
        const string SampleScene = "Assets/Scenes/MainScene.unity";
        /// <summary>프로젝트 규칙: 하루 작업 = 브랜치 parkhansol_ui_yyMMdd (없으면 현재 브랜치에서 새로 만듦)</summary>
        const string BranchPrefix = "parkhansol_ui_";
        static bool _running;

        [MenuItem("OZ/UI/Git Push (02.parkhansol_ only)", priority = 40)]
        static void Push() => OZGitPushWindow.Open(false, "main");

        [MenuItem("OZ/UI/Git Push + main (MainScene 포함)", priority = 41)]
        static void PushMain() => OZGitPushWindow.Open(true, "main");

        [MenuItem("OZ/UI/Git Push + develop (MainScene 포함)", priority = 42)]
        static void PushDevelop() => OZGitPushWindow.Open(true, "develop");

        internal static string LastMessage()
        {
            try { if (File.Exists(MessageFile)) return File.ReadAllText(MessageFile).Trim(); } catch { }
            return MessagePrefix;
        }

        internal static void Push(bool toMain, string shared, string message)
        {
            message = (message ?? "").Trim();
            if (!message.StartsWith(MessagePrefix)) message = MessagePrefix + message;
            if (message == MessagePrefix) { Debug.LogError("[OZ Git] 커밋 메시지를 입력하세요."); return; }
            try { Directory.CreateDirectory(Path.GetDirectoryName(MessageFile)); File.WriteAllText(MessageFile, message, new UTF8Encoding(false)); } catch { }
            if (_running) { Debug.LogWarning("[OZ Git] 이미 실행 중입니다."); return; }
            AssetDatabase.SaveAssets();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string log = Path.Combine(root, "Logs", "OZ_GitPush.txt");
            _running = true;
            Debug.Log("[OZ Git] 푸시 시작… (결과: Logs/OZ_GitPush.txt)");
            Task.Run(() => Run(root, log, message, toMain, shared)).ContinueWith(t =>
            {
                _running = false;
                bool ok = t.Status == TaskStatus.RanToCompletion && t.Result;
                EditorApplication.delayCall += () =>
                {
                    if (ok) Debug.Log("[OZ Git] 푸시 완료 → " + log);
                    else Debug.LogError("[OZ Git] 푸시 실패 — " + log + " 확인");
                };
            });
        }

        static bool Run(string root, string logPath, string message, bool toMain, string shared)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OZ Git Push — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            bool ok = false;
            try
            {
                string git = FindGit();
                sb.AppendLine("git: " + git);

                Exec(git, "rev-parse --abbrev-ref HEAD", root, sb, out string branch);
                branch = branch.Trim();
                string today = BranchPrefix + DateTime.Now.ToString("yyMMdd");
                if (branch != today)
                {
                    bool exists = Exec(git, $"rev-parse --verify --quiet refs/heads/{today}", root, sb, out _) == 0;
                    if (Exec(git, exists ? $"switch {today}" : $"switch -c {today}", root, sb, out _) != 0)
                    {
                        sb.AppendLine("!! 오늘 브랜치로 전환 실패 → 중단");
                        return false;
                    }
                    branch = today;
                }
                Exec(git, "config user.name", root, sb, out string user);
                if (string.IsNullOrWhiteSpace(user)) { sb.AppendLine("!! git user.name 미설정 → 중단"); return false; }

                if (Exec(git, "add -- \"Assets/02.parkhansol_\" \"Assets/02.parkhansol_.meta\"", root, sb, out _) != 0) return false;
                if (toMain && Exec(git, $"add -- \"{SampleScene}\"", root, sb, out _) != 0) return false;
                Exec(git, "diff --cached --stat", root, sb, out string staged);
                Exec(git, "diff --cached --name-only", root, sb, out string names, echo: false);
                foreach (var line in names.Split('\n'))
                {
                    var p = line.Trim();
                    if (p.Length > 0 && !p.StartsWith("Assets/02.parkhansol_") && !(toMain && p == SampleScene))
                    {
                        sb.AppendLine("!! 02.parkhansol_ 밖의 파일이 스테이징됨 → 중단: " + p);
                        return false;
                    }
                }

                if (string.IsNullOrWhiteSpace(staged)) sb.AppendLine("(새로 커밋할 변경 없음)");
                else
                {
                    string msgFile = Path.Combine(Path.GetTempPath(), "oz_commit_msg.txt");
                    File.WriteAllText(msgFile, message + "\n\n" +
                        "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\n" +
                        "Claude-Session: https://claude.ai/code/session_013hgdcgnJnupREACjVtYr69\n", new UTF8Encoding(false));
                    if (Exec(git, $"commit -F \"{msgFile}\"", root, sb, out _) != 0) return false;
                }

                bool remoteHas = Exec(git, $"ls-remote --exit-code --heads origin {branch}", root, sb, out _) == 0;
                if (remoteHas && Exec(git, $"pull --rebase --autostash origin {branch}", root, sb, out _) != 0)
                {
                    sb.AppendLine("!! pull --rebase 실패 → 충돌 가능. rebase 중단 후 종료");
                    Exec(git, "rebase --abort", root, sb, out _);
                    return false;
                }
                if (Exec(git, $"push -u origin {branch}", root, sb, out _, timeoutMs: 600000) != 0) return false;
                if (toMain)
                {
                    if (Exec(git, $"fetch origin {shared}", root, sb, out _, timeoutMs: 300000) != 0) return false;
                    if (Exec(git, $"merge-base --is-ancestor origin/{shared} HEAD", root, sb, out _) != 0)
                    {
                        sb.AppendLine($"!! origin/{shared}에 내 브랜치에 없는 커밋이 있음 (팀원 푸시) → {shared} 푸시 중단. 브랜치는 올라감 → PR로 합칠 것");
                        return false;
                    }
                    if (Exec(git, $"push origin HEAD:{shared}", root, sb, out _, timeoutMs: 600000) != 0) return false;
                }
                Exec(git, "log --oneline -3", root, sb, out _);
                Exec(git, "status --short -- Assets/02.parkhansol_", root, sb, out _);
                ok = true;
                return true;
            }
            catch (Exception e)
            {
                sb.AppendLine("!! 예외: " + e);
                return false;
            }
            finally
            {
                sb.AppendLine(ok ? "== 성공" : "== 실패");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            }
        }

        static string FindGit()
        {
            string[] candidates =
            {
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe"),
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return "git"; // PATH
        }

        static int Exec(string git, string args, string cwd, StringBuilder log, out string stdout, bool echo = true, int timeoutMs = 120000)
        {
            var psi = new ProcessStartInfo(git, args)
            {
                WorkingDirectory = cwd,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            using (var p = Process.Start(psi))
            {
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } log.AppendLine($"$ git {args}\n!! 시간 초과"); stdout = ""; return -1; }
                stdout = outTask.Result;
                string err = errTask.Result;
                if (echo)
                {
                    log.AppendLine($"$ git {args}  (exit {p.ExitCode})");
                    if (!string.IsNullOrWhiteSpace(stdout)) log.AppendLine(Trim(stdout));
                    if (!string.IsNullOrWhiteSpace(err)) log.AppendLine(Trim(err));
                }
                return p.ExitCode;
            }
        }

        static string Trim(string s) => s.Length > 4000 ? s.Substring(0, 4000) + "\n…(생략)" : s.TrimEnd();
    }

    /// <summary>커밋 메시지 입력 창 (parkhansol_내용). Enter 또는 버튼으로 푸시</summary>
    internal class OZGitPushWindow : EditorWindow
    {
        bool _toShared;
        string _shared;
        string _message;

        internal static void Open(bool toShared, string shared)
        {
            var w = GetWindow<OZGitPushWindow>(true, "OZ Git Push", true);
            w._toShared = toShared;
            w._shared = shared;
            w._message = OZGitPush.LastMessage();
            w.minSize = w.maxSize = new Vector2(520, 130);
            w.ShowUtility();
            w.Focus();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(_toShared ? $"오늘 브랜치 + {_shared} 에 푸시 (MainScene 포함)" : "오늘 브랜치에만 푸시 (02.parkhansol_만)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("커밋 메시지 (parkhansol_내용)");
            GUI.SetNextControlName("msg");
            _message = EditorGUILayout.TextField(_message);
            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "msg") EditorGUI.FocusTextInControl("msg");
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("취소", GUILayout.Height(28))) Close();
                if (GUILayout.Button("푸시", GUILayout.Height(28)) || enter)
                {
                    OZGitPush.Push(_toShared, _shared, _message);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
