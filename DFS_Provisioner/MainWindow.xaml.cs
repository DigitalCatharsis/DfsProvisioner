using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DFS_Provisioner.ViewModels;

namespace DFS_Provisioner
{
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            AdPasswordBox.PasswordChanged += (_, _) => ViewModel.AdPassword = AdPasswordBox.SecurePassword;
            ServerPasswordBox.PasswordChanged += (_, _) => ViewModel.ServerPassword = ServerPasswordBox.SecurePassword;
            DfsPasswordBox.PasswordChanged += (_, _) => ViewModel.DfsPassword = DfsPasswordBox.SecurePassword;

            ViewModel.LogAppended += (msg, isError) =>
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run(msg)
                {
                    Foreground = isError ? Brushes.Red : Brushes.White
                });
                LogRichTextBox.Document.Blocks.Add(paragraph);
                LogRichTextBox.ScrollToEnd();
            };
            ViewModel.ClearLogRequested += () => LogRichTextBox.Document.Blocks.Clear();
        }
    }
}