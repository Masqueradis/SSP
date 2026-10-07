using System.Diagnostics;

namespace Lab5Task6;

static class Program
{
    const int PrimeLimit = 10_000_000;

    static async Task Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 6. ОТМЕНА ОПЕРАЦИЙ (CancellationToken) ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        await CancelAfterDemo();
        await ManualCancelDemo();
        await PrimesWithCancel();
        await LinkedTokensDemo();
        CheckTokenInsideLoop();

        Console.WriteLine("=== ЗАДАНИЕ 6 ВЫПОЛНЕНО ===");
        Pause();
    }

    static async Task CancelAfterDemo()
    {
        Console.WriteLine("--- 6.1. CancellationTokenSource.CancelAfter(3000) ---");

        using CancellationTokenSource cts = new();
        cts.CancelAfter(3000);

        var sw = Stopwatch.StartNew();
        try
        {
            await LongRunningTaskAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"  Операция была отменена через {sw.ElapsedMilliseconds} мс (ожидалось ~3000 мс)");
        }

        Console.WriteLine($"  Состояние источника: {(cts.IsCancellationRequested ? "отменён" : "активен")}\n");
    }

    static async Task LongRunningTaskAsync(CancellationToken token)
    {
        for (int i = 0; i < 20; i++)
        {
            token.ThrowIfCancellationRequested();
            Console.WriteLine($"  Шаг {i + 1} выполнен");
            await Task.Delay(500, token);
        }
    }

    static async Task ManualCancelDemo()
    {
        Console.WriteLine("--- 6.2. Ручная отмена по нажатию клавиши ---");

        using CancellationTokenSource cts = new();
        Task worker = Task.Run(() => LoopWithCancellation(cts.Token, 40, 100));

        if (Console.IsInputRedirected)
        {
            Console.WriteLine("  Ввод перенаправлен, отмена по клавише недоступна — ждём 3 секунды и отменяем программно.");
            await Task.Delay(3000);
            cts.Cancel();
        }
        else
        {
            Console.WriteLine("  Нажмите любую клавишу для отмены...");
            await Task.Run(() =>
            {
                try
                {
                    Console.ReadKey(intercept: true);
                }
                catch (Exception)
                {
                }
            });

            cts.Cancel();
            Console.WriteLine("  Клавиша нажата, вызван cts.Cancel()");
        }

        try
        {
            await worker;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Рабочий цикл завершился с OperationCanceledException\n");
        }
    }

    static void LoopWithCancellation(CancellationToken token, int iterations, int delayMs)
    {
        try
        {
            for (int i = 0; i < iterations; i++)
            {
                token.ThrowIfCancellationRequested();
                Console.WriteLine($"  Итерация {i + 1}");
                Thread.Sleep(delayMs);
            }

            Console.WriteLine("  Цикл завершился полностью, отмена не понадобилась");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Цикл прерван: CancellationToken обнаружил запрос отмены");
            throw;
        }
    }

    static async Task PrimesWithCancel()
    {
        Console.WriteLine($"--- 6.3. Расчёт простых чисел до {PrimeLimit:N0} с возможностью отмены ---");
        Console.WriteLine("  Решето Эратосфена выполняется многократно, чтобы успела сработать отмена.");

        using CancellationTokenSource cts = new();
        cts.CancelAfter(2000);

        var sw = Stopwatch.StartNew();
        long passes = 0;
        try
        {
            passes = await Task.Run(() => RunPrimePasses(cts.Token, int.MaxValue), cts.Token);
            Console.WriteLine($"  Расчёт завершён: {passes} проходов, найдено {CountPrimes(PrimeLimit, CancellationToken.None):N0} простых за {sw.ElapsedMilliseconds} мс");
        }
        catch (OperationCanceledException)
        {
            passes = Interlocked.Read(ref _passes);
            Console.WriteLine($"  Расчёт отменён через {sw.ElapsedMilliseconds} мс, выполнено проходов: {passes}");
        }

        sw.Restart();
        long full = await Task.Run(() => RunPrimePasses(CancellationToken.None, 20));
        sw.Stop();
        Console.WriteLine($"  Тот же расчёт без отмены: {full} проходов за {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Вывод: отмена остановила работу на {passes} из 20 проходов, экономя примерно {(100.0 * passes / 20.0):F0}% времени.\n");
    }

    static long _passes;

    static long RunPrimePasses(CancellationToken token, int maxPasses)
    {
        long passes = 0;
        while (passes < maxPasses)
        {
            token.ThrowIfCancellationRequested();
            CountPrimes(PrimeLimit, token);
            passes++;
            Interlocked.Exchange(ref _passes, passes);
        }

        return passes;
    }

    static long CountPrimes(int limit, CancellationToken token)
    {
        bool[] composite = new bool[limit + 1];
        long count = 0;
        int checks = 0;

        for (int n = 2; n <= limit; n++)
        {
            if ((++checks & 0x3FFF) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            if (composite[n])
            {
                continue;
            }

            count++;
            for (long m = (long)n * n; m <= limit; m += n)
            {
                composite[(int)m] = true;
            }
        }

        return count;
    }

    static async Task LinkedTokensDemo()
    {
        Console.WriteLine("--- 6.4. CreateLinkedTokenSource: объединение нескольких источников отмены ---");

        int callbackCount = 0;

        using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2)))
        using (CancellationTokenSource manual = new())
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, manual.Token))
        {
            using CancellationTokenRegistration reg = linked.Token.Register(() =>
            {
                Interlocked.Increment(ref callbackCount);
                Console.WriteLine($"  Сработал обработчик отмены (всего {callbackCount}), IsCancellationRequested = {linked.IsCancellationRequested}");
            });

            Console.WriteLine("  Случай A: объединены таймаут 2 с и ручной источник, ждём таймаут...");
            var sw = Stopwatch.StartNew();
            try
            {
                await LongRunningTaskAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                string who = timeout.IsCancellationRequested ? "таймаут" : manual.IsCancellationRequested ? "ручная отмена" : "неизвестно";
                Console.WriteLine($"  Операция отменена через {sw.ElapsedMilliseconds} мс, сработал: {who}");
                Console.WriteLine($"  timeout.IsCancellationRequested = {timeout.IsCancellationRequested}, manual.IsCancellationRequested = {manual.IsCancellationRequested}, linked = {linked.IsCancellationRequested}");
            }
        }

        using (CancellationTokenSource manual = new())
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(manual.Token))
        {
            Console.WriteLine("  Случай B: объединён только ручной источник, отменяем его через 1 секунду.");
            Task cancelTask = Task.Run(async () =>
            {
                await Task.Delay(1000);
                manual.Cancel();
            });

            var sw = Stopwatch.StartNew();
            try
            {
                await LongRunningTaskAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"  Операция отменена через {sw.ElapsedMilliseconds} мс, сработала ручная отмена");
            }

            await cancelTask;
            Console.WriteLine($"  linked.IsCancellationRequested = {linked.IsCancellationRequested}, всего обработчиков отмены: {callbackCount}");
        }

        Console.WriteLine("  Вывод: общий токен отменяется при отмене любого из источников.\n");
    }

    static void CheckTokenInsideLoop()
    {
        Console.WriteLine("--- 6.5. Проверка токена без исключений: token.IsCancellationRequested ---");

        using CancellationTokenSource cts = new();
        int processed = 0;

        cts.CancelAfter(1000);
        for (int i = 0; i < 50; i++)
        {
            if (cts.IsCancellationRequested)
            {
                Console.WriteLine($"  Цикл мягко остановлен на итерации {i}, обработано {processed} элементов");
                break;
            }

            processed++;
            Thread.Sleep(100);
        }

        Console.WriteLine("  Вывод: IsCancellationRequested не бросает исключений — удобно для частичной обработки.\n");
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
