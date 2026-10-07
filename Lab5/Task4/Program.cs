using System.Diagnostics;

namespace Lab5Task4;

sealed class BankAccount
{
    private readonly object _lock = new();
    private int _balance;

    public string Owner { get; }

    public BankAccount(string owner) => Owner = owner;

    public void Deposit(int amount)
    {
        lock (_lock)
        {
            int newBalance = _balance + amount;
            Thread.Sleep(10);
            _balance = newBalance;
            Console.WriteLine($"  [{Owner}] +{amount} -> баланс {_balance}");
        }
    }

    public void Withdraw(int amount)
    {
        lock (_lock)
        {
            if (_balance >= amount)
            {
                int newBalance = _balance - amount;
                Thread.Sleep(10);
                _balance = newBalance;
                Console.WriteLine($"  [{Owner}] -{amount} -> баланс {_balance}");
            }
            else
            {
                Console.WriteLine($"  [{Owner}] отказ: недостаточно средств для снятия {amount}, баланс {_balance}");
            }
        }
    }

    public int GetBalance()
    {
        lock (_lock)
        {
            return _balance;
        }
    }
}

sealed class SafeCounter
{
    private readonly object _lock = new();
    private int _value;

    public int Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
    }

    public void Increment()
    {
        lock (_lock)
        {
            _value++;
        }
    }

    public void Decrement()
    {
        lock (_lock)
        {
            _value--;
        }
    }

    public int GetValue() => Value;
}

static class Program
{
    const int Threads = 100;
    const int Iterations = 1000;

    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 4. СИНХРОНИЗАЦИЯ ПОТОКОВ ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        BankAccountDemo();
        SafeCounterDemo();
        SemaphoreLimit();

        Console.WriteLine("=== ЗАДАНИЕ 4 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void BankAccountDemo()
    {
        Console.WriteLine("--- 4.1. Банковский счёт: 5 пополнений и 5 снятий параллельно ---");

        BankAccount account = new BankAccount("счёт-1");
        Task[] tasks = new Task[10];

        for (int i = 0; i < 5; i++)
        {
            tasks[i] = Task.Run(() => account.Deposit(100));
        }

        for (int i = 5; i < 10; i++)
        {
            tasks[i] = Task.Run(() => account.Withdraw(50));
        }

        Task.WaitAll(tasks);
        Console.WriteLine($"  Итоговый баланс: {account.GetBalance()} (ожидалось 250, если бы снятий хватило)");
        Console.WriteLine("  Вывод: lock гарантирует, что все операции с _balance выполнились целиком.\n");
    }

    static void SafeCounterDemo()
    {
        Console.WriteLine($"--- 4.2. SafeCounter: {Threads} потоков на инкремент и {Threads} на декремент, по {Iterations} раз ---");

        SafeCounter counter = new SafeCounter();
        Thread[] threads = new Thread[Threads * 2];

        for (int i = 0; i < Threads; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int k = 0; k < Iterations; k++)
                {
                    counter.Increment();
                }
            });
            threads[Threads + i] = new Thread(() =>
            {
                for (int k = 0; k < Iterations; k++)
                {
                    counter.Decrement();
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

        Console.WriteLine($"  Итоговое значение: {counter.GetValue()} (ожидалось 0)");
        Console.WriteLine($"  Корректно: {counter.GetValue() == 0}, время {sw.ElapsedMilliseconds} мс");
        Console.WriteLine("  Без lock результат был бы не 0: инкремент и декремент не атомарны.\n");
    }

    static void SemaphoreLimit()
    {
        Console.WriteLine("--- 4.3. SemaphoreSlim: одновременно работают не более 3 потоков ---");

        using SemaphoreSlim semaphore = new(3, 3);
        int active = 0;
        int maxObserved = 0;
        object stateLock = new();

        Task[] tasks = new Task[12];
        for (int i = 0; i < tasks.Length; i++)
        {
            int id = i;
            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    int now = Interlocked.Increment(ref active);
                    lock (stateLock)
                    {
                        maxObserved = Math.Max(maxObserved, now);
                    }

                    Console.WriteLine($"  Задача {id,2} вошла в критическую секцию (одновременно {now})");
                    await Task.Delay(150);
                    Interlocked.Decrement(ref active);
                    Console.WriteLine($"  Задача {id,2} вышла из критической секции");
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        Task.WaitAll(tasks);
        Console.WriteLine($"\n  Максимально одновременных задач: {maxObserved} (лимит семафора 3)");
        Console.WriteLine($"  Соблюдён ли лимит: {maxObserved <= 3}\n");
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
