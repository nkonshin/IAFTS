using System;
using ReactiveUI;
using Avalonia.Controls;

namespace IAFTS.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private TreeDetectionViewModel? _treeDetectionViewModel;
        private Window? _window;

        public MainWindowViewModel()
        {
            Console.WriteLine("MainWindowViewModel создан");
            TreeDetectionViewModel = new TreeDetectionViewModel();
        }

        public Window? Window
        {
            get => _window;
            set
            {
                Console.WriteLine($"Устанавливаем Window в MainWindowViewModel: {value != null}");
                _window = value;
                // Обновляем Window в TreeDetectionViewModel при изменении
                if (TreeDetectionViewModel != null)
                {
                    Console.WriteLine("Обновляем Window в TreeDetectionViewModel");
                    TreeDetectionViewModel.Window = value;
                }
            }
        }

        public TreeDetectionViewModel TreeDetectionViewModel
        {
            get => _treeDetectionViewModel ??= new TreeDetectionViewModel();
            set
            {
                Console.WriteLine("Устанавливаем TreeDetectionViewModel");
                _treeDetectionViewModel = value;
                if (_treeDetectionViewModel != null && Window != null)
                {
                    Console.WriteLine("Устанавливаем Window в новый TreeDetectionViewModel");
                    _treeDetectionViewModel.Window = Window;
                }
            }
        }

        public string Greeting { get; } = "Welcome to Avalonia!";
    }
}