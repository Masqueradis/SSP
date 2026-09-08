using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class BuggyCode
{
    private static List<IDisposable> _cache = new List<IDisposable>();
    private static event EventHandler? _globalEvent;
    private byte[] _data;

    public BuggyCode(int dataSizeKB)
    {
        _data = new byte[dataSizeKB * 1024];
        _globalEvent += OnEvent;
        _cache.Add(new MemoryStream(1024));
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        var temp = new byte[1024 * 100];
    }

    public static int CacheCount => _cache.Count;

    public static void CreateInstances(int count, int sizeKB)
    {
        for (int i = 0; i < count; i++)
        {
            new BuggyCode(sizeKB);
        }
    }

    public static void FireEvent() => _globalEvent?.Invoke(null, EventArgs.Empty);
}

public class FixedBuggyCode : IDisposable
{
    private static List<IDisposable> _cache = new List<IDisposable>();
    private static event EventHandler? _globalEvent;
    private byte[] _data;
    private bool _disposed;

    public static int AliveSubscribers { get; private set; }

    public FixedBuggyCode(int dataSizeKB)
    {
        _data = new byte[dataSizeKB * 1024];
        _globalEvent += OnEvent;
        AliveSubscribers++;
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        var temp = new byte[1024 * 100];
    }

    public static void CreateInstances(int count, int sizeKB)
    {
        for (int i = 0; i < count; i++)
        {
            new FixedBuggyCode(sizeKB);
        }
    }

    public static int CacheCount => _cache.Count;

    public static void FireEvent() => _globalEvent?.Invoke(null, EventArgs.Empty);

    public static void OpenCachedStream()
    {
        _cache.Add(new MemoryStream(1024));
    }

    public static void ClearCache()
    {
        foreach (var item in _cache) item.Dispose();
        _cache.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _globalEvent -= OnEvent;
        _disposed = true;
        AliveSubscribers--;
    }
}

class Program
{
    const int Count = 400;
    const int SizeKB = 256;

    static void Main()
    {
        Console.WriteLine("=== Задание 7. Поиск утечек в коде (BuggyCode) ===\n");

        Console.WriteLine("Найденные места утечек и их причины:\n");
        Console.WriteLine("1. static List<IDisposable> _cache — кэш растёт без ограничений и никогда не очищается,");
        Console.WriteLine("   каждый MemoryStream удерживается до конца работы приложения.");
        Console.WriteLine("2. static event _globalEvent — конструктор подписывает OnEvent() и никогда не отписывает,");
        Console.WriteLine("   делегат хранит ссылку на экземпляр -> ни один BuggyCode не может быть собран GC.");
        Console.WriteLine("3. new MemoryStream(1024) добавляется в кэш без вызова Dispose() — утечка неуправляемых");
        Console.WriteLine("   (Native) ресурсов даже после удаления элемента из списка.");
        Console.WriteLine("4. CreateInstances() создаёт объекты, которые никто не сохраняет, но каждый удерживается");
        Console.WriteLine("   статическим событием и статическим кэшем — формально это «бесплатный» рост памяти.\n");

        Console.WriteLine($"Демонстрация (Count={Count}, размер данных {SizeKB} КБ; по условию 1000 x 1 МБ):\n");

        ShowLeakyVersion();
        ShowFixedVersion();

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }

    static void ShowLeakyVersion()
    {
        Console.WriteLine("=== ОРИГИНАЛЬНЫЙ код (с утечками) ===");
        long before = GC.GetTotalMemory(true);
        Console.WriteLine($"Память до создания: {before / (1024 * 1024)} MB");

        BuggyCode.CreateInstances(Count, SizeKB);
        BuggyCode.FireEvent();

        long after = GC.GetTotalMemory(true);
        Console.WriteLine($"Создали {Count} экземпляров (без сохранения ссылок).");
        Console.WriteLine($"Элементов в статическом кэше: {BuggyCode.CacheCount:N0}");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long afterCollect = GC.GetTotalMemory(true);
        Console.WriteLine($"Память после создания: {afterCollect / (1024 * 1024)} MB");
        Console.WriteLine($"Прирост: {(afterCollect - before) / (1024 * 1024)} MB");
        Console.WriteLine("=> Память НЕ освободилась: экземпляры удерживаются статическим событием и кэшем.\n");
    }

    static void ShowFixedVersion()
    {
        Console.WriteLine("=== ИСПРАВЛЕННЫЙ код ===");
        long before = GC.GetTotalMemory(true);
        Console.WriteLine($"Память до создания: {before / (1024 * 1024)} MB");

        var instances = new List<FixedBuggyCode>();
        for (int i = 0; i < Count; i++) instances.Add(new FixedBuggyCode(SizeKB));
        FixedBuggyCode.FireEvent();
        FixedBuggyCode.OpenCachedStream();

        long after = GC.GetTotalMemory(true);
        Console.WriteLine($"Память после создания: {after / (1024 * 1024)} MB (живых подписчиков: {FixedBuggyCode.AliveSubscribers})");

        foreach (var instance in instances) instance.Dispose();
        instances.Clear();
        FixedBuggyCode.ClearCache();

        long afterDispose = GC.GetTotalMemory(true);
        Console.WriteLine($"Память после Dispose всех + ClearCache + GC.Collect: {afterDispose / (1024 * 1024)} MB");
        Console.WriteLine($"Освобождено: {(after - afterDispose) / (1024 * 1024)} MB, живых подписчиков: {FixedBuggyCode.AliveSubscribers}");
        Console.WriteLine("=> Исправления: отписка в Dispose(), Dispose() всех элементов кэша и очистка кэша.");
    }
}