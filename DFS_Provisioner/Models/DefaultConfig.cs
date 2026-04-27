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
    }

    public class ActiveDirectoryConfig
    {
        public string Domain { get; set; }
        public string GroupsOU { get; set; }
        public string GroupNamePrefix { get; set; }
        public string ReadGroupSuffix { get; set; }
        public string WriteGroupSuffix { get; set; }
        public string ReadGroupName { get; set; }  
        public string WriteGroupName { get; set; } 
        public string GroupDescriptionTemplate { get; set; }
        public string GroupNotesTemplate { get; set; }
    }

    public class ShareConfig
    {
        public string Server { get; set; }
        public string LocalPath { get; set; }       // full path, e.g. E:\Shares\MyShare
        public string OwnerAccount { get; set; }
    }

    public class DfsConfig
    {
        public string NamespaceServer { get; set; }
        public string NamespaceRoot { get; set; }
        public string LinkNameTemplate { get; set; }
        public string FolderTargetPathTemplate { get; set; }
    }

    public class OptionsConfig
    {
        public bool RemoveEveryoneFromNTFS { get; set; }
        public int ADReplicationWaitSeconds { get; set; }
    }
}