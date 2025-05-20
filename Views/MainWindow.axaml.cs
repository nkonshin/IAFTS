using Avalonia.Controls;
using IAFTS.ViewModels;

namespace IAFTS.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            var viewModel = new MainWindowViewModel
            {
                Window = this
            };
            
            // Устанавливаем DataContext после инициализации компонентов
            DataContext = viewModel;
            
            // Убедимся, что TreeDetectionViewModel получил ссылку на окно
            viewModel.TreeDetectionViewModel.Window = this;
        }
    }
}