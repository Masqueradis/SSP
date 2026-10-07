using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lab5Task1;

static class RaceField
{
    public static int Value;
}

class Worker
{
    public int Data { get; set; }

    public void Execute() => Console.WriteLine($"[Worker] Обработка значения {Data}");
}

static class Program
{
    static readonly object Sync = new();
    static int _lockedCounter;
    static volatile bool _runPriorities;
    static long _lowest, _normal, _highest;

    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 1. СОЗДАНИЕ И УПРАВЛЕНИЕ ПОТОКАМИ (Thread) ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        BasicThread();
        ThreadParameters();
        ThreeThreadsJoin();
        Race();
        Priorities();

        Console.WriteLine("\n=== ЗАДАНИЕ 1 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void BasicThread()
    {
        Console.WriteLine("--- 1.1. Базовый поток и Join ---");

        Thread myThread = new Thread(DoWork);
        myThread.Start();

        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"  [Главный поток {Environment.CurrentManagedThreadId}] шаг {i}");
            Thread.Sleep(100);
        }

        myThread.Join();
        Console.WriteLine("  Join() вернул управление: дочерний поток завершён.\n");
    }

    static void DoWork()
    {
        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"  [Дочерний поток {Environment.CurrentManagedThreadId}] шаг {i}");
            Thread.Sleep(150);
        }
    }

    static void ThreadParameters()
    {
        Console.WriteLine("--- 1.2. Три способа передачи параметров в поток ---");

        Thread thread1 = new Thread(PrintMessage);
        thread1.Start("Привет из потока!");
        thread1.Join();

        string message = "Сообщение через лямбду";
        Thread thread2 = new Thread(() => Console.WriteLine($"  [Лямбда, поток {Environment.CurrentManagedThreadId}] {message}"));
        thread2.Start();
        thread2.Join();

        Worker worker = new Worker { Data = 42 };
        Thread thread3 = new Thread(worker.Execute);
        thread3.Start();
        thread3.Join();

        Console.WriteLine();
    }

    static void PrintMessage(object? msg) =>
        Console.WriteLine($"  [ParameterizedThreadStart, поток {Environment.CurrentManagedThreadId}] {msg}");

    static void ThreeThreadsJoin()
    {
        Console.WriteLine("--- 1.3. Три потока, каждый выводит числа от 1 до 10 с задержкой 200 мс ---");

        Thread[] threads = new Thread[3];
        for (int t = 0; t < threads.Length; t++)
        {
            int id = t + 1;
            threads[t] = new Thread(() =>
            {
                for (int i = 1; i <= 10; i++)
                {
                    Console.WriteLine($"  [Поток {id}] {i}");
                    Thread.Sleep(200);
                }
            });
        }

        var sw = Stopwatch.StartNew();
        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        sw.Stop();
        Console.WriteLine($"  Все три потока завершены за {sw.ElapsedMilliseconds} мс (ожидается ~2000 мс).\n");
    }

    static void Race()
    {
        Console.WriteLine("--- 1.4. Гонка данных: два потока инкрементируют общую переменную ---");
        const int iterations = 1_000_000;

        RaceField.Value = 0;
        Thread r1 = new Thread(() => RaceSpin(iterations));
        Thread r2 = new Thread(() => RaceSpin(iterations));
        r1.Start();
        r2.Start();
        r1.Join();
        r2.Join();
        int withoutLock = RaceField.Value;

        RaceField.Value = 0;
        Thread l1 = new Thread(() => RaceSpinLocked(iterations));
        Thread l2 = new Thread(() => RaceSpinLocked(iterations));
        l1.Start();
        l2.Start();
        l1.Join();
        l2.Join();
        int withLock = _lockedCounter;

        Console.WriteLine($"  Без блокировки: {withoutLock:N0} (ожидалось {iterations * 2:N0}, потеряно {iterations * 2 - withoutLock:N0})");
        Console.WriteLine($"  С блокировкой : {withLock:N0} (ожидалось {iterations * 2:N0}, потеряно {iterations * 2 - withLock:N0})");
        Console.WriteLine("  Вывод: без синхронизации инкремент не атомарен — часть увеличений теряется.\n");
    }

    static void RaceSpin(int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            RaceField.Value++;
        }
    }

    static void RaceSpinLocked(int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            lock (Sync)
            {
                _lockedCounter++;
            }
        }
    }

    static void Priorities()
    {
        Console.WriteLine("--- 1.5. Приоритеты потоков (Lowest / Normal / Highest) ---");
        Console.WriteLine("  Каждый поток крутит счётчик 2 секунды, затем печатает число выполненных итераций.");
        Console.WriteLine($"  Система: {(OperatingSystem.IsWindows() ? "Windows" : "Linux")}, потоков ОС: {Environment.ProcessorCount}");

        _lowest = _normal = _highest = 0;
        _runPriorities = true;

        Task stopper = Task.Run(async () =>
        {
            await Task.Delay(2000);
            _runPriorities = false;
        });

        Thread low = new Thread(() =>
        {
            PrioritySpin(() => Interlocked.Increment(ref _lowest));
            _niceLow = ReadOwnNice();
        })
        {
            Name = "Lowest",
            Priority = ThreadPriority.Lowest
        };
        Thread norm = new Thread(() =>
        {
            PrioritySpin(() => Interlocked.Increment(ref _normal));
            _niceNorm = ReadOwnNice();
        })
        {
            Name = "Normal",
            Priority = ThreadPriority.Normal
        };
        Thread high = new Thread(() =>
        {
            PrioritySpin(() => Interlocked.Increment(ref _highest));
            _niceHigh = ReadOwnNice();
        })
        {
            Name = "Highest",
            Priority = ThreadPriority.Highest
        };

        string pLow = low.Priority.ToString();
        string pNorm = norm.Priority.ToString();
        string pHigh = high.Priority.ToString();

        low.Start();
        norm.Start();
        high.Start();

        low.Join();
        norm.Join();
        high.Join();
        stopper.Wait();

        Console.WriteLine($"  Фактический nice (поле 19 в /proc/self/task/<tid>/stat):");
        Console.WriteLine($"    {pLow,-8} -> {_niceLow}");
        Console.WriteLine($"    {pNorm,-8} -> {_niceNorm}");
        Console.WriteLine($"    {pHigh,-8} -> {_niceHigh}");

        Console.WriteLine($"  Lowest : {_lowest:N0} итераций (приоритет {pLow})");
        Console.WriteLine($"  Normal : {_normal:N0} итераций (приоритет {pNorm})");
        Console.WriteLine($"  Highest: {_highest:N0} итераций (приоритет {pHigh})");
        Console.WriteLine("  Вывод: Thread.Priority влияет только на планирование, но не на корректность результата.");
        Console.WriteLine("  При 3 потоках и свободных ядрах все потоки выполняются одновременно, поэтому числа близки.");
        Console.WriteLine("  Особенность Linux: .NET сопоставляет Thread.Priority со значением nice, но понизить nice");
        Console.WriteLine("  (то есть реально повысить приоритет) обычный пользователь не может — только root.");
        Console.WriteLine("  Поэтому на Linux Lowest/Normal/Highest фактически одинаковы, а на Windows приоритеты работают.");
        Console.WriteLine("  Проверка: taskset -c 0 dotnet run — все три потока на одном ядре.\n");
    }

    static int _niceLow = int.MinValue;
    static int _niceNorm = int.MinValue;
    static int _niceHigh = int.MinValue;

    static int ReadOwnNice()
    {
        try
        {
            long nativeId = gettid();
            string stat = File.ReadAllText($"/proc/self/task/{nativeId}/stat");
            int close = stat.LastIndexOf(')');
            string[] fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return int.Parse(fields[16]);
        }
        catch (Exception)
        {
            return int.MinValue;
        }
    }

    [DllImport("libc", EntryPoint = "gettid")]
    static extern int gettid();

    static void PrioritySpin(Action action)
    {
        while (_runPriorities)
        {
            action();
            if ((DateTime.UtcNow.Ticks & 0xFF) == 0)
            {
                Thread.Sleep(0);
            }
        }
    }

    static void Pause()
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        Console.Write("Нажмите Enter для выхода...");
        try
        {
            Console.ReadLine();
        }
        catch (Exception)
        {
        }
    }
}
