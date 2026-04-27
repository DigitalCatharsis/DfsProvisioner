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
            catch
            {
                return false;
            }
        }

        public static bool ShareExists(string server, string shareName, string username, SecureString password)
        {
            try
            {
                var scope = GetManagementScope(server, username, password);
                var query = new ObjectQuery($"SELECT * FROM Win32_Share WHERE Name = '{shareName}'");
                var searcher = new ManagementObjectSearcher(scope, query);
                return searcher.Get().Count > 0;
            }
            catch
            {
                return false;
            }
        }

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
            var returnValue = (uint)outParams["ReturnValue"];
            if (returnValue != 0)
                throw new Exception($"Share creation failed. Code: {returnValue}");
        }

        public static void CreateRemoteDirectory(string server, string directoryPath, string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);
            var processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = processClass.GetMethodParameters("Create");
            inParams["CommandLine"] = $"cmd.exe /c if not exist \"{directoryPath}\" mkdir \"{directoryPath}\"";
            processClass.InvokeMethod("Create", inParams, null);
        }

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

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}