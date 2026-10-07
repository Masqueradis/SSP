using System.Diagnostics;
using System.Numerics;

namespace Lab5Task3;

static class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 3. TASK И ASYNC/AWAIT ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        await BasicAsyncAwait();
        await ParallelDownloads();
        await AsyncTimer();
        await Fibonacci();
        await ErrorHandling();
        await NoAwaitWarning();

        Console.WriteLine("=== ЗАДАНИЕ 3 ВЫПОЛНЕНО ===");
        Pause();
    }

    static async Task BasicAsyncAwait()
    {
        Console.WriteLine("--- 3.1. Базовый async/await ---");

        Task<int> task = LongRunningOperationAsync();
        Console.WriteLine($"  Главный поток {Environment.CurrentManagedThreadId} не блокируется и продолжает работу.");
        Console.WriteLine($"  Состояние задачи сразу после вызова: {task.Status}");

        int result = await task;
        Console.WriteLine($"  Результат: {result}");
        Console.WriteLine("  Thread.Sleep блокирует поток, Task.Delay освобождает его — поток может обслуживать другие задачи.\n");
    }

    static async Task<int> LongRunningOperationAsync()
    {
        Console.WriteLine("  Операция начата...");
        await Task.Delay(2000);
        Console.WriteLine("  Операция завершена");
        return 42;
    }

    static async Task ParallelDownloads()
    {
        Console.WriteLine("--- 3.2. Параллельное выполнение трёх «загрузок» через Task.WhenAll ---");

        Stopwatch sw = Stopwatch.StartNew();
        Task<string> task1 = Task.Run(() => SimulateWork("https://site-1.ru/data.json", 1500));
        Task<string> task2 = Task.Run(() => SimulateWork("https://site-2.ru/data.json", 900));
        Task<string> task3 = Task.Run(() => SimulateWork("https://site-3.ru/data.json", 1200));

        string[] results = await Task.WhenAll(task1, task2, task3);
        sw.Stop();

        foreach (string r in results)
        {
            Console.WriteLine($"  {r}");
        }

        Console.WriteLine($"  Суммарное время: {sw.ElapsedMilliseconds} мс (сумма задержек = 3600 мс)");
        Console.WriteLine("  Вывод: задачи выполнялись одновременно, поэтому 3600 мс превратились в ~1500 мс.\n");
    }

    static string SimulateWork(string url, int delayMs)
    {
        Console.WriteLine($"    Загрузка {url} начата (поток {Environment.CurrentManagedThreadId}), задержка {delayMs} мс");
        Thread.Sleep(delayMs);
        int size = 1024 * (10 + Math.Abs(url.GetHashCode(StringComparison.Ordinal) % 17));
        return $"  Загрузка {url} завершена: {size} байт, поток {Environment.CurrentManagedThreadId}";
    }

    static async Task AsyncTimer()
    {
        Console.WriteLine("--- 3.3. Асинхронный таймер: каждые 2 секунды 5 раз (10 секунд) ---");

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 1; i <= 5; i++)
        {
            await Task.Delay(2000);
            Console.WriteLine($"  [{sw.Elapsed.TotalSeconds:F1} с] Тик {i}: {DateTime.Now:HH:mm:ss.fff}");
        }

        Console.WriteLine("  Таймер остановлен.\n");
    }

    static async Task Fibonacci()
    {
        Console.WriteLine("--- 3.4. Асинхронный расчёт чисел Фибоначчи через Task.Run ---");

        const int n = 2000;
        const int repeats = 3;

        Stopwatch swSync = Stopwatch.StartNew();
        BigInteger syncValue = BigInteger.Zero;
        for (int r = 0; r < repeats; r++)
        {
            syncValue = FibonacciBig(n);
        }

        swSync.Stop();

        Stopwatch swAsync = Stopwatch.StartNew();
        Task<BigInteger> task = Task.Run(() =>
        {
            BigInteger value = BigInteger.Zero;
            for (int r = 0; r < repeats; r++)
            {
                value = FibonacciBig(n);
            }

            return value;
        });
        BigInteger asyncValue = await task;
        swAsync.Stop();

        Console.WriteLine($"  Fib({n}) содержит {asyncValue.ToString().Length} цифр, повторов: {repeats}");
        Console.WriteLine($"  Синхронно  : {swSync.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Через Task.Run: {swAsync.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Результаты совпадают: {syncValue == asyncValue}");        Console.WriteLine("  Вывод: Task.Run лишь выносит вычисление в поток пула, оно остаётся синхронным и блокирующим.");
        Console.WriteLine("  Для настоящей асинхронности CPU-задачу надо разбивать и отдавать await.\n");
    }

    static long FibonacciSync(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        long a = 0, b = 1;
        for (int i = 2; i <= n; i++)
        {
            (a, b) = (b, a + b);
        }

        return b;
    }

    static BigInteger FibonacciBig(int n)
    {
        BigInteger a = BigInteger.Zero;
        BigInteger b = BigInteger.One;
        for (int i = 2; i <= n; i++)
        {
            (a, b) = (b, a + b);
        }

        return b;
    }

    static async Task ErrorHandling()
    {
        Console.WriteLine("--- 3.5. Обработка исключений в асинхронном коде ---");

        try
        {
            await FaultyOperationAsync();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Поймано исключение: {ex.Message}");
        }

        try
        {
            await Task.WhenAll(SucceedAsync("A"), FailingAsync("B"), SucceedAsync("C"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  WhenAll: агрегированное исключение — {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("  Вывод: WhenAll не отменяет остальные задачи, а ждёт их все и бросает исключение.\n");
    }

    static async Task SucceedAsync(string name)
    {
        await Task.Delay(300);
        Console.WriteLine($"  Задача {name} успешно завершена");
    }

    static async Task FailingAsync(string name)
    {
        await Task.Delay(200);
        throw new InvalidOperationException($"Задача {name} провалилась!");
    }

    static async Task FaultyOperationAsync()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("Что-то пошло не так!");
    }

    static async Task NoAwaitWarning()
    {
        Console.WriteLine("--- 3.6. Что будет, если в async-методе нет await ---");

        Task task = NoAwaitMethod();
        Console.WriteLine($"  Task.Status сразу после вызова: {task.Status} (для async без await — RanToCompletion или WarningAsError)");
        await task;
        Console.WriteLine("  Метод без await компилируется с предупреждением CS1998 и выполняется синхронно до первого ожидания.");
        Console.WriteLine("  Исключение внутри такого метода не попадёт в await — оно будет брошено синхронно.\n");
    }

    static Task NoAwaitMethod()
    {
        return Task.CompletedTask;
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
