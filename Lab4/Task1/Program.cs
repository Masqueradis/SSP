using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lab4Task1;

static class MemoryInfo
{
    public static void Print(string title, long totalBytes = -1)
    {
        if (totalBytes < 0)
        {
            Console.WriteLine($"--- {title} ---");
        }
        else
        {
            Console.WriteLine($"--- {title}: {totalBytes / 1024.0 / 1024.0:F2} МБ ---");
        }
        Console.WriteLine($"  Сборок: Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
        using Process p = Process.GetCurrentProcess();
        p.Refresh();
        Console.WriteLine($"  Working Set: {p.WorkingSet64 / 1024.0 / 1024.0:F2} МБ, Private: {p.PrivateMemorySize64 / 1024.0 / 1024.0:F2} МБ");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FullCollect()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }
}

class StaticLeakDemo
{
    private static readonly List<byte[]> _staticCache = new();
    private readonly List<byte[]> _instanceCache = new();

    public const int ObjectSize = 10 * 1024;
    public const int ObjectCount = 1000;

    public static int StaticCount => _staticCache.Count;
    public int InstanceCount => _instanceCache.Count;

    public static void AddStaticForDemo(int value) => _staticCache.Add(BitConverter.GetBytes(value));
    public void AddInstanceForDemo(int value) => _instanceCache.Add(BitConverter.GetBytes(value));
    public static IEnumerable<int> StaticItems() => _staticCache.Select(b => BitConverter.ToInt32(b, 0));
    public IEnumerable<int> InstanceItems() => _instanceCache.Select(b => BitConverter.ToInt32(b, 0));

    public (WeakReference instanceWitness, WeakReference staticWitness) CreateLeak()
    {
        for (int i = 0; i < ObjectCount; i++)
        {
            _staticCache.Add(new byte[ObjectSize]);
        }

        for (int i = 0; i < ObjectCount; i++)
        {
            _instanceCache.Add(new byte[ObjectSize]);
        }

        return (new WeakReference(_instanceCache[0]), new WeakReference(_staticCache[0]));
    }

    public static void ClearStatic()
    {
        _staticCache.Clear();
    }

    public void ClearInstance()
    {
        _instanceCache.Clear();
    }
}

sealed class EventLeakDemo
{
    public event EventHandler? DataReceived;

    private readonly List<EventHandler> _handlers = new();
    private readonly List<Subscriber> _subscribers = new();

    public int HandlerCount => _handlers.Count;

    public WeakReference Subscribe(int id)
    {
        var subscriber = new Subscriber(id, this);
        EventHandler handler = (sender, e) => subscriber.OnData(e);

        DataReceived += handler;
        _handlers.Add(handler);
        _subscribers.Add(subscriber);
        subscriber.KeepAlive = false;

        return new WeakReference(subscriber);
    }

    public void Raise()
    {
        DataReceived?.Invoke(this, new EventArgs());
    }

    public void UnsubscribeAll()
    {
        foreach (EventHandler handler in _handlers)
        {
            DataReceived -= handler;
        }
        _handlers.Clear();
        _subscribers.Clear();
    }
}

sealed class Subscriber
{
    private readonly byte[] _cache = new byte[100 * 1024];

    public Subscriber(int id, EventLeakDemo source)
    {
        Id = id;
        Source = source;
    }

    public int Id { get; }
    public EventLeakDemo Source { get; }
    public bool KeepAlive { get; set; }
    public int Received { get; private set; }
    public int CacheSize => _cache.Length;

    public void OnData(EventArgs e)
    {
        Received++;
        byte[] payload = new byte[100 * 1024];
        payload[0] = (byte)Id;
    }
}

static class Program
{
    static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 78));
    }

    static void SubHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"### {text}");
    }

    static void StaticFieldVsInstanceField()
    {
        Header("ЧАСТЬ 0. Статическое поле против экземплярного поля");

        var first = new StaticLeakDemo();
        var second = new StaticLeakDemo();

        StaticLeakDemo.AddStaticForDemo(111);
        first.AddInstanceForDemo(1);
        StaticLeakDemo.AddStaticForDemo(222);
        second.AddInstanceForDemo(2);

        Console.WriteLine($"Экземпляр 1 -> статический список: [{string.Join(",", StaticLeakDemo.StaticItems())}]");
        Console.WriteLine($"Экземпляр 2 -> статический список: [{string.Join(",", StaticLeakDemo.StaticItems())}]");
        Console.WriteLine($"Экземпляр 1 -> свой список:       [{string.Join(",", first.InstanceItems())}]");
        Console.WriteLine($"Экземпляр 2 -> свой список:       [{string.Join(",", second.InstanceItems())}]");
        Console.WriteLine("Вывод: статический список ОДИН на все экземпляры, экземплярные списки разные.");

        StaticLeakDemo.ClearStatic();
        first.ClearInstance();
        second.ClearInstance();
    }

    static void Main()
    {
        Console.WriteLine("ЛАБОРАТОРНАЯ РАБОТА 4 — ЗАДАНИЕ 1: ИССЛЕДОВАНИЕ УТЕЧЕК ПАМЯТИ");
        Console.WriteLine($"Среда: {Environment.Version}  |  Рабочие процессоры: {Environment.ProcessorCount}");
        Console.WriteLine($"Серверный GC: {System.Runtime.GCSettings.IsServerGC}  |  Латентность: {System.Runtime.GCSettings.LatencyMode}");

        StaticFieldVsInstanceField();

        StaticLeak();
        EventLeak();
        Summary();

        Console.WriteLine();
        Console.WriteLine("Нажмите любую клавишу...");
        try { Console.ReadKey(); } catch { }
    }

    static void StaticLeak()
    {
        Header("ЗАДАНИЕ 1.1 — СТАТИЧЕСКАЯ УТЕЧКА (1000 объектов по 10 КБ)");

        var demo = new StaticLeakDemo();

        MemoryInfo.FullCollect();
        long baseline = GC.GetTotalMemory(true);
        MemoryInfo.Print("Старт (после полной сборки)", baseline);

        SubHeader($"Создаём {StaticLeakDemo.ObjectCount} объектов по {StaticLeakDemo.ObjectSize / 1024} КБ");
        (WeakReference instanceWitness, WeakReference staticWitness) = demo.CreateLeak();
        Console.WriteLine($"В статическом списке: {StaticLeakDemo.StaticCount} элементов, в экземплярном: {demo.InstanceCount}");

        MemoryInfo.FullCollect();
        long afterGc = GC.GetTotalMemory(true);
        MemoryInfo.Print("После GC.Collect() — прирост отн. старта", afterGc - baseline);
        Console.WriteLine($"Ожидаемый прирост данных: {StaticLeakDemo.ObjectCount * StaticLeakDemo.ObjectSize / 1024.0 / 1024.0:F2} МБ");

        SubHeader("Проверяем достижимость объектов через WeakReference");
        Console.WriteLine($"  Элемент СТАТИЧЕСКОГО списка жив после полного GC: {staticWitness.IsAlive}  <- статический корень удерживает объект");
        Console.WriteLine($"  Элемент ЭКЗЕМПЛЯРНОГО списка жив после полного GC: {instanceWitness.IsAlive}");

        SubHeader("Очищаем статический список и повторяем сборку");
        StaticLeakDemo.ClearStatic();
        demo.ClearInstance();
        MemoryInfo.FullCollect();
        long afterClear = GC.GetTotalMemory(true);
        MemoryInfo.Print("После Clear() и полной сборки", afterClear - baseline);
        Console.WriteLine($"  Элемент СТАТИЧЕСКОГО списка жив после Clear(): {staticWitness.IsAlive}  <- объект собран, память освобождена");
        Console.WriteLine($"  Элемент ЭКЗЕМПЛЯРНОГО списка жив после Clear(): {instanceWitness.IsAlive}");

        Console.WriteLine("Вывод: статический список — корень GC, поэтому 1000 объектов по 10 КБ");
        Console.WriteLine("       (около 9,8 МБ) остаются в куче даже после принудительной сборки.");
    }

    static void EventLeak()
    {
        Header("ЗАДАНИЕ 1.2 — СОБЫТИЙНАЯ УТЕЧКА (100 подписок на событие)");

        var demo = new EventLeakDemo();

        MemoryInfo.FullCollect();
        long baseline = GC.GetTotalMemory(true);
        MemoryInfo.Print("Старт (после полной сборки)", baseline);

        SubHeader("Подписываем 100 обработчиков; каждый подписчик удерживает кэш 100 КБ");
        var witnesses = new List<WeakReference>();
        for (int i = 0; i < 100; i++)
        {
            witnesses.Add(demo.Subscribe(i + 1));
        }
        Console.WriteLine($"Обработчиков в списке отписки: {demo.HandlerCount}");
        Console.WriteLine($"Удерживается кэшем подписчиков: {100 * 100 / 1024.0:F2} МБ");

        SubHeader("Генерируем событие (обработчик разово выделяет 100 КБ) и запускаем GC");
        for (int i = 0; i < 10; i++)
        {
            demo.Raise();
        }
        MemoryInfo.FullCollect();
        long afterGc = GC.GetTotalMemory(true);
        MemoryInfo.Print("После событий и GC.Collect() — прирост отн. старта", afterGc - baseline);
        Console.WriteLine($"  Подписчик №1 жив после полного GC: {witnesses[0].IsAlive}  <- делегат события удерживает подписчика");
        Console.WriteLine($"  Подписчик №100 жив после полного GC: {witnesses[99].IsAlive}");

        SubHeader("Отписываемся (UnsubscribeAll) и повторяем сборку");
        demo.UnsubscribeAll();
        MemoryInfo.FullCollect();
        long afterUnsubscribe = GC.GetTotalMemory(true);
        MemoryInfo.Print("После отписки и полной сборки — прирост отн. старта", afterUnsubscribe - baseline);
        Console.WriteLine($"  Подписчик №1 жив после отписки: {witnesses[0].IsAlive}  <- собран");
        Console.WriteLine($"  Подписчик №100 жив после отписки: {witnesses[99].IsAlive}  <- собран");

        Console.WriteLine("Вывод: подписка на событие создаёт двустороннюю ссылку (источник -> делегат -> подписчик).");
        Console.WriteLine("       Без отписки статический или долгоживущий источник события удерживает все подписчики.");
    }

    static void Summary()
    {
        Header("ИТОГ ПО ЗАДАНИЮ 1");

        Console.WriteLine("1) Статический список:");
        Console.WriteLine("   поле static — корень GC на всё время работы процесса;");
        Console.WriteLine("   объекты из списка достижимы, поэтому GC.Collect() их не удаляет.");
        Console.WriteLine();
        Console.WriteLine("2) Событие:");
        Console.WriteLine("   компилятор превращает += в хранение делегата в поле источника;");
        Console.WriteLine("   делегат удерживает подписчика, поэтому без -= подписчик не собирается.");
        Console.WriteLine();
        Console.WriteLine("3) Признак утечки в обоих случаях один:");
        Console.WriteLine("   объект вне досягаемости из кода, но IsAlive == true после полного GC.");
        Console.WriteLine("   Лечится только разрывом ссылки: Clear(), -= или WeakReference вместо сильной ссылки.");
    }
}
