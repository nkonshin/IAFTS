using Avalonia.Controls;
using Avalonia.Interactivity;

namespace IAFTS.Views
{
    public partial class InfoDialog : Window
    {
        public InfoDialog(string message)
        {
            InitializeComponent();
            this.FindControl<TextBlock>("MessageText").Text = message;
        }

        private void Ok_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
