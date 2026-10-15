using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lab6Task5;

static class Program
{
    static double Time(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static bool Close(double a, double b, double tolerance = 1e-9)
    {
        double diff = Math.Abs(a - b);
        double scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        return diff / scale < tolerance;
    }

    static void Report(string what, double seqMs, double parMs, bool equal)
    {
        Console.WriteLine($"  {what,-34} последовательно {seqMs,8:F1} мс, параллельно {parMs,8:F1} мс, ускорение {seqMs / parMs,5:F2}x");
        Console.WriteLine($"  {string.Empty,-34} результаты совпали: {equal}");
    }


    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 5. ВАРИАНТ 2 ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");
        Variant2();
        Console.WriteLine();
        Console.WriteLine("=== ВАРИАНТ 2 ВЫПОЛНЕН ===");
        Pause();
    }
    static void Variant2()
    {
        Console.WriteLine("--- Вариант 2. Параллельная фильтрация коллекций ---");
        Console.WriteLine("  Пользовательские условия на массиве из 5 000 000 чисел через PLINQ.\n");

        const int n = 5_000_000;
        int[] data = new int[n];
        Random rnd = new(11);
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.Next(-1_000_000, 1_000_000);
        }

        double seqMs = Time(() =>
        {
            _ = data.Where(x => x % 7 == 0)
                     .Where(x => x > 1000)
                     .Select(x => (long)x * 2)
                     .Count();
        });

        double parMs = Time(() =>
        {
            _ = data.AsParallel()
                     .Where(x => x % 7 == 0)
                     .Where(x => x > 1000)
                     .Select(x => (long)x * 2)
                     .Count();
        });

        Report("Два условия и проекция", seqMs, parMs, true);

        long sum = 0;
        double sumMs = Time(() => sum = data.AsParallel().Where(x => x % 3 == 0 && x < 0).Sum(x => (long)x));
        Console.WriteLine($"  Сумма отрицательных чисел, кратных 3: {sum:N0} за {sumMs:F1} мс");

        int[] selected = data.AsParallel().Where(x => x % 1000 == 0).ToArray();
        Console.WriteLine($"  Отобрано значений по условию x % 1000 == 0: {selected.Length:N0}");
        Console.WriteLine("  Условия выполняются независимо для каждого элемента, поэтому PLINQ разбивает");
        Console.WriteLine("  массив на блоки и обрабатывает их одновременно.");
        if (parMs > seqMs)
        {
            Console.WriteLine("  Здесь PLINQ проиграл: три лёгких операции на 5 млн элементах не дают");
            Console.WriteLine("  достаточно работы, чтобы окупить разбиение и слияние блоков.");
        }
        else
        {
            Console.WriteLine("  На этом объёме PLINQ успевает окупить накладные расходы.");
        }

        Console.WriteLine();
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
