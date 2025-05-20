using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IAFTS.Models;
using System.IO;
using System.Text;

namespace IAFTS.Services
{
    public class SearchScriptService
    {
        private readonly string _scriptPath;

        public SearchScriptService(string scriptPath)
        {
            // Преобразуем путь к скрипту в формат macOS
            _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptPath);
        }

        public async Task ProcessDataAsync(LidarData data)
        {
            try
            {
                // Преобразуем пути к файлам в формат macOS
                var lasPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, data.LasFilePath);
                var tiffPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, data.TiffFilePath);
                var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, data.OutputPath ?? "output");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python3",  // На macOS обычно python3 вместо python
                    Arguments = $"{Path.GetFullPath(_scriptPath)} {Path.GetFullPath(lasPath)} {Path.GetFullPath(tiffPath)} {Path.GetFullPath(outputPath)}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                // Чтение вывода
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception($"Ошибка обработки данных: {error}");
                }

                // Добавляем логирование для отладки
                Console.WriteLine($"Python скрипт завершился успешно");
                Console.WriteLine($"Вывод: {output}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обработке данных: {ex.Message}", ex);
            }
        }
    }
}
