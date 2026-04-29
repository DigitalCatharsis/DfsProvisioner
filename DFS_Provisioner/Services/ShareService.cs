using System;
using System.Linq;
using System.Management;
using System.Net;
using System.Security;

namespace DFS_Provisioner.Services
{
    public static class ShareService
    {
        public static bool TestServerConnection(string server, string username, SecureString password)
        {
            try
            {
                var scope = GetManagementScope(server, username, password);
                scope.Connect();
                return true;
            }
            catch { return false; }
        }

        public static bool ShareExists(string server, string shareName, string username, SecureString password)
        {
            try
            {
                var scope = GetManagementScope(server, username, password);
                var query = new ObjectQuery($"SELECT * FROM Win32_Share WHERE Name = '{shareName}'");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    return searcher.Get().Count > 0;
                }
            }
            catch { return false; }
        }

        public static void CreateShare(string server, string localPath, string shareName,
                                       string description, string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            var mc = new ManagementClass(scope, new ManagementPath("Win32_Share"), null);
            var inParams = mc.GetMethodParameters("Create");
            inParams["Path"] = localPath;
            inParams["Name"] = shareName;
            inParams["Type"] = 0; // Disk Drive
            inParams["Description"] = description;

            var outParams = mc.InvokeMethod("Create", inParams, null);
            uint returnValue = (uint)outParams["ReturnValue"];
            if (returnValue != 0)
                throw new Exception($"Share creation failed. Code: {returnValue}");
        }

        public static void SetSharePermissions(string server, string shareName,
                                               string readGroupSid, string writeGroupSid,
                                               bool removeEveryone,
                                               string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);

            // Класс Win32_LogicalShareSecuritySetting позволяет управлять разрешениями шары
            string objPath = $"Win32_LogicalShareSecuritySetting.Name=\"{shareName}\"";
            var securitySetting = new ManagementObject(scope, new ManagementPath(objPath), null);

            // Получаем текущий дескриптор безопасности
            var outParams = securitySetting.InvokeMethod("GetSecurityDescriptor", null, null);
            if (outParams == null)
                throw new Exception("GetSecurityDescriptor returned null.");
            var sd = (ManagementBaseObject)outParams["Descriptor"];
            if (sd == null)
                throw new Exception("Security descriptor is null.");

            // Удаляем Everyone (SID: S-1-1-0), если нужно
            if (removeEveryone)
                RemoveEveryone(sd);

            // Удаляем существующие ACE для наших групп, чтобы избежать дублирования
            RemoveAcesBySid(sd, readGroupSid);
            RemoveAcesBySid(sd, writeGroupSid);

            // Добавляем новые ACE: Read для readGroupSid, Change для writeGroupSid
            AddShareAce(sd, readGroupSid, 0x1200A9, 0); // READ
            AddShareAce(sd, writeGroupSid, 0x1301BF, 0); // CHANGE

            // Применяем изменённый дескриптор
            var setParams = securitySetting.GetMethodParameters("SetSecurityDescriptor");
            setParams["Descriptor"] = sd;
            var result = securitySetting.InvokeMethod("SetSecurityDescriptor", setParams, null);
            if (result == null)
                throw new Exception("SetSecurityDescriptor returned null.");
            uint ret = (uint)result["ReturnValue"];
            if (ret != 0)
                throw new Exception($"SetSecurityDescriptor failed with code {ret}");
        }

        public static void CreateRemoteDirectory(string server, string directoryPath, string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            var mc = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = mc.GetMethodParameters("Create");
            inParams["CommandLine"] = $"cmd.exe /c if not exist \"{directoryPath}\" mkdir \"{directoryPath}\"";
            mc.InvokeMethod("Create", inParams, null);
        }

        // Вспомогательные приватные методы

        private static void RemoveEveryone(ManagementBaseObject sd)
        {
            if (sd["DACL"] is ManagementBaseObject[] dacl)
            {
                var filtered = dacl.Where(ace =>
                {
                    if (ace["Trustee"] is ManagementBaseObject trustee)
                        return trustee["SIDString"]?.ToString() != "S-1-1-0";
                    return true;
                }).ToArray();
                sd["DACL"] = filtered;
            }
        }

        private static void RemoveAcesBySid(ManagementBaseObject sd, string sid)
        {
            if (sd["DACL"] is ManagementBaseObject[] dacl)
            {
                var filtered = dacl.Where(ace =>
                {
                    if (ace["Trustee"] is ManagementBaseObject trustee)
                        return trustee["SIDString"]?.ToString() != sid;
                    return true;
                }).ToArray();
                sd["DACL"] = filtered;
            }
        }

        private static void AddShareAce(ManagementBaseObject sd, string sid, uint accessMask, uint flags)
        {
            // Создаём корректный объект Trustee через CreateInstance
            var trusteeClass = new ManagementClass("Win32_Trustee");
            var trusteeObj = trusteeClass.CreateInstance();
            trusteeObj["SIDString"] = sid;
            trusteeObj["Name"] = sid; // опционально

            // Создаём корректный объект ACE
            var aceClass = new ManagementClass("Win32_Ace");
            var aceObj = aceClass.CreateInstance();
            aceObj["Trustee"] = trusteeObj;
            aceObj["AccessMask"] = accessMask;
            aceObj["AceFlags"] = flags;
            aceObj["AceType"] = 0; // Allow

            // Добавляем ACE в массив DACL
            var dacl = sd["DACL"] as ManagementBaseObject[] ?? Array.Empty<ManagementBaseObject>();
            var newDacl = new ManagementBaseObject[dacl.Length + 1];
            Array.Copy(dacl, newDacl, dacl.Length);
            newDacl[^1] = aceObj;
            sd["DACL"] = newDacl;
        }

        public static ManagementScope GetManagementScope(string server, string username, SecureString password)
        {
            var options = new ConnectionOptions
            {
                Username = username,
                Password = ToPlainString(password),
                Authentication = AuthenticationLevel.PacketPrivacy
            };
            var scope = new ManagementScope($@"\\{server}\root\cimv2", options);
            scope.Connect();
            return scope;
        }

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}