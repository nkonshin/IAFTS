using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveUI;
using IAFTS.Models;
using IAFTS.Services;
using System.Reactive;

namespace IAFTS.ViewModels
{
    public class TreeDetectionViewModel : ReactiveObject
    {
        private readonly SearchScriptService _searchScriptService;
        private LidarData _lidarData;

        public TreeDetectionViewModel()
        {
            _searchScriptService = new SearchScriptService("search_script/search_script.py");
            _lidarData = new LidarData
            {
                Trees = new List<Tree>()
            };

            LoadLasCommand = ReactiveCommand.CreateFromTask(ExecuteLoadLas);
            LoadTiffCommand = ReactiveCommand.CreateFromTask(ExecuteLoadTiff);
            ProcessDataCommand = ReactiveCommand.CreateFromTask(ExecuteProcessData);
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
            // TODO: Реализовать диалог выбора файла LAS
            var lasFilePath = "path/to/las/file.las";
            LidarData.LasFilePath = lasFilePath;
            await Task.Delay(1); // Добавляем await для асинхронности
        }

        private async Task ExecuteLoadTiff()
        {
            // TODO: Реализовать диалог выбора файла TIFF
            var tiffFilePath = "path/to/tiff/file.tif";
            LidarData.TiffFilePath = tiffFilePath;
            await Task.Delay(1); // Добавляем await для асинхронности
        }

        private async Task ExecuteProcessData()
        {
            try
            {
                // Проверяем наличие файлов
                if (string.IsNullOrEmpty(LidarData.LasFilePath) || string.IsNullOrEmpty(LidarData.TiffFilePath))
                {
                    throw new InvalidOperationException("Не выбраны входные файлы");
                }

                // Обработка данных через Python скрипт
                await _searchScriptService.ProcessDataAsync(LidarData);
            }
            catch (Exception ex)
            {
                // Используем ex для вывода ошибки
                Console.WriteLine($"Ошибка: {ex.Message}");
                throw;
            }
        }
    }
}