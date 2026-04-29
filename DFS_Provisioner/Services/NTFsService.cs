using System.IO;
using System.Net;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace DFS_Provisioner.Services
{
    public static class NtfsService
    {
        [DllImport("mpr.dll")]
        private static extern int WNetCancelConnection2(string name, int flags, bool force);

        public static void SetNtfsPermissions(string server, string directoryPath,
                                              string readGroupSid, string writeGroupSid,
                                              string ownerAccount, bool removeEveryone,
                                              string username, SecureString password)
        {
            var driveLetter = directoryPath.Substring(0, 1).ToUpperInvariant();
            var folderPath = directoryPath.Substring(2).TrimStart('\\'); // удаляем "C:" или "C:\"
            var adminSharePath = $@"\\{server}\{driveLetter}$";
            var fullUncPath = $@"{adminSharePath}\{folderPath}";

            var credentials = new NetworkCredential(username, ToPlainString(password));

            // 1. Отключаем ВСЕ соединения с этим сервером (не только конкретную шару)
            DisconnectAllFromServer(server);

            // 2. Создаём новое подключение
            using (new NetworkConnection(adminSharePath, credentials))
            {
                var dirInfo = new DirectoryInfo(fullUncPath);
                if (!dirInfo.Exists)
                    throw new DirectoryNotFoundException($"Directory not found: {fullUncPath}");

                var security = dirInfo.GetAccessControl();

                if (removeEveryone)
                {
                    security.RemoveAccessRuleAll(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                }

                var readSidObj = new SecurityIdentifier(readGroupSid);
                var writeSidObj = new SecurityIdentifier(writeGroupSid);

                RemoveSpecificRule(security, readSidObj);
                RemoveSpecificRule(security, writeSidObj);

                security.AddAccessRule(new FileSystemAccessRule(
                    readSidObj,
                    FileSystemRights.ReadAndExecute,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                security.AddAccessRule(new FileSystemAccessRule(
                    writeSidObj,
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                if (!string.IsNullOrWhiteSpace(ownerAccount))
                {
                    try
                    {
                        security.SetOwner(new NTAccount(ownerAccount));
                    }
                    catch { }
                }

                dirInfo.SetAccessControl(security);
            }
        }

        /// <summary>
        /// Отключает все сетевые соединения с указанным сервером.
        /// </summary>
        private static void DisconnectAllFromServer(string server)
        {
            // Пытаемся отключить IPC$ (основное соединение) и административные шары
            var pathsToDisconnect = new[]
            {
                $@"\\{server}\IPC$",
                $@"\\{server}\C$",
                $@"\\{server}\D$",
                $@"\\{server}\E$",
                $@"\\{server}\F$",
                $@"\\{server}",
            };

            foreach (var path in pathsToDisconnect)
            {
                // Принудительно отключаем, игнорируем ошибки (если не было подключено)
                WNetCancelConnection2(path, 0, true);
            }

            // Небольшая пауза, чтобы Windows успела освободить ресурсы
            System.Threading.Thread.Sleep(500);
        }

        private static void RemoveSpecificRule(DirectorySecurity security, SecurityIdentifier sid)
        {
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference == sid)
                {
                    security.RemoveAccessRuleAll(rule);
                }
            }
        }

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}