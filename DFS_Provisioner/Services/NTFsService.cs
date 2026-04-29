using System;
using System.Collections.Generic;
using System.Management;
using System.Net;
using System.Security;
using System.Security.AccessControl;

namespace DFS_Provisioner.Services
{
    public static class NtfsService
    {
        public static void SetNtfsPermissions(string server, string directoryPath,
                                              string readGroupSid, string writeGroupSid,
                                              string ownerAccount,
                                              string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            SetOwner(scope, directoryPath); // не критично

            var mo = new ManagementObject(scope,
                new ManagementPath($"Win32_LogicalFileSecuritySetting.Path='{directoryPath}'"), null);

            // Получаем дескриптор
            var outParams = mo.InvokeMethod("GetSecurityDescriptor", null, null);
            if (outParams == null)
                throw new Exception("GetSecurityDescriptor returned null.");
            var sd = (ManagementBaseObject)outParams["Descriptor"];
            if (sd == null)
                throw new Exception("Security descriptor is null.");

            // Удаляем Everyone всегда (если нужно управлять флагом, добавьте параметр)
            RemoveEveryone(sd);

            // Удаляем старые ACE для наших групп
            RemoveAcesBySid(sd, readGroupSid);
            RemoveAcesBySid(sd, writeGroupSid);

            // Добавляем новые ACE
            AddAce(sd, readGroupSid, (uint)FileSystemRights.ReadAndExecute,
                   (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit));
            AddAce(sd, writeGroupSid, (uint)FileSystemRights.Modify,
                   (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit));

            // Применяем обратно
            var setParams = mo.GetMethodParameters("SetSecurityDescriptor");
            if (setParams == null)
                throw new Exception("GetMethodParameters for SetSecurityDescriptor returned null.");
            setParams["Descriptor"] = sd;
            var result = mo.InvokeMethod("SetSecurityDescriptor", setParams, null);
            if (result == null)
                throw new Exception("SetSecurityDescriptor returned null.");
            uint ret = (uint)result["ReturnValue"];
            if (ret != 0)
                throw new Exception($"SetSecurityDescriptor failed with code {ret}");
        }

        // Вспомогательные методы

        private static ManagementScope GetManagementScope(string server, string username, SecureString password)
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

        private static void SetOwner(ManagementScope scope, string path)
        {
            var mo = new ManagementObject(scope,
                new ManagementPath($"Win32_LogicalFileSecuritySetting.Path='{path}'"), null);
            try { mo.InvokeMethod("TakeOwnership", null, null); } catch { }
        }

        private static void RemoveEveryone(ManagementBaseObject sd)
        {
            if (sd["DACL"] is ManagementBaseObject[] dacl)
            {
                var filtered = new List<ManagementBaseObject>();
                foreach (var ace in dacl)
                {
                    if (ace["Trustee"] is ManagementBaseObject trustee &&
                        trustee["SIDString"]?.ToString() == "S-1-1-0")
                        continue;
                    filtered.Add(ace);
                }
                sd["DACL"] = filtered.ToArray();
            }
        }

        private static void RemoveAcesBySid(ManagementBaseObject sd, string sid)
        {
            if (sd["DACL"] is ManagementBaseObject[] dacl)
            {
                var filtered = new List<ManagementBaseObject>();
                foreach (var ace in dacl)
                {
                    if (ace["Trustee"] is ManagementBaseObject trustee &&
                        trustee["SIDString"]?.ToString() == sid)
                        continue;
                    filtered.Add(ace);
                }
                sd["DACL"] = filtered.ToArray();
            }
        }

        private static void AddAce(ManagementBaseObject sd, string sid, uint accessMask, uint aceFlags)
        {
            // Корректное создание через CreateInstance
            var trusteeClass = new ManagementClass("Win32_Trustee");
            var trusteeObj = trusteeClass.CreateInstance();
            trusteeObj["SIDString"] = sid;

            var aceClass = new ManagementClass("Win32_Ace");
            var aceObj = aceClass.CreateInstance();
            aceObj["Trustee"] = trusteeObj;
            aceObj["AccessMask"] = accessMask;
            aceObj["AceFlags"] = aceFlags;
            aceObj["AceType"] = 0; // Allow

            var dacl = sd["DACL"] as ManagementBaseObject[] ?? Array.Empty<ManagementBaseObject>();
            var newDacl = new ManagementBaseObject[dacl.Length + 1];
            Array.Copy(dacl, newDacl, dacl.Length);
            newDacl[^1] = aceObj;
            sd["DACL"] = newDacl;
        }

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}