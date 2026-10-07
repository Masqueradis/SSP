using System.Diagnostics;
using System.Text;

namespace Lab5Task11;

static class Program
{
    const string MutexName = "Global\\Lab5Task11_DemoMutex";
    const string LocalMutexName = "Lab5Task11_LocalMutex";

    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--child")
        {
            int exitCode = Child(args.Length > 1 ? args[1] : "wait");
            Environment.Exit(exitCode);
            return;
        }

        Console.WriteLine("=== ЗАДАНИЕ 11. ИМЕНОВАННЫЕ (СИСТЕМНЫЕ) МЬЮТЕКСЫ ===");
        Console.WriteLine($"Имя мьютекса: {MutexName}");
        Console.WriteLine("Именованный мьютекс — это объект ядра, общий для всех процессов системы,");
        Console.WriteLine("поэтому он работает не только между потоками, но и между программами.\n");

        LocalVsGlobal();
        InProcessContention();
        CrossProcessBlocking();
        AbandonedMutex();

        Console.WriteLine("=== ЗАДАНИЕ 11 ВЫПОЛНЕНО ===");
        Pause();
    }

    static void LocalVsGlobal()
    {
        Console.WriteLine("--- 11.1. Префиксы имён: Local и Global ---");
        Console.WriteLine($"  Windows: Local\\ или пустое имя — мьютекс виден только внутри одной сессии (одного входа в систему).");
        Console.WriteLine($"  Windows: Global\\ — мьютекс виден всем сессиям и всем процессам машины.");
        Console.WriteLine($"  Имя без префикса ({LocalMutexName}) по умолчанию считается локальным.");
        Console.WriteLine($"  Linux: понятия сессии нет, именованные мьютексы разделяются по имени объекта (файл в /tmp/.dotnet/shm),");
        Console.WriteLine($"  поэтому префикс меняет имя объекта, но не даёт отдельной области видимости.\n");

        using Mutex local = new(false, LocalMutexName, out bool createdLocal);
        Console.WriteLine($"  Создан новый локальный мьютекс {LocalMutexName}: {createdLocal}");

        Mutex? same = null;
        try
        {
            same = new Mutex(false, LocalMutexName, out bool createdSecond);
            Console.WriteLine($"  Второй мьютекс с тем же именем: создан заново = {createdSecond} (то есть это тот же объект, а не копия)");
        }
        catch (AbandonedMutexException)
        {
            Console.WriteLine("  Мьютекс был оставлен другим процессом");
        }
        finally
        {
            same?.Dispose();
        }

        Console.WriteLine();
    }

    static void InProcessContention()
    {
        Console.WriteLine("--- 11.2. Синхронизация потоков одного процесса ---");

        using Mutex mutex = new(false, MutexName, out bool createdNew);
        Console.WriteLine($"  Мьютекс {MutexName} создан заново: {createdNew}");

        bool acquired = mutex.WaitOne(0);
        Console.WriteLine($"  Текущий поток захватил мьютекс: {acquired}");

        bool recursive = mutex.WaitOne(0);
        Console.WriteLine($"  Тот же поток захватил мьютекс повторно: {recursive} (мьютекс реентерабельный)");

        int secondThreadResult = -1;
        Thread t = new Thread(() =>
        {
            secondThreadResult = mutex.WaitOne(300) ? 1 : 0;
            if (secondThreadResult == 1)
            {
                mutex.ReleaseMutex();
            }
        })
        {
            Name = "Thread2"
        };
        t.Start();
        t.Join();

        Console.WriteLine($"  Другой поток попытался взять мьютекс на 300 мс: {(secondThreadResult == 1 ? "успешно" : "отказ, мьютекс занят")}");

        mutex.ReleaseMutex();
        mutex.ReleaseMutex();
        Console.WriteLine("  Мьютекс освобождён (два ReleaseMutex подряд: по одному на каждый захват).");

        bool nowFree = mutex.WaitOne(0);
        Console.WriteLine($"  После освобождения мьютекс снова доступен: {nowFree}");
        if (nowFree)
        {
            mutex.ReleaseMutex();
        }

        Console.WriteLine();
    }

    static void CrossProcessBlocking()
    {
        Console.WriteLine("--- 11.3. Настоящая проверка между процессами ---");
        Console.WriteLine($"  Родитель захватывает {MutexName}, затем запускает дочерний процесс.");
        Console.WriteLine("  Дочерний процесс — это тот же самый исполняемый файл с аргументом --child.\n");

        using Mutex mutex = new(false, MutexName);

        if (!mutex.WaitOne(0))
        {
            Console.WriteLine("  Мьютекс уже занят другим процессом, пункт пропущен.");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("  Родитель: мьютекс захвачен.");
        string blocked = RunChild("wait", timeoutMs: 3000);
        Console.WriteLine($"  Дочерний процесс при захваченном мьютексе:{Environment.NewLine}{blocked}");

        mutex.ReleaseMutex();
        Console.WriteLine("  Родитель: мьютекс освобождён.");

        string free = RunChild("wait", timeoutMs: 3000);
        Console.WriteLine($"  Дочерний процесс при свободном мьютексе:{Environment.NewLine}{free}");
        Console.WriteLine("  Вывод: мьютекс действительно общий для процессов, а не только для потоков.");
        Console.WriteLine();
    }

    static void AbandonedMutex()
    {
        Console.WriteLine("--- 11.4. Покинутый мьютекс (AbandonedMutexException) ---");
        Console.WriteLine("  Если процесс упал, удерживая мьютекс, следующий владелец получит");
        Console.WriteLine("  AbandonedMutexException. Мьютекс при этом всё равно передаётся ему.\n");

        using Mutex mutex = new(false, MutexName);

        Process child = StartChild("hold");
        Thread.Sleep(1500);

        if (child.HasExited)
        {
            Console.WriteLine("  Дочерний процесс уже завершился, тест не удался.");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("  Дочерний процесс удерживает мьютекс, убиваем его принудительно.");
        child.Kill(entireProcessTree: true);
        child.WaitForExit();

        try
        {
            bool got = mutex.WaitOne(3000);
            Console.WriteLine($"  Родитель получил мьютекс после гибели владельца: {got} (исключения не было)");
            if (got)
            {
                mutex.ReleaseMutex();
            }
        }
        catch (AbandonedMutexException)
        {
            Console.WriteLine("  Получено ожидаемое исключение AbandonedMutexException: мьютекс был покинут.");
            Console.WriteLine("  Владельцем мьютекса стал текущий поток, его нужно освободить через ReleaseMutex.");
            mutex.ReleaseMutex();
        }

        Console.WriteLine();
    }

    static int Child(string mode)
    {
        Console.WriteLine($"    [дочерний процесс pid {Environment.ProcessId}]");

        using Mutex mutex = new(false, MutexName);
        try
        {
            bool got = mode == "hold"
                ? mutex.WaitOne(5000)
                : mutex.WaitOne(1200);

            if (!got)
            {
                Console.WriteLine("    Не удалось захватить мьютекс за 1200 мс — он удерживается другим процессом.");
                return 2;
            }

            Console.WriteLine("    Мьютекс захвачен.");
            if (mode == "hold")
            {
                Console.WriteLine("    Держу мьютекс и засыпаю на 30 секунд, меня должны убить.");
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }

            mutex.ReleaseMutex();
            Console.WriteLine("    Мьютекс освобождён.");
            return 0;
        }
        catch (AbandonedMutexException)
        {
            Console.WriteLine("    Мьютекс был покинут предыдущим владельцем, но передан мне.");
            mutex.ReleaseMutex();
            return 3;
        }
    }

    static Process StartChild(string mode)
    {
        ProcessStartInfo psi = new()
        {
            FileName = Environment.ProcessPath ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (psi.FileName == "dotnet")
        {
            psi.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        psi.ArgumentList.Add("--child");
        psi.ArgumentList.Add(mode);
        return Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить дочерний процесс");
    }

    static string RunChild(string mode, int timeoutMs)
    {
        using Process child = StartChild(mode);
        StringBuilder output = new();
        child.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine("    " + e.Data);
            }
        };
        child.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.AppendLine("    [ошибка] " + e.Data);
            }
        };
        child.BeginOutputReadLine();
        child.BeginErrorReadLine();

        if (!child.WaitForExit(timeoutMs))
        {
            child.Kill(entireProcessTree: true);
            output.AppendLine("    [дочерний процесс не ответил вовремя]");
            return output.ToString();
        }

        return output.ToString();
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
