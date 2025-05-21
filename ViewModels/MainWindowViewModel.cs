using System;
using ReactiveUI;
using Avalonia.Controls;
using System.Reactive;

namespace IAFTS.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private TreeDetectionViewModel? _treeDetectionViewModel;
        private Window? _window;
        private object? _currentTabContent;
        public ReactiveCommand<Unit, Unit> ShowAboutUsTabCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowWorkTabCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowAboutProjectTabCommand { get; }

        public MainWindowViewModel()
        {
            Console.WriteLine("MainWindowViewModel создан");
            TreeDetectionViewModel = new TreeDetectionViewModel();
            ShowAboutUsTabCommand = ReactiveCommand.Create(() => {
                CurrentTabContent = new IAFTS.Views.AboutUsTab();
                return Unit.Default;
            });
            ShowWorkTabCommand = ReactiveCommand.Create(() => {
                CurrentTabContent = CreateWorkContent();
                return Unit.Default;
            });
            ShowAboutProjectTabCommand = ReactiveCommand.Create(() => {
                CurrentTabContent = new IAFTS.Views.AboutProjectTab();
                return Unit.Default;
            });
            CurrentTabContent = CreateWorkContent();
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

        public object CurrentTabContent
        {
            get => _currentTabContent;
            set => this.RaiseAndSetIfChanged(ref _currentTabContent, value);
        }

        // Метод для создания рабочего интерфейса (Grid)
        private object CreateWorkContent()
        {
            return new IAFTS.Views.WorkTab();
        }

        public string Greeting { get; } = "Welcome to Avalonia!";
    }
}