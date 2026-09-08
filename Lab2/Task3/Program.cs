using System;
using System.Collections.Generic;
using System.Diagnostics;

public class MemoryMonitor : IDisposable
{
    private bool _disposed;
    private List<byte[]> _allocatedMemory = new List<byte[]>();

    public void AllocateMemory(int sizeInMB)
    {
        var data = new byte[sizeInMB * 1024 * 1024];
        _allocatedMemory.Add(data);
    }

    public void AllocateLOHObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var largeObject = new byte[90000 + i];
        }
    }

    public void SimulateBoxing()
    {
        var list = new List<object>();
        for (int i = 0; i < 1000000; i++)
        {
            list.Add(i);
        }
    }

    public void PrintMemoryInfo(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"--- {message} ---");
        Console.WriteLine($"GC Generation (this): {GC.GetGeneration(this)}");
        Console.WriteLine($"Total Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Collection Count Gen0: {GC.CollectionCount(0)}");
        Console.WriteLine($"Collection Count Gen1: {GC.CollectionCount(1)}");
        Console.WriteLine($"Collection Count Gen2: {GC.CollectionCount(2)}");
    }

    public void Cleanup()
    {
        _allocatedMemory.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Cleanup();
            _disposed = true;
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Задание 3. Тестирование и анализ управления памятью ===");

        using (var monitor = new MemoryMonitor())
        {
            Console.WriteLine("\n1. Базовое выделение памяти:");
            monitor.AllocateMemory(10);
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n2. Создание объектов в LOH:");
            monitor.AllocateLOHObjects(100);
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n3. Тест упаковки:");
            var stopwatch = Stopwatch.StartNew();
            monitor.SimulateBoxing();
            stopwatch.Stop();
            Console.WriteLine($"Время выполнения с упаковкой (List<object>): {stopwatch.ElapsedMilliseconds}ms");

            Console.WriteLine("\n4. После принудительной сборки:");
            monitor.Cleanup();
            monitor.PrintMemoryInfo();

            Console.WriteLine("\n5. Оптимизированная версия (без упаковки):");
            stopwatch.Restart();
            var optimizedList = new List<int>();
            for (int i = 0; i < 1000000; i++)
            {
                optimizedList.Add(i);
            }
            stopwatch.Stop();
            Console.WriteLine($"Время выполнения без упаковки (List<int>):  {stopwatch.ElapsedMilliseconds}ms");
        }

        Console.WriteLine("\nАнализ: варианты с упаковкой и без упаковки; после Cleanup() память");
        Console.WriteLine("освобождается, выборки по поколениям показывают работу сборщика мусора.");

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }
}