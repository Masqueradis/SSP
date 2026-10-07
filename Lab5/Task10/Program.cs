using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Lab5Task10;

sealed record WaitingThread(string Name, string Holds, string WaitsFor, int ThreadId, string[] Frames);

sealed class WaitingRegistry
{
    private readonly List<WaitingThread> _items = new();
    private readonly object _sync = new();

    public void Set(string name, string holds, string waitsFor, int threadId, StackTrace trace)
    {
        List<string> frames = new();
        for (int i = 0; i < trace.FrameCount && frames.Count < 4; i++)
        {
            MethodBase? method = trace.GetFrame(i).GetMethod();
            if (method is null)
            {
                continue;
            }

            frames.Add($"{method.DeclaringType?.Name}.{method.Name}()");
        }

        lock (_sync)
        {
            _items.RemoveAll(x => x.Name == name);
            _items.Add(new WaitingThread(name, holds, waitsFor, threadId, frames.ToArray()));
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _items.Clear();
        }
    }

    public IReadOnlyList<WaitingThread> All()
    {
        lock (_sync)
        {
            return _items.ToList();
        }
    }

    public string? HolderOf(string resource)
    {
        lock (_sync)
        {
            return _items.FirstOrDefault(x => x.Holds == resource)?.Name;
        }
    }

    public bool HasCycle(out string path)
    {
        lock (_sync)
        {
            path = string.Empty;
            if (_items.Count < 2)
            {
                return false;
            }

            List<string> visited = new();
            string current = _items[0].Name;
            while (true)
            {
                visited.Add(current);
                WaitingThread? node = _items.FirstOrDefault(x => x.Name == current);
                if (node is null)
                {
                    return false;
                }

                string? next = _items.FirstOrDefault(x => x.Holds == node.WaitsFor)?.Name;
                if (next is null)
                {
                    return false;
                }

                if (visited.Contains(next))
                {
                    path = string.Join(" -> ", visited.SkipWhile(x => x != next).Append(next));
                    return true;
                }

                current = next;
            }
        }
    }
}

static class Program
{
    static readonly object LockA = new();
    static readonly object LockB = new();

    static readonly WaitingRegistry Waiting = new();
    static volatile bool _abort;
    static int _tid1 = -1;
    static int _tid2 = -1;

    [DllImport("libc", EntryPoint = "gettid")]
    static extern int gettid();

    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 10. ОБНАРУЖЕНИЕ И ДИАГНОСТИКА ВЗАИМОБЛОКИРОВОК ===");
        Console.WriteLine($"Среда: {(OperatingSystem.IsWindows() ? "Windows" : "Linux")}, {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        if (args.Length > 0 && args[0] == "deadlock")
        {
            RealHang();
            return;
        }

        InstrumentedDeadlock();
        Diagnostics();
        FixedOrder();
        TryEnterWithTimeout();

        Console.WriteLine("=== ЗАДАНИЕ 10 ВЫПОЛНЕНО ===");
        Console.WriteLine();
        Console.WriteLine("Дополнительно есть режим реального зависания: dotnet run -- deadlock");
        Console.WriteLine("Он воспроизводит код из методички буквально и зависает навсегда.");
        Pause();
    }

    static void RealHang()
    {
        Console.WriteLine("--- РЕЖИМ РЕАЛЬНОГО ЗАВИСАНИЯ (dotnet run -- deadlock) ---");
        Console.WriteLine("Сейчас программа зависнет навсегда, остановите её клавишами Ctrl+C.");
        Console.WriteLine("Это дословно тот код, что приведён в методичке.\n");

        Thread t1 = new Thread(Thread1Real) { Name = "Thread1" };
        Thread t2 = new Thread(Thread2Real) { Name = "Thread2" };
        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();
    }

    static void Thread1Real()
    {
        lock (LockA)
        {
            Console.WriteLine($"Thread1: захватил lockA (tid {gettid()})");
            Thread.Sleep(1000);
            lock (LockB)
            {
                Console.WriteLine("Thread1: захватил lockB");
            }
        }
    }

    static void Thread2Real()
    {
        lock (LockB)
        {
            Console.WriteLine($"Thread2: захватил lockB (tid {gettid()})");
            Thread.Sleep(1000);
            lock (LockA)
            {
                Console.WriteLine("Thread2: захватил lockA");
            }
        }
    }

    static void InstrumentedDeadlock()
    {
        Console.WriteLine("--- 10.1. Воспроизведение взаимоблокировки ---");
        Console.WriteLine("Thread1 захватывает lockA и ждёт lockB, Thread2 наоборот: lockB и ждёт lockA.");
        Console.WriteLine("Ожидание второго замка сделано через Monitor.TryEnter с таймаутом 150 мс, чтобы");
        Console.WriteLine("программа успела вывести диагностику и завершиться. Само взаимное ожидание");
        Console.WriteLine("при этом настоящее: каждый поток удерживает один замок и не может получить второй.\n");

        _abort = false;
        Waiting.Clear();

        (Thread First, Thread Second) pair = StartDeadlockPair();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 2000 && (pair.First.IsAlive || pair.Second.IsAlive))
        {
            Thread.Sleep(50);
        }

        bool deadlocked = pair.First.IsAlive && pair.Second.IsAlive;
        Console.WriteLine($"  Через {sw.ElapsedMilliseconds} мс оба потока ещё живы: {deadlocked}");
        Console.WriteLine($"  Взаимоблокировка воспроизведена: {deadlocked}");

        _abort = true;
        pair.First.Join();
        pair.Second.Join();
        Console.WriteLine($"  После снятия ожидания оба потока завершились, всего {sw.ElapsedMilliseconds} мс.\n");
    }

    static (Thread First, Thread Second) StartDeadlockPair()
    {
        _abort = false;
        Waiting.Clear();
        _tid1 = -1;
        _tid2 = -1;

        Thread t1 = new Thread(DeadlockedThread1) { Name = "Thread1" };
        Thread t2 = new Thread(DeadlockedThread2) { Name = "Thread2" };

        t1.Start();
        t2.Start();
        return (t1, t2);
    }

    static void DeadlockedThread1()
    {
        DeadlockedWorker("Thread1", "lockA", "lockB", LockA, LockB, tid => _tid1 = tid);
    }

    static void DeadlockedThread2()
    {
        DeadlockedWorker("Thread2", "lockB", "lockA", LockB, LockA, tid => _tid2 = tid);
    }

    static void DeadlockedWorker(string name, string holds, string wants, object first, object second, Action<int> reportTid)
    {
        int tid = gettid();
        reportTid(tid);
        Console.WriteLine($"  {name}: захватил {holds} (tid {tid})");

        Monitor.Enter(first);
        try
        {
            Thread.Sleep(1000);
            Waiting.Set(name, holds, wants, tid, new StackTrace(1, false));

            while (!Monitor.TryEnter(second, 150))
            {
                if (_abort)
                {
                    Waiting.Clear();
                    return;
                }
            }

            Waiting.Clear();
            Monitor.Exit(second);
        }
        finally
        {
            Monitor.Exit(first);
        }
    }

    static void Diagnostics()
    {
        Console.WriteLine("--- 10.2. Диагностика без Visual Studio ---");
        Console.WriteLine("В Visual Studio это окна Parallel Stacks, Threads и Call Stack.");
        Console.WriteLine("Ниже их аналоги, построенные средствами самой программы.\n");

        Waiting.Clear();
        (Thread First, Thread Second) pair = StartDeadlockPair();
        Thread.Sleep(1600);

        PrintParallelStacks();
        PrintWaitForGraph();
        PrintThreadsWindow();

        _abort = true;
        pair.First.Join();
        pair.Second.Join();
        Console.WriteLine();
    }

    static void PrintParallelStacks()
    {
        Console.WriteLine("  [Аналог окна Parallel Stacks]");
        StringBuilder sb = new();
        foreach (WaitingThread w in Waiting.All())
        {
            sb.AppendLine($"    Thread [{w.Name}] (tid {w.ThreadId}) удерживает {w.Holds}, ожидает {w.WaitsFor}");
            foreach (string frame in w.Frames)
            {
                sb.AppendLine($"      └─ {frame}");
            }
        }

        Console.Write(sb.ToString());
        bool cycle = Waiting.HasCycle(out string path);
        Console.WriteLine($"    Взаимоблокировка: {cycle}, цепочка: {path}");
        Console.WriteLine();
    }

    static void PrintWaitForGraph()
    {
        Console.WriteLine("  [Граф ожидания: кто какой ресурс ждёт]");
        foreach (WaitingThread w in Waiting.All())
        {
            string? holder = Waiting.HolderOf(w.WaitsFor);
            Console.WriteLine($"    {w.Name} ждёт {w.WaitsFor} --> удерживает {(holder ?? "никто")}");
        }

        bool cycle = Waiting.HasCycle(out string path);
        Console.WriteLine($"    Цикл замкнут: {cycle} ({path})");
        Console.WriteLine();
    }

    static void PrintThreadsWindow()
    {
        Console.WriteLine("  [Аналог окна Threads через Process.GetCurrentProcess().Threads]");
        Process process = Process.GetCurrentProcess();
        process.Refresh();

        var wanted = new HashSet<int> { _tid1, _tid2 };
        ProcessThread[] all = process.Threads.Cast<ProcessThread>().ToArray();
        foreach (ProcessThread pt in all.OrderBy(t => t.Id))
        {
            if (!wanted.Contains(pt.Id))
            {
                continue;
            }

            string who = pt.Id == _tid1 ? "Thread1" : "Thread2";
            string reason = pt.WaitReason == (ThreadWaitReason)0 ? "UserRequest" : pt.WaitReason.ToString();
            Console.WriteLine($"    ID {pt.Id,6} ({who,-8}) состояние: {pt.ThreadState,-24} причина: {reason,-12} CPU: {pt.TotalProcessorTime.TotalMilliseconds,5:F0} мс");
        }

        Console.WriteLine();
    }

    static void FixedOrder()
    {
        Console.WriteLine("--- 10.3. Исправление: одинаковый порядок захвата ---");
        Console.WriteLine("Оба потока берут lockA первым, затем lockB. Цикл ожидания исчезает.\n");

        object a = new();
        object b = new();
        var sw = Stopwatch.StartNew();

        Thread t1 = new Thread(() => Ordered(a, b, "Thread1")) { Name = "Thread1" };
        Thread t2 = new Thread(() => Ordered(a, b, "Thread2")) { Name = "Thread2" };
        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();
        sw.Stop();

        Console.WriteLine($"  Оба потока завершились за {sw.ElapsedMilliseconds} мс — взаимоблокировки нет");
        Console.WriteLine("  Вывод: единый глобальный порядок блокировок исключает цикл ожидания по построению.");
        Console.WriteLine("  Альтернативы: одна объединённая блокировка вместо двух, либо lock только");
        Console.WriteLine("  на атомарных полях, где составного состояния нет.\n");
    }

    static void Ordered(object a, object b, string name)
    {
        lock (a)
        {
            Thread.Sleep(150);
            lock (b)
            {
                Thread.Sleep(150);
                Console.WriteLine($"  {name}: захватил оба замка в правильном порядке");
            }
        }
    }

    static void TryEnterWithTimeout()
    {
        Console.WriteLine("--- 10.4. Обнаружение дедлока по таймауту (Monitor.TryEnter с TimeSpan) ---");

        object a = new();
        object b = new();
        int timeouts = 0;

        Thread t1 = new Thread(() => TimeoutWorker(a, b, "lockA", "lockB", "Thread1", () => Interlocked.Increment(ref timeouts)))
        {
            Name = "Thread1"
        };
        Thread t2 = new Thread(() => TimeoutWorker(b, a, "lockB", "lockA", "Thread2", () => Interlocked.Increment(ref timeouts)))
        {
            Name = "Thread2"
        };

        t1.Start();
        t2.Start();
        t1.Join();
        t2.Join();

        Console.WriteLine($"  Сработало таймаутов: {timeouts} из 2");
        Console.WriteLine("  Механизм: Monitor.TryEnter возвращает false вместо бесконечной блокировки,");
        Console.WriteLine("  поэтому программа может записать в лог, отправить метрику и освободить ресурсы.");
        Console.WriteLine("  Практичный приём: сначала записать состояние под одним монитором, и только потом");
        Console.WriteLine("  захватывать остальные блокировки — тогда цикл ожидания становится невозможен.\n");
    }

    static void TimeoutWorker(object first, object second, string holdsName, string wantsName, string name, Action onTimeout)
    {
        if (!Monitor.TryEnter(first, TimeSpan.FromMilliseconds(200)))
        {
            return;
        }

        try
        {
            Thread.Sleep(300);
            if (!Monitor.TryEnter(second, TimeSpan.FromMilliseconds(200)))
            {
                onTimeout();
                Console.WriteLine($"  {name}: не удалось захватить {wantsName} за 200 мс, удерживая {holdsName} — вероятен дедлок");
                return;
            }

            Console.WriteLine($"  {name}: захватил {wantsName}");
            Monitor.Exit(second);
        }
        finally
        {
            Monitor.Exit(first);
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
