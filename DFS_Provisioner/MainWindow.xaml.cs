using DFS_Provisioner.ViewModels;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

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
            ViewModel.ClearLogRequested += () => LogRichTextBox.Document.Blocks.Clear();

            // Подписываемся на событие логирования из ViewModel
            ViewModel.LogAppended += OnLogAppended;
        }

        private void OnLogAppended(string message, bool isError)
        {
            var paragraph = new Paragraph();
            var color = isError ? Colors.Red : Colors.White;
            paragraph.Inlines.Add(new Run(message) { Foreground = new SolidColorBrush(color) });

            LogRichTextBox.Document.Blocks.Add(paragraph);
            LogRichTextBox.ScrollToEnd();
        }
    }
}