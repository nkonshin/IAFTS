using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using IAFTS.Models;
using IAFTS.Services;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Linq;

namespace IAFTS.ViewModels
{
    public class TreeDetectionViewModel : ReactiveObject
    {
        private readonly SearchScriptService _searchScriptService;
        private LidarData _lidarData;
        private Window? _window;

        public TreeDetectionViewModel()
        {
            Console.WriteLine("TreeDetectionViewModel создан");
            _searchScriptService = new SearchScriptService("search_script/search_script.py");
            _lidarData = new LidarData
            {
                Trees = new List<Tree>()
            };

            LoadLasCommand = ReactiveCommand.CreateFromTask(ExecuteLoadLas);
            LoadTiffCommand = ReactiveCommand.CreateFromTask(ExecuteLoadTiff);
            ProcessDataCommand = ReactiveCommand.CreateFromTask(ExecuteProcessData);
        }

        public Window? Window
        {
            get => _window;
            set 
            {
                Console.WriteLine($"Устанавливаем Window в TreeDetectionViewModel: {value != null}");
                _window = value;
            }
        }

        public LidarData LidarData
        {
            get => _lidarData;
            set => this.RaiseAndSetIfChanged(ref _lidarData, value);
        }

        public ReactiveCommand<Unit, Unit> LoadLasCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadTiffCommand { get; }
        public ReactiveCommand<Unit, Unit> ProcessDataCommand { get; }

        private async Task ExecuteLoadLas()
        {
            Console.WriteLine("Нажата кнопка загрузки LAS");
            if (Window == null)
            {
                Console.WriteLine("Ошибка: Window не инициализирован");
                return;
            }

            Console.WriteLine("Создаем диалог выбора файла");
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("LAS Files") { Patterns = new[] { "*.las" } }
                }
            };

            try
            {
                var result = await Window.StorageProvider.OpenFilePickerAsync(options);
                if (result.Count > 0)
                {
                    LidarData.LasFilePath = result[0].Path.LocalPath;
                    Console.WriteLine($"Выбран файл LAS: {LidarData.LasFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выборе файла LAS: {ex.Message}");
            }
        }

        private async Task ExecuteLoadTiff()
        {
            Console.WriteLine("Нажата кнопка загрузки TIFF");
            if (Window == null)
            {
                Console.WriteLine("Ошибка: Window не инициализирован");
                return;
            }

            Console.WriteLine("Создаем диалог выбора файла");
            var options = new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("TIFF Files") { Patterns = new[] { "*.tif", "*.tiff" } }
                }
            };

            try
            {
                var result = await Window.StorageProvider.OpenFilePickerAsync(options);
                if (result.Count > 0)
                {
                    LidarData.TiffFilePath = result[0].Path.LocalPath;
                    Console.WriteLine($"Выбран файл TIFF: {LidarData.TiffFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выборе файла TIFF: {ex.Message}");
            }
        }

        private async Task ExecuteProcessData()
        {
            try
            {
                if (string.IsNullOrEmpty(LidarData.LasFilePath) || string.IsNullOrEmpty(LidarData.TiffFilePath))
                {
                    throw new InvalidOperationException("Не выбраны входные файлы");
                }

                Console.WriteLine($"Начинаем обработку данных...");
                Console.WriteLine($"Файл LAS: {LidarData.LasFilePath}");
                Console.WriteLine($"Файл TIFF: {LidarData.TiffFilePath}");

                await _searchScriptService.ProcessDataAsync(LidarData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                throw;
            }
        }
    }
}