using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security;

namespace DFS_Provisioner.Services
{
    /// <summary>
    /// Provides methods for interacting with Active Directory:
    /// checking group existence, creating groups, obtaining SIDs, and testing domain connections.
    /// </summary>
    public static class AdService
    {
        /// <summary>Checks whether a security group exists in the specified domain.</summary>
        public static bool GroupExists(string domain, string groupName, string username, SecureString password)
        {
            using (var context = new PrincipalContext(ContextType.Domain, domain, username, ToPlainString(password)))
            using (var searcher = new PrincipalSearcher(new GroupPrincipal(context) { Name = groupName }))
            {
                return searcher.FindOne() != null;
            }
        }

        /// <summary>Tests a domain connection by attempting to create a PrincipalContext.</summary>
        public static bool TestDomainConnection(string domain, string username, SecureString password)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, domain, username, ToPlainString(password)))
                {
                    // Context creation validates the credentials
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Retrieves the SID of a security group by its name.</summary>
        public static string GetGroupSid(string domain, string groupName, string username, SecureString password)
        {
            using (var context = new PrincipalContext(ContextType.Domain, domain, username, ToPlainString(password)))
            {
                var group = GroupPrincipal.FindByIdentity(context, IdentityType.Name, groupName);
                if (group == null)
                    throw new Exception($"Group '{groupName}' not found.");
                return group.Sid.Value;
            }
        }

        /// <summary>Creates a new security group in the specified OU with description and notes.</summary>
        public static void CreateGroup(
            string domain,
            string ou,
            string groupName,
            string description,
            string notes,
            string username,
            SecureString password)
        {
            using (var context = new PrincipalContext(ContextType.Domain, domain, ou, username, ToPlainString(password)))
            {
                var group = new GroupPrincipal(context, groupName)
                {
                    Description = description
                };
                group.Save();

                if (!string.IsNullOrEmpty(notes))
                {
                    var entry = (DirectoryEntry)group.GetUnderlyingObject();
                    entry.Properties["info"].Value = notes;
                    entry.CommitChanges();
                }
            }
        }

        /// <summary>Converts a SecureString to a plain string (used for AD authentication only).</summary>
        private static string ToPlainString(SecureString secure)
        {
            return new System.Net.NetworkCredential(string.Empty, secure).Password;
        }
    }
}