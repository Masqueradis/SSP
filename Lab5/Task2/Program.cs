using System.Collections.Concurrent;
using System.Diagnostics;

namespace Lab5Task2;

static class Program
{
    const int TaskCount = 20;
    const int BenchmarkCount = 1000;

    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 2. ПУЛ ПОТОКОВ (ThreadPool) ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        PoolInfo();
        TwentyTasks();
        ThreadVsPool();
        InsideTheSameTask();

        Console.WriteLine("=== ЗАДАНИЕ 2 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void PoolInfo()
    {
        Console.WriteLine("--- 2.1. Параметры пула потоков ---");

        ThreadPool.GetMinThreads(out int minWorker, out int minCompletion);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxCompletion);

        Console.WriteLine($"  Минимум рабочих потоков       : {minWorker}");
        Console.WriteLine($"  Минимум потоков завершения   : {minCompletion}");
        Console.WriteLine($"  Максимум рабочих потоков      : {maxWorker}");
        Console.WriteLine($"  Максимум потоков завершения  : {maxCompletion}");
        Console.WriteLine($"  Доступно процессов ОС        : {Environment.ProcessorCount}\n");
    }

    static void TwentyTasks()
    {
        Console.WriteLine($"--- 2.2. {TaskCount} задач в пуле, каждая имитирует долгую операцию (500 мс) ---");
        Console.WriteLine("  Главный поток продолжает работу, не ожидая завершения задач.\n");

        ConcurrentDictionary<int, int> threadUsage = new();
        CountdownEvent finished = new(TaskCount);
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < TaskCount; i++)
        {
            int taskId = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int tid = Environment.CurrentManagedThreadId;
                threadUsage.AddOrUpdate(tid, 1, (_, v) => v + 1);
                Console.WriteLine($"  Задача {taskId,2} выполняется в потоке {tid}");
                Thread.Sleep(500);
                Console.WriteLine($"  Задача {taskId,2} завершена");
                finished.Signal();
            });
        }

        Console.WriteLine($"\n  Главный поток: поставлено {TaskCount} задач, ждём завершения...");
        finished.Wait();
        sw.Stop();

        Console.WriteLine($"\n  Все {TaskCount} задач завершены за {sw.ElapsedMilliseconds} мс.");
        Console.WriteLine($"  Задействовано уникальных потоков пула: {threadUsage.Count} (из {TaskCount} задач)");
        Console.WriteLine($"  Одна задача занимает поток ~500 мс, 20 задач последовательно заняли бы ~10000 мс.");
        Console.WriteLine("  Вывод: пул создал несколько потоков и переиспользовал их для разных задач.\n");
    }

    static void ThreadVsPool()
    {
        Console.WriteLine($"--- 2.3. Производительность: {BenchmarkCount} обычных Thread против {BenchmarkCount} задач пула ---");

        long counter = 0;
        Stopwatch sw = Stopwatch.StartNew();
        Thread[] threads = new Thread[BenchmarkCount];
        for (int i = 0; i < BenchmarkCount; i++)
        {
            threads[i] = new Thread(() => Interlocked.Increment(ref counter));
        }

        long createMs = sw.ElapsedMilliseconds;
        foreach (Thread t in threads)
        {
            t.Start();
        }

        long startMs = sw.ElapsedMilliseconds;
        foreach (Thread t in threads)
        {
            t.Join();
        }

        sw.Stop();
        long totalThreadMs = sw.ElapsedMilliseconds;
        long threadCounter = counter;

        counter = 0;
        Stopwatch swPool = Stopwatch.StartNew();
        CountdownEvent done = new(BenchmarkCount);
        for (int i = 0; i < BenchmarkCount; i++)
        {
            ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(_ =>
            {
                Interlocked.Increment(ref counter);
                done.Signal();
            }), null);
        }

        long poolQueueMs = swPool.ElapsedMilliseconds;
        done.Wait();
        swPool.Stop();

        Console.WriteLine($"  Thread      : создание {createMs,6} мс | запуск {startMs - createMs,6} мс | итого {totalThreadMs,6} мс | результат {threadCounter:N0}");
        Console.WriteLine($"  ThreadPool  : постановка {poolQueueMs,6} мс | ожидание {swPool.ElapsedMilliseconds - poolQueueMs,6} мс | итого {swPool.ElapsedMilliseconds,6} мс | результат {counter:N0}");
        Console.WriteLine($"  Ускорение ThreadPool относительно Thread: {(double)totalThreadMs / Math.Max(1, swPool.ElapsedMilliseconds):F1}x");
        Console.WriteLine("  Вывод: создание Thread дорого (1 МБ стека + запись в таблицу потоков ОС),");
        Console.WriteLine("  пул переиспользует ограниченное число потоков, поэтому почти бесплатен.\n");
    }

    static void InsideTheSameTask()
    {
        Console.WriteLine("--- 2.4. Используются ли одни и те же потоки для разных задач? ---");

        CountdownEvent done = new(TaskCount);
        ConcurrentDictionary<int, List<int>> map = new();

        for (int i = 0; i < TaskCount; i++)
        {
            int taskId = i;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int tid = Environment.CurrentManagedThreadId;
                map.AddOrUpdate(tid, _ => new List<int> { taskId }, (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(taskId);
                    }

                    return list;
                });
                Thread.Sleep(50);
                done.Signal();
            });
        }

        done.Wait();

        int reused = map.Count(x => x.Value.Count > 1);
        foreach (var pair in map.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  Поток {pair.Key}: задачи {string.Join(", ", pair.Value.OrderBy(x => x))}");
        }

        Console.WriteLine($"\n  Уникальных потоков: {map.Count}, из них выполнили больше одной задачи: {reused}");
        Console.WriteLine("  Вывод: да, пул переиспользует одни и те же потоки для разных задач.\n");
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
