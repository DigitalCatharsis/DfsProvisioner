using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security;

namespace DFS_Provisioner.Services
{
    public static class AdService
    {
        // Checks if a group with the specified name exists in the domain
        public static bool GroupExists(string domain, string groupName, string username, SecureString password)
        {
            // Establish a connection to the domain using provided credentials
            using (var context = new PrincipalContext(ContextType.Domain, domain, username, ToPlainString(password)))
            // Create a searcher with a GroupPrincipal filter set to the target group name
            using (var searcher = new PrincipalSearcher(new GroupPrincipal(context) { Name = groupName }))
            {
                // Return true if at least one matching object is found
                return searcher.FindOne() != null;
            }
        }

        public static bool TestDomainConnection(String domain, String username, SecureString password)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, domain, username, ToPlainString(password)))
                {
                    // Simply creating the context checks the account to see if the domain is available
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
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

        // Creates a new security group in a specific Organizational Unit (OU)
        public static void CreateGroup
            (string domain, 
            string ou, 
            string groupName, 
            string description, 
            string notes, 
            string username, 
            SecureString password)
        {
            // Connect to a specific container (OU) in the domain
            using (var context = new PrincipalContext(ContextType.Domain, domain, ou, username, ToPlainString(password)))
            {
                // Define a new group object and set its basic description
                var group = new GroupPrincipal(context, groupName)
                {
                    Description = description
                };
                // Commit the new group creation to Active Directory
                group.Save();

                // If extra notes are provided, update the low-level 'info' attribute
                if (!string.IsNullOrEmpty(notes))
                {
                    // Access the underlying DirectoryEntry (COM object) for advanced attributes
                    var entry = (DirectoryEntry)group.GetUnderlyingObject();
                    // Set the 'info' property which corresponds to the 'Notes' field in AD
                    entry.Properties["info"].Value = notes;
                    // Save changes to the raw AD object
                    entry.CommitChanges();
                }
            }
        }

        //TODO: FIX!
        // Helper method to convert SecureString back to a plain text string for AD authentication
        private static string ToPlainString(SecureString secure)
        {
            // NetworkCredential is used here as a secure way to extract the password pointer
            return new System.Net.NetworkCredential(string.Empty, secure).Password;
        }
    }
}
