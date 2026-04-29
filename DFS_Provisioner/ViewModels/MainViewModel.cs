using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using Newtonsoft.Json;
using DFS_Provisioner.Models;
using DFS_Provisioner.Services;

namespace DFS_Provisioner.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _configFilePath = "appsettings.json";
        private string _domainName;
        private string _adUsername;
        private string _serverUsername;
        private string _groupsOU;
        private string _groupNamePrefix;
        private string _readGroupSuffix;
        private string _writeGroupSuffix;
        private string _groupDescriptionTemplate;
        private string _groupNotesTemplate;
        private string _shareServer;
        private string _localPath;
        private string _ownerAccount;
        private bool _removeEveryone;
        private string _dfsServer;
        private string _namespaceRoot;
        private string _linkNameTemplate;
        private string _folderTargetPathTemplate;
        private string _readGroupName;
        private string _writeGroupName;

        public string ConfigFilePath { get => _configFilePath; set { _configFilePath = value; OnPropertyChanged(); } }
        public string DomainName { get => _domainName; set { _domainName = value; OnPropertyChanged(); } }
        public string AdUsername { get => _adUsername; set { _adUsername = value; OnPropertyChanged(); } }
        public string ServerUsername { get => _serverUsername; set { _serverUsername = value; OnPropertyChanged(); } }
        public string GroupsOU { get => _groupsOU; set { _groupsOU = value; OnPropertyChanged(); } }
        public string GroupNamePrefix { get => _groupNamePrefix; set { _groupNamePrefix = value; OnPropertyChanged(); } }
        public string ReadGroupSuffix { get => _readGroupSuffix; set { _readGroupSuffix = value; OnPropertyChanged(); } }
        public string WriteGroupSuffix { get => _writeGroupSuffix; set { _writeGroupSuffix = value; OnPropertyChanged(); } }
        public string GroupDescriptionTemplate { get => _groupDescriptionTemplate; set { _groupDescriptionTemplate = value; OnPropertyChanged(); } }
        public string GroupNotesTemplate { get => _groupNotesTemplate; set { _groupNotesTemplate = value; OnPropertyChanged(); } }
        public string ShareServer { get => _shareServer; set { _shareServer = value; OnPropertyChanged(); } }
        public string LocalPath
        {
            get => _localPath;
            set { _localPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShareFolderName)); }
        }
        public string OwnerAccount { get => _ownerAccount; set { _ownerAccount = value; OnPropertyChanged(); } }
        public bool RemoveEveryone { get => _removeEveryone; set { _removeEveryone = value; OnPropertyChanged(); } }
        public string DfsServer { get => _dfsServer; set { _dfsServer = value; OnPropertyChanged(); } }
        public string NamespaceRoot { get => _namespaceRoot; set { _namespaceRoot = value; OnPropertyChanged(); } }
        public string LinkNameTemplate { get => _linkNameTemplate; set { _linkNameTemplate = value; OnPropertyChanged(); } }
        public string FolderTargetPathTemplate { get => _folderTargetPathTemplate; set { _folderTargetPathTemplate = value; OnPropertyChanged(); } }
        public string ReadGroupName { get => _readGroupName; set { _readGroupName = value; OnPropertyChanged(); } }
        public string WriteGroupName { get => _writeGroupName; set { _writeGroupName = value; OnPropertyChanged(); } }

        public string ShareFolderName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LocalPath)) return string.Empty;
                return Path.GetFileName(LocalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
        }

        public SecureString AdPassword { get; set; }
        public SecureString ServerPassword { get; set; }

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(LocalPath))
                {
                    if (string.IsNullOrWhiteSpace(LocalPath))
                        return "Local path cannot be empty";
                    var folder = ShareFolderName;
                    if (folder.Length == 0 || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        return "Invalid folder name in path";
                }
                return null;
            }
        }
        public string Error => null;

        public ICommand LoadConfigCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand CheckAllCommand { get; }
        public ICommand CreateGroupsCommand { get; }
        public ICommand CreateShareAndSecurityCommand { get; }
        public ICommand SetupDfsCommand { get; }
        public ICommand RunAllCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel()
        {
            LoadConfigCommand = new RelayCommand(LoadConfig);
            SaveConfigCommand = new RelayCommand(SaveConfig);
            CheckAllCommand = new RelayCommand(CheckAll);
            CreateGroupsCommand = new RelayCommand(CreateGroups);
            CreateShareAndSecurityCommand = new RelayCommand(CreateShareAndNtfs);
            SetupDfsCommand = new RelayCommand(SetupDfs);
            RunAllCommand = new RelayCommand(RunAll);
            ClearLogCommand = new RelayCommand(ClearLog);

            AutoLoadConfig();
        }

        private void AutoLoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath)) return;
                var config = JsonConvert.DeserializeObject<DefaultConfig>(File.ReadAllText(ConfigFilePath));
                ApplyConfig(config);
                Log("Configuration loaded.");
            }
            catch (Exception ex)
            {
                Log($"Configuration not loaded: {ex.Message}. Fill fields manually.", true);
            }
        }

        private void LoadConfig()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() == true)
            {
                ConfigFilePath = dlg.FileName;
                try
                {
                    var config = JsonConvert.DeserializeObject<DefaultConfig>(File.ReadAllText(ConfigFilePath));
                    ApplyConfig(config);
                    Log("Configuration loaded.");
                }
                catch (Exception ex) { Log($"Error: {ex.Message}", true); }
            }
        }

        private void SaveConfig()
        {
            var config = new DefaultConfig
            {
                Credentials = new CredentialsConfig { AdUsername = AdUsername, ServerUsername = ServerUsername },
                ActiveDirectory = new ActiveDirectoryConfig
                {
                    Domain = DomainName,
                    GroupsOU = GroupsOU,
                    GroupNamePrefix = GroupNamePrefix,
                    ReadGroupSuffix = ReadGroupSuffix,
                    WriteGroupSuffix = WriteGroupSuffix,
                    ReadGroupName = ReadGroupName,
                    WriteGroupName = WriteGroupName,
                    GroupDescriptionTemplate = GroupDescriptionTemplate,
                    GroupNotesTemplate = GroupNotesTemplate
                },
                Share = new ShareConfig { Server = ShareServer, LocalPath = LocalPath, OwnerAccount = OwnerAccount },
                Dfs = new DfsConfig
                {
                    NamespaceServer = DfsServer,
                    NamespaceRoot = NamespaceRoot,
                    LinkNameTemplate = LinkNameTemplate,
                    FolderTargetPathTemplate = FolderTargetPathTemplate
                },
                Options = new OptionsConfig { RemoveEveryoneFromNTFS = RemoveEveryone, ADReplicationWaitSeconds = 5 }
            };
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(config, Formatting.Indented));
                    Log("Saved.");
                }
                catch (Exception ex) { Log($"Save error: {ex.Message}", true); }
            }
        }

        private void ApplyConfig(DefaultConfig config)
        {
            if (config == null) return;
            if (config.Credentials != null)
            {
                AdUsername = config.Credentials.AdUsername ?? "";
                ServerUsername = config.Credentials.ServerUsername ?? "";
            }
            if (config.ActiveDirectory != null)
            {
                DomainName = config.ActiveDirectory.Domain ?? "";
                GroupsOU = config.ActiveDirectory.GroupsOU ?? "";
                GroupNamePrefix = config.ActiveDirectory.GroupNamePrefix ?? "";
                ReadGroupSuffix = config.ActiveDirectory.ReadGroupSuffix ?? "";
                WriteGroupSuffix = config.ActiveDirectory.WriteGroupSuffix ?? "";
                GroupDescriptionTemplate = config.ActiveDirectory.GroupDescriptionTemplate ?? "";
                GroupNotesTemplate = config.ActiveDirectory.GroupNotesTemplate ?? "";

                var folderName = string.IsNullOrWhiteSpace(LocalPath) ? "MyShare" : ShareFolderName;
                ReadGroupName = !string.IsNullOrWhiteSpace(config.ActiveDirectory.ReadGroupName)
                    ? config.ActiveDirectory.ReadGroupName
                    : $"{GroupNamePrefix}{folderName}{ReadGroupSuffix}";
                WriteGroupName = !string.IsNullOrWhiteSpace(config.ActiveDirectory.WriteGroupName)
                    ? config.ActiveDirectory.WriteGroupName
                    : $"{GroupNamePrefix}{folderName}{WriteGroupSuffix}";
            }
            if (config.Share != null)
            {
                ShareServer = config.Share.Server ?? "";
                LocalPath = config.Share.LocalPath ?? "";
                OwnerAccount = config.Share.OwnerAccount ?? "";
            }
            if (config.Dfs != null)
            {
                DfsServer = config.Dfs.NamespaceServer ?? "";
                NamespaceRoot = config.Dfs.NamespaceRoot ?? "";
                LinkNameTemplate = config.Dfs.LinkNameTemplate ?? "";
                FolderTargetPathTemplate = config.Dfs.FolderTargetPathTemplate ?? "";
            }
            if (config.Options != null)
            {
                RemoveEveryone = config.Options.RemoveEveryoneFromNTFS;
            }
        }

        private async void CheckAll()
        {
            Log("=== Uniqueness check ===");
            var pathError = this[nameof(LocalPath)];
            if (!string.IsNullOrEmpty(pathError)) { Log(pathError, true); return; }

            try
            {
                if (!ShareService.TestServerConnection(ShareServer, ServerUsername, ServerPassword))
                    Log("Server connection failed.", true);
                else Log("Server connection OK.");
            }
            catch (Exception ex) { Log($"Server check error: {ex.Message}", true); }

            try
            {
                if (AdService.GroupExists(DomainName, ReadGroupName, AdUsername, AdPassword))
                    Log($"Group {ReadGroupName} exists.", true);
                else Log($"Group {ReadGroupName} free.");

                if (AdService.GroupExists(DomainName, WriteGroupName, AdUsername, AdPassword))
                    Log($"Group {WriteGroupName} exists.", true);
                else Log($"Group {WriteGroupName} free.");
            }
            catch (Exception ex) { Log($"AD error: {ex.Message}", true); }

            try
            {
                if (ShareService.ShareExists(ShareServer, ShareFolderName, ServerUsername, ServerPassword))
                    Log($"Share {ShareFolderName} exists.", true);
                else Log($"Share {ShareFolderName} free.");
            }
            catch (Exception ex) { Log($"Share check error: {ex.Message}", true); }

            try
            {
                var linkName = LinkNameTemplate.Replace("{ShareName}", ShareFolderName);
                if (DfsService.DfsLinkExists(NamespaceRoot, linkName))
                    Log($"DFS link {linkName} exists.", true);
                else
                    Log($"DFS link {linkName} free.");
            }
            catch (Exception ex) { Log($"DFS check error: {ex.Message}", true); }
        }

        private async void CreateGroups()
        {
            Log("=== Creating AD groups ===");
            try
            {
                var descRead = GroupDescriptionTemplate.Replace("{ShareName}", ShareFolderName).Replace("{AccessType}", "Read");
                var descWrite = GroupDescriptionTemplate.Replace("{ShareName}", ShareFolderName).Replace("{AccessType}", "Write");
                var notes = GroupNotesTemplate.Replace("{ShareName}", ShareFolderName).Replace("{Date}", DateTime.Now.ToShortDateString());

                if (!AdService.GroupExists(DomainName, ReadGroupName, AdUsername, AdPassword))
                {
                    AdService.CreateGroup(DomainName, GroupsOU, ReadGroupName, descRead, notes, AdUsername, AdPassword);
                    Log($"Group {ReadGroupName} created.");
                }
                else Log($"Group {ReadGroupName} already exists.");

                if (!AdService.GroupExists(DomainName, WriteGroupName, AdUsername, AdPassword))
                {
                    AdService.CreateGroup(DomainName, GroupsOU, WriteGroupName, descWrite, notes, AdUsername, AdPassword);
                    Log($"Group {WriteGroupName} created.");
                }
                else Log($"Group {WriteGroupName} already exists.");

                Thread.Sleep(5000);
                Log("AD replication wait completed.");
            }
            catch (Exception ex) { Log($"Error: {ex.Message}", true); }
        }

        private async void CreateShareAndNtfs()
        {
            Log("=== Share and NTFS ===");
            try
            {
                ShareService.CreateRemoteDirectory(ShareServer, LocalPath, ServerUsername, ServerPassword);
                Log($"Directory ensured: {LocalPath}");

                if (!ShareService.ShareExists(ShareServer, ShareFolderName, ServerUsername, ServerPassword))
                {
                    ShareService.CreateShare(ShareServer, LocalPath, ShareFolderName,
                                             $"Share {ShareFolderName}", ServerUsername, ServerPassword);
                    Log($"Share '{ShareFolderName}' created.");
                }
                else Log($"Share '{ShareFolderName}' already exists.");

                Thread.Sleep(5000);

                var readGroupSid = AdService.GetGroupSid(DomainName, ReadGroupName, AdUsername, AdPassword);
                var writeGroupSid = AdService.GetGroupSid(DomainName, WriteGroupName, AdUsername, AdPassword);

                NtfsService.SetNtfsPermissions(ShareServer, LocalPath, readGroupSid, writeGroupSid,
                                               OwnerAccount, RemoveEveryone, ServerUsername, ServerPassword);
                Log("NTFS permissions applied.");
            }
            catch (Exception ex) { Log($"Share/NTFS error: {ex.Message}", true); }
        }

        private async void SetupDfs()
        {
            Log("=== DFS ===");
            try
            {
                var linkName = LinkNameTemplate.Replace("{ShareName}", ShareFolderName);
                var target = FolderTargetPathTemplate.Replace("{Server}", ShareServer).Replace("{ShareName}", ShareFolderName);
                DfsService.CreateDfsLink(
                    namespaceRoot: NamespaceRoot,
                    linkName: linkName,
                    folderTargetPath: target,
                    description: $"DFS link for {ShareFolderName}");
                Log($"DFS link '{linkName}' created.");
            }
            catch (Exception ex) { Log($"DFS error: {ex.Message}", true); }
        }

        private async void RunAll()
        {
            CheckAll();
            CreateGroups();
            CreateShareAndNtfs();
            SetupDfs();
            Log("=== All operations completed ===");
        }

        public event Action<string, bool> LogAppended;
        private void Log(string message, bool isError = false)
        {
            var prefix = isError ? "[ERROR] " : "[INFO] ";
            LogAppended?.Invoke($"{DateTime.Now:T} {prefix}{message}", isError);
        }

        public event Action ClearLogRequested;
        private void ClearLog() => ClearLogRequested?.Invoke();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}