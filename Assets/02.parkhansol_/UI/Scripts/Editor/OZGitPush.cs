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
    /// </summary>
    internal static class OZGitPush
    {
        const string DefaultMessage = "parkhansol_UI기반세팅_HUD창지도대화_자동점검";
        static bool _running;

        [MenuItem("OZ/UI/Git Push (02.parkhansol_ only)", priority = 40)]
        static void Push()
        {
            if (_running) { Debug.LogWarning("[OZ Git] 이미 실행 중입니다."); return; }
            AssetDatabase.SaveAssets();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string log = Path.Combine(root, "Logs", "OZ_GitPush.txt");
            _running = true;
            Debug.Log("[OZ Git] 푸시 시작… (결과: Logs/OZ_GitPush.txt)");
            Task.Run(() => Run(root, log, DefaultMessage)).ContinueWith(t =>
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

        static bool Run(string root, string logPath, string message)
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
                Exec(git, "config user.name", root, sb, out string user);
                if (string.IsNullOrWhiteSpace(user)) { sb.AppendLine("!! git user.name 미설정 → 중단"); return false; }

                if (Exec(git, "add -- \"Assets/02.parkhansol_\" \"Assets/02.parkhansol_.meta\"", root, sb, out _) != 0) return false;
                Exec(git, "diff --cached --stat", root, sb, out string staged);
                Exec(git, "diff --cached --name-only", root, sb, out string names, echo: false);
                foreach (var line in names.Split('\n'))
                {
                    var p = line.Trim();
                    if (p.Length > 0 && !p.StartsWith("Assets/02.parkhansol_"))
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
                        "Claude-Session: https://claude.ai/code/session_0169M9VqZ6KuRRJuVCFkj3Uh\n", new UTF8Encoding(false));
                    if (Exec(git, $"commit -F \"{msgFile}\"", root, sb, out _) != 0) return false;
                }

                if (Exec(git, $"pull --rebase --autostash origin {branch}", root, sb, out _) != 0)
                {
                    sb.AppendLine("!! pull --rebase 실패 → 충돌 가능. rebase 중단 후 종료");
                    Exec(git, "rebase --abort", root, sb, out _);
                    return false;
                }
                if (Exec(git, $"push origin {branch}", root, sb, out _, timeoutMs: 600000) != 0) return false;
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
}
