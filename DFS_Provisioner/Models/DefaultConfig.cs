namespace DFS_Provisioner.Models
{
    /// <summary>Root configuration class representing the entire JSON file.</summary>
    public class DefaultConfig
    {
        public CredentialsConfig Credentials { get; set; }
        public ActiveDirectoryConfig ActiveDirectory { get; set; }
        public ShareConfig Share { get; set; }
        public DfsConfig Dfs { get; set; }
        public OptionsConfig Options { get; set; }
    }

    /// <summary>Credentials stored in plain text (except passwords).</summary>
    public class CredentialsConfig
    {
        public string AdUsername { get; set; }
        public string ServerUsername { get; set; }
        public string Server { get; set; }
        public string DfsUsername { get; set; }
    }

    /// <summary>Active Directory related settings.</summary>
    public class ActiveDirectoryConfig
    {
        public string Domain { get; set; }
        public string GroupsOU { get; set; }
        public string ReadGroupName { get; set; }
        public string WriteGroupName { get; set; }
        public string GroupDescriptionTemplate { get; set; }
        public string GroupNotesTemplate { get; set; }
    }

    /// <summary>File share related settings.</summary>
    public class ShareConfig
    {
        public string LocalPath { get; set; }
        public string OwnerAccount { get; set; }
    }

    /// <summary>DFS namespace related settings.</summary>
    public class DfsConfig
    {
        public string NamespaceRoot { get; set; }
        public string LinkNameTemplate { get; set; }
    }

    /// <summary>Miscellaneous options.</summary>
    public class OptionsConfig
    {
        public int ADReplicationWaitSeconds { get; set; }
    }
}