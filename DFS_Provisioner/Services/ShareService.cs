using System.Management;
using System.Net;
using System.Security;

namespace DFS_Provisioner.Services
{
    /// <summary>
    /// Provides methods for managing file shares on a remote server via WMI.
    /// Includes checking existence, creation, permission setting, and remote directory creation.
    /// </summary>
    public static class ShareService
    {
        /// <summary>Tests connection to a server by opening a WMI scope.</summary>
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

        /// <summary>Checks whether an SMB share with the given name exists on the server.</summary>
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

        /// <summary>Creates a new file share.</summary>
        public static void CreateShare(string server, string localPath, string shareName,
                                       string description, string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            var mc = new ManagementClass(scope, new ManagementPath("Win32_Share"), null);
            var inParams = mc.GetMethodParameters("Create");
            inParams["Path"] = localPath;
            inParams["Name"] = shareName;
            inParams["Type"] = 0;
            inParams["Description"] = description;

            var outParams = mc.InvokeMethod("Create", inParams, null);
            uint returnValue = (uint)outParams["ReturnValue"];
            if (returnValue != 0)
                throw new Exception($"Share creation failed. Code: {returnValue}");
        }

        /// <summary>Configures share-level permissions using Win32_LogicalShareSecuritySetting.</summary>
        public static void SetSharePermissions(string server, string shareName,
                                               string readGroupSid, string writeGroupSid,
                                               bool removeEveryone,
                                               string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);

            string objPath = $"Win32_LogicalShareSecuritySetting.Name=\"{shareName}\"";
            var securitySetting = new ManagementObject(scope, new ManagementPath(objPath), null);

            var outParams = securitySetting.InvokeMethod("GetSecurityDescriptor", null, null);
            if (outParams == null)
                throw new Exception("GetSecurityDescriptor returned null.");
            var sd = (ManagementBaseObject)outParams["Descriptor"];
            if (sd == null)
                throw new Exception("Security descriptor is null.");

            if (removeEveryone)
                RemoveEveryone(sd);

            RemoveAcesBySid(sd, readGroupSid);
            RemoveAcesBySid(sd, writeGroupSid);

            AddShareAce(sd, readGroupSid, 0x1200A9, 0);
            AddShareAce(sd, writeGroupSid, 0x1301BF, 0);

            var setParams = securitySetting.GetMethodParameters("SetSecurityDescriptor");
            setParams["Descriptor"] = sd;
            var result = securitySetting.InvokeMethod("SetSecurityDescriptor", setParams, null);
            if (result == null)
                throw new Exception("SetSecurityDescriptor returned null.");
            uint ret = (uint)result["ReturnValue"];
            if (ret != 0)
                throw new Exception($"SetSecurityDescriptor failed with code {ret}");
        }

        /// <summary>Creates a directory on the remote server via cmd.exe.</summary>
        public static void CreateRemoteDirectory(string server, string directoryPath, string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            var mc = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = mc.GetMethodParameters("Create");
            inParams["CommandLine"] = $"cmd.exe /c if not exist \"{directoryPath}\" mkdir \"{directoryPath}\"";
            mc.InvokeMethod("Create", inParams, null);
        }

        /// <summary>Creates a WMI scope for the specified server.</summary>
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
            var trusteeClass = new ManagementClass("Win32_Trustee");
            var trusteeObj = trusteeClass.CreateInstance();
            trusteeObj["SIDString"] = sid;
            trusteeObj["Name"] = sid;

            var aceClass = new ManagementClass("Win32_Ace");
            var aceObj = aceClass.CreateInstance();
            aceObj["Trustee"] = trusteeObj;
            aceObj["AccessMask"] = accessMask;
            aceObj["AceFlags"] = flags;
            aceObj["AceType"] = 0;

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