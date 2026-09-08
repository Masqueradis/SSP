using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.NETCore.Client;

namespace HeapDumpSample
{
    class Program
    {
        static string dumpDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Dumps"
        );

        static void Main(string[] args)
        {
            try
            {
                Directory.CreateDirectory(dumpDirectory);

                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var client = new DiagnosticsClient(processId);

                Console.WriteLine($"=== Создание снимков кучи процесса {processId} ===\n");

                Console.WriteLine("Этап 1. Разогреваем память (List<byte[]> с данными).\n");
                var stage1 = new List<byte[]>();
                for (int i = 0; i < 200; i++) stage1.Add(new byte[1024 * 10]);

                string dump1 = Capture(client, "heap_dump_stage1");
                Console.WriteLine($"✅ Снимок 1 сохранён: {dump1}\n");

                Console.WriteLine("Этап 2. Добавляем большой массив (10 МБ) и 50 объектов LOH (> 85 КБ).\n");
                var bigArray = new byte[10 * 1024 * 1024];
                var lohObjects = new List<byte[]>();
                for (int i = 0; i < 50; i++) lohObjects.Add(new byte[100000 + i]);

                string dump2 = Capture(client, "heap_dump_stage2");
                Console.WriteLine($"✅ Снимок 2 сохранён: {dump2}\n");

                Console.WriteLine("Первый снимок сделан ДО выделения большого массива,");
                Console.WriteLine("второй — ПОСЛЕ. Их можно сравнить утилитой DumpReader.");

                DirectoryInfo info = new DirectoryInfo(dumpDirectory);
                foreach (var f in info.GetFiles("*.dump"))
                {
                    Console.WriteLine($"- {f.Name}: {f.Length / 1024.0:F2} KB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при создании снимка кучи: {ex.Message}");
            }
        }

        static string Capture(DiagnosticsClient client, string name)
        {
            string dumpPath = Path.Combine(dumpDirectory, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.dump");
            Console.WriteLine($"Создание снимка кучи...");
            Console.WriteLine($"Файл будет сохранён: {dumpPath}");
            client.WriteDump(DumpType.WithHeap, dumpPath, logDumpGeneration: true);
            var fileInfo = new FileInfo(dumpPath);
            double fileSizeKB = fileInfo.Length / 1024.0;
            Console.WriteLine($"Размер файла: {fileSizeKB:F2} KB");
            return dumpPath;
        }
    }
}