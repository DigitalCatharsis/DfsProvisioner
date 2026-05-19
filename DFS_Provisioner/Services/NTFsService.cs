using System.Management;
using System.Net;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DFS_Provisioner.Services
{
    /// <summary>
    /// Applies NTFS permissions to a remote directory via WMI.
    /// Always removes the Everyone group and then adds the specified read and write groups.
    /// Also sets the folder owner if an owner account is provided.
    /// </summary>
    public static class NtfsService
    {
        /// <summary>Configures NTFS permissions and optionally sets the owner.</summary>
        public static void SetNtfsPermissions(string server, string directoryPath,
        string readGroupSid, string writeGroupSid,
        string ownerAccount,
        string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);

            var mo = new ManagementObject(scope,
            new ManagementPath($"Win32_LogicalFileSecuritySetting.Path='{directoryPath}'"), null);

            // Get current security descriptor 
            var outParams = mo.InvokeMethod("GetSecurityDescriptor", null, null);
            if (outParams == null)
                throw new Exception("GetSecurityDescriptor returned null.");
            var sd = (ManagementBaseObject)outParams["Descriptor"];
            if (sd == null)
                throw new Exception("Security descriptor is null.");

            // 1. Filter the DACL: remove all inherited entries (flag 0x10),
            // to prevent them from being duplicated when saving.
            if (sd["DACL"] is ManagementBaseObject[] dacl)
            {
                var explicitAces = new List<ManagementBaseObject>();
                foreach (var ace in dacl)
                {
                    uint aceFlags = (uint)ace["AceFlags"];
                    // 0x10 (16) is the INHERITED_ACE flag. Skip such entries.
                    if ((aceFlags & 0x10) == 0)
                    {
                        explicitAces.Add(ace);
                    }
                }
                sd["DACL"] = explicitAces.ToArray();
            }

            // 2. Set owner if account is specified 
            if (!string.IsNullOrWhiteSpace(ownerAccount))
                SetOwner(scope, sd, ownerAccount);

            // 3. Remove Everyone and old group ACEs (only from the list of explicit rights) 
            RemoveEveryone(sd);
            RemoveAcesBySid(sd, readGroupSid);
            RemoveAcesBySid(sd, writeGroupSid);

            // 4. Add new ACEs 
            AddAce(scope, sd, readGroupSid, (uint)FileSystemRights.ReadAndExecute,
            (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit));
            AddAce(scope, sd, writeGroupSid, (uint)FileSystemRights.Modify,
            (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit));

            // 5. Set control flags. 
            // 0x8004 = SE_SELF_RELATIVE | SE_DACL_PRESENT 
            sd["ControlFlags"] = 0x8404;

            // Apply the updated descriptor 
            var setParams = mo.GetMethodParameters("SetSecurityDescriptor");
            setParams["Descriptor"] = sd;
            var result = mo.InvokeMethod("SetSecurityDescriptor", setParams, null);

            if (result == null)
                throw new Exception("SetSecurityDescriptor returned null.");
            uint ret = (uint)result["ReturnValue"];
            if (ret != 0)
                throw new Exception($"SetSecurityDescriptor failed with code {ret}");
        }

        /// <summary>Sets the owner field of the security descriptor to the specified account's SID.</summary>
        private static void SetOwner(ManagementScope scope, ManagementBaseObject sd, string ownerAccount)
        {
            try
            {
                var ntAccount = new NTAccount(ownerAccount);
                var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));

                byte[] sidBytes = new byte[sid.BinaryLength];
                sid.GetBinaryForm(sidBytes, 0);

                // Создаем Trustee корректно через scope
                var trusteeClass = new ManagementClass(scope, new ManagementPath("Win32_Trustee"), null);
                var trustee = trusteeClass.CreateInstance();

                trustee["SID"] = sidBytes;
                trustee["Name"] = ntAccount.Value;

                sd["Owner"] = trustee;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при установке владельца: {ex.Message}");
            }
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

        private static void AddAce(ManagementScope scope, ManagementBaseObject sd, string sid, uint accessMask, uint aceFlags)
        {
            var trusteeClass = new ManagementClass(scope, new ManagementPath("Win32_Trustee"), null);
            var trusteeObj = trusteeClass.CreateInstance();
            trusteeObj["SIDString"] = sid;

            var aceClass = new ManagementClass(scope, new ManagementPath("Win32_Ace"), null);
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