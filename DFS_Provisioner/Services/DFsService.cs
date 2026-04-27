using System.Management;
using System.Net;
using System.Security;

namespace DFS_Provisioner.Services
{
    public static class DfsService
    {
        // Checks if a DFS link exists using WMI query against Win32_DFSNode
        public static bool DfsLinkExists(string namespaceServer, string namespaceRoot, string linkName,
                                         string username, SecureString password)
        {
            try
            {
                var scope = GetManagementScope(namespaceServer, username, password);
                // Search for a node with the specific combined path
                var query = new ObjectQuery(
                    $"SELECT * FROM Win32_DFSNode WHERE Path = '{namespaceRoot}\\{linkName}'");
                var searcher = new ManagementObjectSearcher(scope, query);
                return searcher.Get().Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Creates a DFS link and assigns a physical target folder to it
        public static void CreateDfsLink(string namespaceServer, string namespaceRoot, string linkName,
                                         string folderTargetPath, string description,
                                         string username, SecureString password)
        {
            var scope = GetManagementScope(namespaceServer, username, password);

            // Find the specific DFS Namespace instance to perform actions on it
            var nsClass = new ManagementClass(scope, new ManagementPath("Win32_DFSNamespace"), null);
            var nsObjects = nsClass.GetInstances();
            ManagementObject nsObject = null;
            foreach (ManagementObject obj in nsObjects)
            {
                if (obj["Path"]?.ToString().Equals(namespaceRoot, StringComparison.OrdinalIgnoreCase) == true)
                {
                    nsObject = obj;
                    break;
                }
            }

            if (nsObject == null)
                throw new Exception($"DFS namespace not found: {namespaceRoot}");

            // Step 1: Create the DFS folder (the logical entry in the namespace)
            var inParams = nsObject.GetMethodParameters("AddFolder");
            inParams["Path"] = linkName;
            inParams["Description"] = description;
            var outParams = nsObject.InvokeMethod("AddFolder", inParams, null);
            uint returnValue = (uint)outParams["ReturnValue"];

            // Error code 183 means the folder already exists, which we might ignore
            if (returnValue != 0 && returnValue != 183)
                throw new Exception($"Failed to create DFS link. Return code: {returnValue}");

            // Step 2: Add a target (physical share) to the newly created DFS folder
            var folderObject = GetDfsFolder(scope, $"{namespaceRoot}\\{linkName}");
            var targetParams = folderObject.GetMethodParameters("AddTarget");
            targetParams["EntryPath"] = folderTargetPath;
            targetParams["Priority"] = 1; // Sets target priority within the referral list
            var targetOut = folderObject.InvokeMethod("AddTarget", targetParams, null);
            uint targetReturn = (uint)targetOut["ReturnValue"];

            if (targetReturn != 0)
                throw new Exception($"Failed to add DFS target. Return code: {targetReturn}");
        }

        // Configures connection to the MicrosoftDFS WMI namespace
        private static ManagementScope GetManagementScope(string server, string username, SecureString password)
        {
            var options = new ConnectionOptions
            {
                Username = username,
                Password = ToPlainString(password),
                Authentication = AuthenticationLevel.PacketPrivacy // Encryption is required for DFS WMI
            };
            // Target the specific DFS management namespace
            var scope = new ManagementScope($@"\\{server}\root\MicrosoftDFS", options);
            scope.Connect();
            return scope;
        }

        // Helper to retrieve a ManagementObject for a specific DFS node
        private static ManagementObject GetDfsFolder(ManagementScope scope, string fullPath)
        {
            var query = new ObjectQuery($"SELECT * FROM Win32_DFSNode WHERE Path = '{fullPath}'");
            var searcher = new ManagementObjectSearcher(scope, query);
            var result = searcher.Get();
            if (result.Count == 0)
                throw new Exception($"DFS folder not found: {fullPath}");

            var enumerator = result.GetEnumerator();
            enumerator.MoveNext();
            return (ManagementObject)enumerator.Current;
        }

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}
