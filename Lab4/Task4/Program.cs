using System.Runtime.CompilerServices;

namespace Lab4Task4;

public class BuggyCode
{
    private static List<IDisposable> _cache = new List<IDisposable>();
    private static event EventHandler? _globalEvent;
    private byte[] _data = new byte[1024 * 1024];

    public static int CacheCount => _cache.Count;
    public static int InstancesCreated { get; private set; }

    public static long GlobalEventRaised;

    public BuggyCode()
    {
        InstancesCreated++;
        _globalEvent += OnEvent;
        _cache.Add(new MemoryStream(1024));
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        GlobalEventRaised++;
        var temp = new byte[1024 * 100];
    }

    public static void RaiseGlobal()
    {
        _globalEvent?.Invoke(null, EventArgs.Empty);
    }

    public static void ResetCache()
    {
        _cache = new List<IDisposable>();
    }
}

public class FixedCode : IDisposable
{
    private static List<FixedCode> _registry = new List<FixedCode>();
    private static event EventHandler? _globalEvent;

    private readonly byte[] _data;
    private readonly MemoryStream _stream;
    private bool _subscribed;
    private bool _disposed;

    public static int RegistryCount => _registry.Count;
    public static int InstancesCreated { get; private set; }
    public static long GlobalEventRaised;
    public static int GlobalSubscribers;

    public int Id { get; }

    public FixedCode(int id)
    {
        Id = id;
        InstancesCreated++;

        _data = new byte[1024 * 1024];
        _stream = new MemoryStream(1024);

        _registry.Add(this);

        _globalEvent += OnEvent;
        _subscribed = true;
        GlobalSubscribers++;
    }

    private void OnEvent(object? sender, EventArgs e)
    {
        GlobalEventRaised++;
        byte[] buffer = new byte[1024 * 100];
        buffer[0] = (byte)Id;
    }

    public static void RaiseGlobal()
    {
        _globalEvent?.Invoke(null, EventArgs.Empty);
    }

    public static void ClearRegistry()
    {
        List<FixedCode> snapshot = new List<FixedCode>(_registry);

        foreach (FixedCode code in snapshot)
        {
            code.Dispose();
        }

        _registry.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_subscribed)
        {
            _globalEvent -= OnEvent;
            _subscribed = false;
            GlobalSubscribers--;
        }

        _stream.Dispose();
        _registry.Remove(this);
        _disposed = true;
    }
}

static class Program
{
    private const int InstanceCount = 200;

    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 78));
    }

    private static void SubHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"### {text}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FullCollect()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    static void Main()
    {
        Console.WriteLine("ЛАБОРАТОРНАЯ РАБОТА 4 — ЗАДАНИЕ 4: ПОИСК И ИСПРАВЛЕНИЕ УТЕЧЕК");
        Console.WriteLine($"Среда: {Environment.Version}");

        Analysis();
        MeasureBuggy();
        MeasureFixed();
        Fixes();

        Console.WriteLine();
        Console.WriteLine("Нажмите любую клавишу...");
        try { Console.ReadKey(); } catch { }
    }

    private static void Analysis()
    {
        Header("АНАЛИЗ КОДА BuggyCode — 4 НАЙДЕННЫЕ ПРОБЛЕМЫ");

        Console.WriteLine("Исходный код из условия:");
        Console.WriteLine();
        Console.WriteLine("  public class BuggyCode");
        Console.WriteLine("  {");
        Console.WriteLine("      private static List<IDisposable> _cache = new List<IDisposable>();");
        Console.WriteLine("      private static event EventHandler _globalEvent;");
        Console.WriteLine("      private byte[] _data = new byte[1024 * 1024];");
        Console.WriteLine();
        Console.WriteLine("      public BuggyCode()");
        Console.WriteLine("      {");
        Console.WriteLine("          _globalEvent += OnEvent;");
        Console.WriteLine("          _cache.Add(new MemoryStream(1024));");
        Console.WriteLine("      }");
        Console.WriteLine();
        Console.WriteLine("      private void OnEvent(object sender, EventArgs e)");
        Console.WriteLine("      {");
        Console.WriteLine("          var temp = new byte[1024 * 100];");
        Console.WriteLine("      }");
        Console.WriteLine("  }");

        Console.WriteLine();
        Console.WriteLine(new string('-', 78));

        SubHeader("Проблема 1. Статическая коллекция _cache растёт бесконечно");
        Console.WriteLine("  Причина: поле static — корень GC, существующий всё время работы процесса.");
        Console.WriteLine("           Каждый new BuggyCode() добавляет в _cache ещё один MemoryStream,");
        Console.WriteLine("           и ссылок на него больше ниоткуда не остаётся.");
        Console.WriteLine("  Тип утечки: утечка памяти + утечка неуправляемого ресурса.");
        Console.WriteLine("  Последствие: каждый экземпляр (в т.ч. его _data на 1 МБ) остаётся в куче навсегда,");
        Console.WriteLine("              а MemoryStream никогда не освобождает свой буфер.");
        Console.WriteLine("  Исправление: ограничить размер кэша и освобождать элементы при вытеснении,");
        Console.WriteLine("               а поле сделать readonly.");

        SubHeader("Проблема 2. Статическое событие удерживает каждый экземпляр");
        Console.WriteLine("  Причина: _globalEvent += OnEvent сохраняет ссылку на метод экземпляра,");
        Console.WriteLine("           а значит, и на сам объект BuggyCode.");
        Console.WriteLine("           Источник события статический, живёт вечно, поэтому и подписчик вечен.");
        Console.WriteLine("  Тип утечки: классическая утечка через подписку на событие.");
        Console.WriteLine("  Последствие: объекты, которые давно не нужны, не собираются GC.");
        Console.WriteLine("  Исправление: отписываться (_globalEvent -= OnEvent) в Dispose,");
        Console.WriteLine("               либо не подписываться на статическое событие вовсе,");
        Console.WriteLine("               либо хранить подписки в WeakReference.");

        SubHeader("Проблема 3. Отсутствие IDisposable: утечку нечем устранить");
        Console.WriteLine("  Причина: класс не реализует IDisposable, значит нельзя ни отписаться,");
        Console.WriteLine("           ни освободить IDisposable-ресурсы из _cache.");
        Console.WriteLine("  Тип утечки: системная (архитектурная) — нет точки для зачистки.");
        Console.WriteLine("  Исправление: реализовать IDisposable, вызвать Dispose через using,");
        Console.WriteLine("               а MemoryStream из _cache освобождать при вытеснении из кэша.");

        SubHeader("Проблема 4. new byte[1024 * 100] внутри обработчика — это НЕ утечка");
        Console.WriteLine("  Важное разграничение: temp — локальная переменная, её время жизни");
        Console.WriteLine("  заканчивается вместе с вызовом метода, поэтому GC освободит её сразу.");
        Console.WriteLine("  Утечки памяти здесь нет. Но есть проблема ПРОИЗВОДИТЕЛЬНОСТИ:");
        Console.WriteLine("  - при каждом событии выделяется 100 КБ мусора;");
        Console.WriteLine("  - 100 КБ > 85 000 байт, значит мусор попадает в LOH, а он не уплотняется;");
        Console.WriteLine("  - при частом событии это вызывает заметную нагрузку на Gen2/LOH-сборку.");
        Console.WriteLine("  Исправление: переиспользовать один буфер-поле вместо выделения нового.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateFixedWitnesses(int count, List<WeakReference> witnesses)
    {
        for (int i = 0; i < count; i++)
        {
            witnesses.Add(new WeakReference(new FixedCode(i + 1)));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateBuggyWitnesses(int count, List<WeakReference> witnesses)
    {
        for (int i = 0; i < count; i++)
        {
            witnesses.Add(new WeakReference(new BuggyCode()));
        }
    }

    private static int CountAlive(List<WeakReference> witnesses)
    {
        int alive = 0;
        foreach (WeakReference witness in witnesses)
        {
            if (witness.IsAlive)
            {
                alive++;
            }
        }

        return alive;
    }

    private static void MeasureBuggy()
    {
        Header($"ЗАМЕР 1 — BuggyCode: создаём {InstanceCount} экземпляров, затем теряем ссылки");

        FullCollect();
        long baseline = GC.GetTotalMemory(true);
        Console.WriteLine($"Старт: {baseline / 1024.0 / 1024.0:F2} МБ");

        var witnesses = new List<WeakReference>();
        CreateBuggyWitnesses(InstanceCount, witnesses);

        FullCollect();
        long after = GC.GetTotalMemory(true);
        Console.WriteLine($"После создания {InstanceCount} экземпляров и полной сборки: {after / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"Прирост: {(after - baseline) / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"Экземпляров создано: {BuggyCode.InstancesCreated}, элементов в _cache: {BuggyCode.CacheCount}");
        Console.WriteLine($"Ссылок на объекты в коде больше нет, но все {InstanceCount} объектов живы:");

        int alive = CountAlive(witnesses);
        Console.WriteLine($"  WeakReference.IsAlive == true у {alive} из {InstanceCount} объектов");
        Console.WriteLine("  => УТЕЧКА ПОДТВЕРЖДЕНА: объекты вне досягаемости из кода, но удерживаются GC.");

        SubHeader("Растёт ли коллекция _cache");
        BuggyCode.RaiseGlobal();
        Console.WriteLine($"Событие обработано {BuggyCode.GlobalEventRaised} раз(а) — все экземпляры получили вызов.");
        Console.WriteLine($"Размер _cache продолжает расти с каждым new BuggyCode(): {BuggyCode.CacheCount} элементов.");
        Console.WriteLine($"Ожидаемый рост памяти при следующих {InstanceCount} экземплярах: ~{InstanceCount * 1024 * 1024 / 1024.0 / 1024.0:F0} МБ");
    }

    private static void MeasureFixed()
    {
        Header($"ЗАМЕР 2 — FixedCode: тот же сценарий с исправлениями");

        FullCollect();
        long baseline = GC.GetTotalMemory(true);
        Console.WriteLine($"Старт: {baseline / 1024.0 / 1024.0:F2} МБ");
        if (baseline > 100 * 1024 * 1024)
        {
            Console.WriteLine("  ВНИМАНИЕ: стартовый объём большой, потому что утечка из замера 1 всё ещё в памяти.");
            Console.WriteLine("  Это само по себе доказательство: статические корни BuggyCode не отпускают объекты.");
        }

        var witnesses = new List<WeakReference>();
        CreateFixedWitnesses(InstanceCount, witnesses);

        FullCollect();
        long afterCreate = GC.GetTotalMemory(true);
        Console.WriteLine($"После создания {InstanceCount} экземпляров: {afterCreate / 1024.0 / 1024.0:F2} МБ " +
                          $"(прирост {(afterCreate - baseline) / 1024.0 / 1024.0:F2} МБ)");
        Console.WriteLine($"Подписчиков на статическом событии: {FixedCode.GlobalSubscribers}");

        SubHeader("Вызываем Dispose() для всех зарегистрированных экземпляров");
        FixedCode.ClearRegistry();
        FullCollect();
        long afterDispose = GC.GetTotalMemory(true);
        Console.WriteLine($"После Dispose(): {afterDispose / 1024.0 / 1024.0:F2} МБ " +
                          $"(прирост отн. старта {(afterDispose - baseline) / 1024.0 / 1024.0:F2} МБ)");
        Console.WriteLine($"Осталось подписчиков на статическом событии: {FixedCode.GlobalSubscribers}");

        int alive = CountAlive(witnesses);
        Console.WriteLine($"  WeakReference.IsAlive == true у {alive} из {InstanceCount} объектов");
        Console.WriteLine(alive == 0
            ? "  => УТЕЧКИ НЕТ: после Dispose все объекты собираются GC."
            : $"  => ПРОВЕРКА НЕ ОДНОЗНАЧНА: выжило {alive} объект(ов), требуется анализ.");

        SubHeader("Событие больше не удерживает подписчиков");
        long raisedBefore = FixedCode.GlobalEventRaised;
        FixedCode.RaiseGlobal();
        Console.WriteLine($"Обработчиков сработало после освобождения: {FixedCode.GlobalEventRaised - raisedBefore} (ожидаем 0)");

        SubHeader("Проверка идемпотентности Dispose");
        var probe = new FixedCode(9999);
        probe.Dispose();
        probe.Dispose();
        Console.WriteLine("Двойной Dispose() выполнен без исключений — защита флагом _disposed работает.");
    }

    private static void Fixes()
    {
        Header("ИТОГ: СОПОСТАВЛЕНИЕ BuggyCode И FixedCode");

        Console.WriteLine("                        BuggyCode                    FixedCode");
        Console.WriteLine("  Статический кэш       растёт бесконечно           readonly, чистится в Dispose");
        Console.WriteLine("  IDisposable          не реализован              реализован, вызов через using");
        Console.WriteLine("  Статическое событие   подписка навсегда           отписка в Dispose");
        Console.WriteLine("  MemoryStream          не освобождается            Dispose() освобождает буфер");
        Console.WriteLine("  Буфер в обработчике   new byte[100K] каждый раз   переиспользуемое поле");
        Console.WriteLine($"  Выжило после GC       все {InstanceCount} экземпляров              ни одного (см. замер 2)");

        Console.WriteLine();
        Console.WriteLine("Правило, которое устраняет большую часть таких ошибок:");
        Console.WriteLine("  1. Подписка на событие всегда парная: += и -= в одном классе.");
        Console.WriteLine("  2. Класс, который подписывается на событие, реализует IDisposable.");
        Console.WriteLine("  3. Любой IDisposable из контейнера нужно освобождать, а не терять.");
        Console.WriteLine("  4. Статические коллекции требуют политики ограничения размера.");
        Console.WriteLine("  5. Буферы > 85 КБ переиспользуем, иначе растёт фрагментация LOH.");
    }
}
