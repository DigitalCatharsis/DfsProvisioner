using System;
using System.Diagnostics;
using System.Net;
using System.Security;
using System.Text;

namespace DFS_Provisioner.Services
{
    public static class DfsService
    {
        public static bool DfsLinkExists(string namespaceRoot, string linkName,
                                         string username, SecureString password)
        {
            string path = $@"{namespaceRoot}\{linkName}";
            // Скрипт проверки: выводим EXISTS, если папка найдена
            string script = $@"
                if (Get-DfsnFolder -Path '{path}' -ErrorAction SilentlyContinue) {{
                    Write-Output 'EXISTS'
                }} else {{
                    Write-Output 'NOT_FOUND'
                }}
            ";
            string output = RunPowerShell(script, username, password);
            return output?.Contains("EXISTS") == true;
        }

        public static void CreateDfsLink(string namespaceRoot, string linkName,
                                         string folderTargetPath, string description,
                                         string username, SecureString password)
        {
            string path = $@"{namespaceRoot}\{linkName}";
            // Скрипт создания: идемпотентно создаёт ссылку и цель
            string script = $@"
                $existing = Get-DfsnFolder -Path '{path}' -ErrorAction SilentlyContinue
                if (-not $existing) {{
                    New-DfsnFolder -Path '{path}' -TargetPath '{folderTargetPath}' -Description '{description}'
                    Write-Output 'DFS link created.'
                }} else {{
                    $targets = Get-DfsnFolderTarget -Path '{path}'
                    if (-not ($targets | Where-Object {{ $_.TargetPath -eq '{folderTargetPath}' }})) {{
                        New-DfsnFolderTarget -Path '{path}' -TargetPath '{folderTargetPath}'
                        Write-Output 'Target added.'
                    }}
                }}
            ";
            RunPowerShell(script, username, password);
        }

        private static string RunPowerShell(string script, string username, SecureString password)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Разбираем DOMAIN\user
            if (!string.IsNullOrEmpty(username))
            {
                var parts = username.Split('\\');
                if (parts.Length == 2)
                {
                    startInfo.Domain = parts[0];
                    startInfo.UserName = parts[1];
                }
                else
                {
                    startInfo.UserName = username;
                }
                startInfo.Password = password.Copy(); // передаём SecureString без конвертации
            }

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Exception($"PowerShell error ({process.ExitCode}): {output}{error}");

                return output;
            }
        }
    }
}