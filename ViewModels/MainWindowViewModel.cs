using ReactiveUI;
using IAFTS.ViewModels;

namespace IAFTS.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private TreeDetectionViewModel? _treeDetectionViewModel;

        public MainWindowViewModel()
        {
            _treeDetectionViewModel = new TreeDetectionViewModel();
        }

        public TreeDetectionViewModel TreeDetectionViewModel
        {
            get => _treeDetectionViewModel ??= new TreeDetectionViewModel();
        }

        public string Greeting { get; } = "Welcome to Avalonia!";
    }
}
