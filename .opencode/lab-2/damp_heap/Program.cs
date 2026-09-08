using System;
using System.IO;
using Microsoft.Diagnostics.NETCore.Client;

namespace HeapDumpSample
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var client = new DiagnosticsClient(processId);

                string dumpDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Dumps"
                );

                Directory.CreateDirectory(dumpDirectory);

                string dumpPath = Path.Combine(
                    dumpDirectory,
                    $"heap_dump_{DateTime.Now:yyyyMMdd_HHmmss}.dump"
                );

                Console.WriteLine($"Создание снимка кучи процесса {processId}...");
                Console.WriteLine($"Файл будет сохранён: {dumpPath}");

                client.WriteDump(DumpType.WithHeap, dumpPath, logDumpGeneration: true);

                Console.WriteLine($"✅ Снимок кучи успешно создан: {dumpPath}");

                var fileInfo = new FileInfo(dumpPath);
                double fileSizeKB = fileInfo.Length / 1024.0;
                Console.WriteLine($"Размер файла: {fileSizeKB:F2} KB");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при создании снимка кучи: {ex.Message}");
            }
        }
    }
}
