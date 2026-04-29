namespace DFS_Provisioner.Models
{
    public class DefaultConfig
    {
        public CredentialsConfig Credentials { get; set; }
        public ActiveDirectoryConfig ActiveDirectory { get; set; }
        public ShareConfig Share { get; set; }
        public DfsConfig Dfs { get; set; }
        public OptionsConfig Options { get; set; }
    }

    public class CredentialsConfig
    {
        public string AdUsername { get; set; }
        public string ServerUsername { get; set; }
        public string Server { get; set; }
        public string DfsUsername { get; set; }   
    }

    public class ActiveDirectoryConfig
    {
        public string Domain { get; set; }
        public string GroupsOU { get; set; }
        public string ReadGroupName { get; set; }
        public string WriteGroupName { get; set; }
        public string GroupDescriptionTemplate { get; set; }
        public string GroupNotesTemplate { get; set; }
    }

    public class ShareConfig
    {
        public string LocalPath { get; set; }
        public string OwnerAccount { get; set; }
    }

    public class DfsConfig
    {
        public string NamespaceRoot { get; set; }
        public string LinkNameTemplate { get; set; }
    }

    public class OptionsConfig
    {
        public int ADReplicationWaitSeconds { get; set; }
    }
}