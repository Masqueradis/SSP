using System;
using System.Collections.Generic;
using System.Diagnostics;

public class StaticLeakDemo
{
    private static List<byte[]> _staticList = new List<byte[]>();

    public static void Reset() => _staticList.Clear();

    public static void AddData()
    {
        for (int i = 0; i < 1000; i++)
        {
            _staticList.Add(new byte[1024 * 10]);
        }
    }

    public static void CheckMemory(string message = "")
    {
        if (!string.IsNullOrEmpty(message)) Console.WriteLine($"--- {message} ---");
        Console.WriteLine($"Текущий размер памяти: {GC.GetTotalMemory(true) / (1024 * 1024)} MB");
        Console.WriteLine($"Количество объектов в статическом списке: {_staticList.Count:N0}");
        Console.WriteLine($"Поколение статического списка: {GC.GetGeneration(_staticList)}");
    }
}

public class EventSubscriber
{
    private byte[] _data;
    public string Name { get; }

    public EventSubscriber(int id, int sizeInKB)
    {
        Name = $"Подписчик_{id}";
        _data = new byte[sizeInKB * 1024];
    }

    public void HandleData(object? sender, EventArgs e)
    {
        var temp = new byte[1024];
    }
}

public class EventLeakDemo
{
    public event EventHandler? DataEvent;

    public void Subscribe(EventHandler handler) => DataEvent += handler;
    public void Unsubscribe(EventHandler handler) => DataEvent -= handler;
}

public class GenerationComparison
{
    public static void Compare()
    {
        Console.WriteLine("\n=== Сравнение поколений объектов ===");

        var obj = new object();
        Console.WriteLine($"Объект сразу после создания: поколение {GC.GetGeneration(obj)}");

        for (int i = 1; i <= 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine($"После GC.Collect() #{i}: поколение {GC.GetGeneration(obj)}");
        }

        Console.WriteLine("\nОбъекты разных размеров (определяем поколение):");
        int[] sizes = { 1024 * 10, 1024 * 50, 90000, 200000 };
        var holders = new List<byte[]>();
        foreach (int size in sizes)
        {
            var data = new byte[size];
            holders.Add(data);
            string note = size > 85000 ? "  -> размер > 85 КБ, сразу в LOH (поколение 2)" : "";
            Console.WriteLine($"размер {size / 1024.0:F0} КБ: поколение {GC.GetGeneration(data)}{note}");
        }
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Задание 4. Исследование статических ссылок и утечек памяти ===\n");

        Console.WriteLine("Часть 1. Статическая утечка.");
        StaticLeakDemo.CheckMemory("До создания объектов");

        StaticLeakDemo.AddData();
        StaticLeakDemo.CheckMemory("После AddData() (1000 x 10 КБ)");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("\nПосле GC.Collect() + WaitForPendingFinalizers:");
        StaticLeakDemo.CheckMemory();
        Console.WriteLine("=> Объекты НЕ удалены: статический список — корень GC, живёт до завершения программы.");

        StaticLeakDemo.Reset();
        GC.Collect();
        Console.WriteLine("\nПосле Reset() + GC.Collect():");
        StaticLeakDemo.CheckMemory();
        Console.WriteLine("=> Память освобождена после очистки статической коллекции.");

        Console.WriteLine("\n\nЧасть 2. Событийная утечка (неотписанные события).");
        RunEventDemo();

        GenerationComparison.Compare();

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }

    static void RunEventDemo()
    {
        var publisher = new EventLeakDemo();
        var subscribers = new List<EventSubscriber>();
        var handlers = new List<EventHandler>();

        for (int i = 0; i < 100; i++)
        {
            var subscriber = new EventSubscriber(i, 256);
            subscribers.Add(subscriber);
            handlers.Add(subscriber.HandleData);
            publisher.Subscribe(subscriber.HandleData);
        }

        ForceCollect();
        Console.WriteLine($"Память после 100 подписок по 256 КБ: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Working Set с подписками: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine($"Кол-во подписчиков: {subscribers.Count}");

        foreach (var handler in handlers) publisher.Unsubscribe(handler);

        subscribers.Clear();
        handlers.Clear();
        ForceCollect();
        Console.WriteLine($"\nПамять после отписки всех: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Working Set после отписки: {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB");
        Console.WriteLine("=> Пока есть подписка, событие держит ссылку на обработчик (и объект-подписчик),");
        Console.WriteLine("   GC не может его собрать. После отписки объекты становятся недостижимыми и удаляются.");
    }

    static void ForceCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}