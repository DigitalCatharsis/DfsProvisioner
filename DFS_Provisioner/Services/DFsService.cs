using System.Diagnostics;
using System.Text;

namespace DFS_Provisioner.Services
{
    public static class DfsService
    {
        public static bool DfsLinkExists(string namespaceRoot, string linkName)
        {
            string path = $@"{namespaceRoot}\{linkName}";
            string script = $"if (Get-DfsnFolder -Path '{path}' -ErrorAction SilentlyContinue) {{ 'EXISTS' }} else {{ 'NOT_FOUND' }}";
            string result = RunPowerShell(script);
            return result?.Contains("EXISTS") == true;
        }

        public static void CreateDfsLink(string namespaceRoot, string linkName,
                                         string folderTargetPath, string description)
        {
            string path = $@"{namespaceRoot}\{linkName}";

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
            RunPowerShell(script);
        }

        private static string RunPowerShell(string script)
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