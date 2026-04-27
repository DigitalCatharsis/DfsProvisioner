using System;
using System.Collections.Generic;
using System.Management;
using System.Net;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DFS_Provisioner.Services
{
    public static class NtfsService
    {
        public static void SetNtfsPermissions(string server, string directoryPath,
                                      string readGroupSid, string writeGroupSid,
                                      string ownerAccount, bool removeEveryone,
                                      string username, SecureString password)
        {
            var scope = GetManagementScope(server, username, password);

            // Optional: try to set owner (not critical)
            SetOwner(scope, directoryPath); // упростим

            // Get current security descriptor
            var sd = GetSecurityDescriptor(scope, directoryPath);
            if (sd == null)
                throw new Exception("Failed to retrieve security descriptor.");

            // Modify DACL
            if (removeEveryone)
                RemoveEveryone(sd);

            // Add ACE for read
            AddAccessRule(sd, readGroupSid, (uint)FileSystemRights.ReadAndExecute,
                          (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit),
                          0); // Allow

            // Add ACE for write
            AddAccessRule(sd, writeGroupSid, (uint)FileSystemRights.Modify,
                          (uint)(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit),
                          0); // Allow

            // Write back
            SetSecurityDescriptor(scope, directoryPath, sd);
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

        private static ManagementBaseObject GetSecurityDescriptor(ManagementScope scope, string path)
        {
            var mo = new ManagementObject(scope,
                new ManagementPath($"Win32_LogicalFileSecuritySetting.Path='{path}'"), null);
            var outParams = mo.InvokeMethod("GetSecurityDescriptor", null, null);
            // Explicit cast from object to ManagementBaseObject
            return (ManagementBaseObject)outParams["Descriptor"];
        }

        private static void SetSecurityDescriptor(ManagementScope scope, string path,
                                                  ManagementBaseObject descriptor)
        {
            var mo = new ManagementObject(scope,
                new ManagementPath($"Win32_LogicalFileSecuritySetting.Path='{path}'"), null);
            var inParams = mo.GetMethodParameters("SetSecurityDescriptor");
            inParams["Descriptor"] = descriptor;
            mo.InvokeMethod("SetSecurityDescriptor", inParams, null);
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
                var newDacl = new List<ManagementBaseObject>();
                foreach (var ace in dacl)
                {
                    var trustee = ace["Trustee"] as ManagementBaseObject;
                    if (trustee?["SIDString"]?.ToString() == "S-1-1-0")
                        continue;
                    newDacl.Add(ace);
                }
                sd["DACL"] = newDacl.ToArray();
            }
        }

        // Вспомогательный метод создания ACE по SID
        private static void AddAccessRule(ManagementBaseObject sd, string sid,
                                          uint accessMask, uint aceFlags, uint aceType)
        {
            var trustee = new ManagementClass("Win32_Trustee");
            trustee["SIDString"] = sid;

            var ace = new ManagementClass("Win32_Ace");
            ace["Trustee"] = trustee;
            ace["AccessMask"] = accessMask;
            ace["AceFlags"] = aceFlags;
            ace["AceType"] = aceType; // 0 = Allow

            var dacl = sd["DACL"] as ManagementBaseObject[] ?? Array.Empty<ManagementBaseObject>();
            var list = new List<ManagementBaseObject>(dacl) { ace };
            sd["DACL"] = list.ToArray();
        }

        private static ManagementObject CreateAce(string accountName, FileSystemRights rights,
                                                  InheritanceFlags inheritance, PropagationFlags propagation,
                                                  AccessControlType type)
        {
            // Convert account name to SID
            var ntAccount = new NTAccount(accountName);
            var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));

            var trustee = new ManagementClass("Win32_Trustee");
            trustee["SIDString"] = sid.Value;
            trustee["Name"] = accountName;

            var ace = new ManagementClass("Win32_Ace");
            ace["Trustee"] = trustee;
            ace["AccessMask"] = (uint)rights;

            // Fix CS0019: cast inheritance to uint before bitwise OR
            uint aceFlags = (uint)inheritance | PropagationFlagsToAceFlags(inheritance, propagation);
            ace["AceFlags"] = aceFlags;

            ace["AceType"] = type == AccessControlType.Allow ? 0 : 1; // 0 = Allow, 1 = Deny

            return ace;
        }

        private static uint PropagationFlagsToAceFlags(InheritanceFlags inheritance, PropagationFlags propagation)
        {
            uint flags = 0;
            if ((inheritance & InheritanceFlags.ContainerInherit) != 0) flags |= 2; // CONTAINER_INHERIT_ACE
            if ((inheritance & InheritanceFlags.ObjectInherit) != 0) flags |= 1;    // OBJECT_INHERIT_ACE
            if (propagation == PropagationFlags.NoPropagateInherit) flags |= 4;     // NO_PROPAGATE_INHERIT_ACE
            if (propagation == PropagationFlags.InheritOnly) flags |= 8;            // INHERIT_ONLY_ACE
            return flags;
        }

        private static string ToPlainString(SecureString secure)
        {
            return new NetworkCredential(string.Empty, secure).Password;
        }
    }
}