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
            SaveResultsCommand = ReactiveCommand.CreateFromTask(ExecuteSaveResults);
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
                    throw new InvalidOperationException("Не выбраны входные файлы (LAS и TIFF)");
                }

                // Определяем рабочую папку — ту же, что у исходного TIFF (или LAS)
                string workDir = System.IO.Path.GetDirectoryName(LidarData.TiffFilePath) ?? System.IO.Path.GetDirectoryName(LidarData.LasFilePath) ?? Environment.CurrentDirectory;

                // Формируем пути для shp, csv, image
                LidarData.ShpFilePath = System.IO.Path.Combine(workDir, "result.shp");
                LidarData.CsvFilePath = System.IO.Path.Combine(workDir, "result.csv");
                LidarData.ImageFilePath = System.IO.Path.Combine(workDir, "result.jpg");

                Console.WriteLine($"Рабочая папка: {workDir}");
                Console.WriteLine($"Файл LAS: {LidarData.LasFilePath}");
                Console.WriteLine($"Файл TIFF: {LidarData.TiffFilePath}");
                Console.WriteLine($"Файл SHP: {LidarData.ShpFilePath}");
                Console.WriteLine($"Файл CSV: {LidarData.CsvFilePath}");
                Console.WriteLine($"Файл IMAGE: {LidarData.ImageFilePath}");

                await _searchScriptService.ProcessDataAsync(LidarData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                throw;
            }
        }

        public ReactiveCommand<Unit, Unit> SaveResultsCommand { get; }

        private async Task ExecuteSaveResults()
        {
            if (Window == null)
            {
                Console.WriteLine("Ошибка: Window не инициализирован");
                return;
            }

            var options = new FolderPickerOpenOptions
            {
                Title = "Выберите папку для сохранения результатов"
            };
            var folders = await Window.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count == 0)
                return;

            string targetDir = folders[0].Path.LocalPath;

            // Копируем все нужные файлы
            void CopyIfExists(string? from, string toName)
            {
                if (!string.IsNullOrEmpty(from) && System.IO.File.Exists(from))
                {
                    string dest = System.IO.Path.Combine(targetDir, toName);
                    System.IO.File.Copy(from, dest, overwrite: true);
                    Console.WriteLine($"Скопирован {from} -> {dest}");
                }
            }

            CopyIfExists(LidarData.ShpFilePath, "result.shp");
            CopyIfExists(LidarData.CsvFilePath, "result.csv");
            CopyIfExists(LidarData.ImageFilePath, "result.jpg");
            // Можно добавить копирование других файлов, если потребуется
        }
    }
}