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
            _scriptPath = scriptPath;
        }

        public async Task ProcessDataAsync(LidarData data)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"{_scriptPath} {data.LasFilePath} {data.TiffFilePath} {data.OutputPath}",
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обработке данных: {ex.Message}", ex);
            }
        }
    }
}
