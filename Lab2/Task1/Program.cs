using System;
using System.Collections.Generic;
using System.Diagnostics;

public class StaticReferenceDemo
{
    private static List<int> _staticList = new List<int>();
    private List<int> _instanceList = new List<int>();

    public void AddToStatic(int value) => _staticList.Add(value);
    public void AddToInstance(int value) => _instanceList.Add(value);

    public void PrintLists(string objectName)
    {
        Console.WriteLine($"{objectName}:");
        Console.WriteLine($"  Статический список (общий): [{string.Join(", ", _staticList)}]");
        Console.WriteLine($"  Экземплярный список (свой): [{string.Join(", ", _instanceList)}]");
        Console.WriteLine();
    }
}

public class StaticFieldDemo
{
    private static int _staticCounter = 0;
    private int _instanceCounter = 0;

    public void Increment()
    {
        _staticCounter++;
        _instanceCounter++;
    }

    public void ShowCounters(string objectName)
    {
        Console.WriteLine($"{objectName}:");
        Console.WriteLine($"  Статический счетчик (общий): {_staticCounter}");
        Console.WriteLine($"  Экземплярный счетчик (свой): {_instanceCounter}");
    }

    public static void ShowStaticFieldDemo()
    {
        var objA = new StaticFieldDemo();
        var objB = new StaticFieldDemo();

        objA.Increment();
        objA.Increment();
        objB.Increment();

        Console.WriteLine("После операций:");
        objA.ShowCounters("Объект A");
        objB.ShowCounters("Объект B");
        Console.WriteLine("=> Статический счетчик ОДИН для обоих объектов, экземплярные РАЗНЫЕ.");
    }
}

public class MemoryLeakExample
{
    private static List<byte[]> _staticList = new List<byte[]>();

    public static void CreateMemoryLeak()
    {
        for (int i = 0; i < 1000; i++)
        {
            _staticList.Add(new byte[1024 * 10]);
        }
    }

    public static void ClearMemoryLeak()
    {
        _staticList.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}

public class EventLeakExample
{
    public event EventHandler BigEvent;

    public void SubscribeLeak()
    {
        BigEvent += (sender, e) =>
        {
            var data = new byte[1024 * 100];
        };
    }
}

public class MemoryMonitor
{
    private List<byte[]> _allocatedMemory = new List<byte[]>();

    public void AllocateMemory(int sizeInMB)
    {
        _allocatedMemory.Add(new byte[sizeInMB * 1024 * 1024]);
    }

    public void AllocateLOHObjects(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var largeObject = new byte[90000 + i];
        }
    }

    public void SimulateBoxing(int count = 1000000)
    {
        var list = new List<object>();
        for (int i = 0; i < count; i++)
        {
            list.Add(i);
        }
    }

    public void PrintMemoryInfo(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"\n=== {message} ===");
        Console.WriteLine($"Total Memory: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}");
        using (Process process = Process.GetCurrentProcess())
        {
            Console.WriteLine($"Working Set: {process.WorkingSet64 / (1024 * 1024)} MB");
        }
    }

    public void Cleanup()
    {
        _allocatedMemory.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Задание 1. Базовые примеры утечки памяти ===");

        Console.WriteLine("\nЧасть 1. Наглядная демонстрация: статическая ссылка — ОДНА на все объекты.\n");
        var obj1 = new StaticReferenceDemo();
        var obj2 = new StaticReferenceDemo();
        var obj3 = new StaticReferenceDemo();

        obj1.AddToStatic(100);
        obj1.AddToInstance(1);
        obj2.AddToStatic(200);
        obj2.AddToInstance(2);
        obj3.AddToStatic(300);
        obj3.AddToInstance(3);

        obj1.PrintLists("Объект 1");
        obj2.PrintLists("Объект 2");
        obj3.PrintLists("Объект 3");

        Console.WriteLine("=> Статический список у всех объектов ОДИН, экземплярные списки РАЗНЫЕ.\n");
        StaticFieldDemo.ShowStaticFieldDemo();

        Console.WriteLine("\nЧасть 2. Проверка утечек: статическая коллекция, событие, LOH, boxing.\n");
        var monitor = new MemoryMonitor();

        Console.WriteLine("1. Выделение памяти (10 МБ + 100 объектов LOH):");
        monitor.AllocateMemory(10);
        monitor.AllocateLOHObjects(100);
        monitor.PrintMemoryInfo("После выделения");

        Console.WriteLine("\n2. Тест утечек:");
        MemoryLeakExample.CreateMemoryLeak();
        var eventExample = new EventLeakExample();
        for (int i = 0; i < 100; i++) eventExample.SubscribeLeak();
        monitor.PrintMemoryInfo("После утечек");

        Console.WriteLine("\n3. Тест упаковки:");
        var sw = Stopwatch.StartNew();
        monitor.SimulateBoxing(1000000);
        sw.Stop();
        Console.WriteLine($"Время с упаковкой (List<object>): {sw.ElapsedMilliseconds}ms");

        sw.Restart();
        var optimizedList = new List<int>();
        for (int i = 0; i < 1000000; i++) optimizedList.Add(i);
        sw.Stop();
        Console.WriteLine($"Время без упаковки (List<int>):  {sw.ElapsedMilliseconds}ms");

        Console.WriteLine("\n4. После очистки:");
        MemoryLeakExample.ClearMemoryLeak();
        monitor.Cleanup();
        monitor.PrintMemoryInfo("Очищено");

        Console.WriteLine("\n5. Поколения объектов:");
        var obj = new object();
        Console.WriteLine($"Начальное поколение объекта: {GC.GetGeneration(obj)}");
        GC.Collect();
        Console.WriteLine($"После GC.Collect():          {GC.GetGeneration(obj)}");

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }
}