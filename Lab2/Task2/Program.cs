using System;
using System.Collections.Generic;
using System.Diagnostics;

public class MemoryMonitor : IDisposable
{
    private bool _disposed;
    private List<byte[]> _allocatedMemory = new List<byte[]>();
    private Random _random = new Random();

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

    public void PrintMemoryInfo()
    {
        Console.WriteLine($"GC Generation: {GC.GetGeneration(this)}");
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
    static void Main()
    {
        Console.WriteLine("=== Задание 2. MemoryMonitor : IDisposable ===");

        using (var monitor = new MemoryMonitor())
        {
            Console.WriteLine("\nШаг 1. Выделяем 4 МБ и 10 объектов LOH (> 85 KБ).");
            monitor.AllocateMemory(4);
            monitor.AllocateLOHObjects(10);

            Console.WriteLine("Шаг 2. Упаковка: 1 000 000 значений int в List<object>.");
            monitor.SimulateBoxing();

            Console.WriteLine("\nШаг 3. Информация о памяти до очистки:");
            monitor.PrintMemoryInfo();

            Console.WriteLine("\nШаг 4. Выходим из using — будет вызван Dispose(), память очищена.");
        }

        Console.WriteLine("\nПроверка повторного вызова Dispose() (должен быть безопасным):");
        var second = new MemoryMonitor();
        second.Dispose();
        second.Dispose();
        Console.WriteLine("Повторный Dispose() выполнен без исключений.");

        Process process = Process.GetCurrentProcess();
        Console.WriteLine($"Working Set: {process.WorkingSet64 / (1024 * 1024)} MB");
    }
}