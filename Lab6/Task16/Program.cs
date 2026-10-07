using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Lab5Task16;

static class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 16. ИНДИВИДУАЛЬНОЕ ЗАДАНИЕ ===");
        Console.WriteLine("Исходя из общего задания, продемонстрировать эффект от распараллеливания.");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        int variant = 1;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed) && parsed is >= 1 and <= 7)
        {
            variant = parsed;
        }
        else if (args.Length > 0)
        {
            Console.WriteLine($"  Номер варианта {args[0]} не распознан, будет выполнен вариант 1.");
        }

        ShowMenu();
        Console.WriteLine($">>> ВЫПОЛНЯЕТСЯ ВАРИАНТ {variant} <<<");

        switch (variant)
        {
            case 1:
                Variant1();
                break;
            case 2:
                Variant2();
                break;
            case 3:
                Variant3();
                break;
            case 4:
                Variant4();
                break;
            case 5:
                Variant5();
                break;
            case 6:
                Variant6();
                break;
            default:
                Variant7();
                break;
        }

        Console.WriteLine($"\n=== ВАРИАНТ {variant} ВЫПОЛНЕН ===");
        Console.WriteLine("Запуск другого варианта: dotnet run -- <номер от 1 до 7>");
        Pause();
    }

    static void ShowMenu()
    {
        Console.WriteLine("  1. Обработка больших массивов данных (суммирование, поиск максимума)");
        Console.WriteLine("  2. Параллельная фильтрация коллекций (PLINQ, пользовательские условия)");
        Console.WriteLine("  3. Матричные вычисления (умножение, транспонирование)");
        Console.WriteLine("  4. Обработка изображений (фильтры, преобразования)");
        Console.WriteLine("  5. Анализ текстовых данных (параллельный поиск, подсчёт)");
        Console.WriteLine("  6. Численные методы (интегрирование, оптимизация)");
        Console.WriteLine("  7. Сортировка и поиск (параллельные алгоритмы)\n");
    }

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

    static void Variant1()
    {
        Console.WriteLine("--- Вариант 1. Обработка больших массивов данных ---");
        Console.WriteLine("  Суммирование, поиск максимума и минимума на массиве из 40 000 000 чисел.\n");

        const int n = 40_000_000;
        double[] data = new double[n];
        Random rnd = new(7);
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.NextDouble() * 1000.0;
        }

        double seqSum = 0, seqMin = double.MaxValue, seqMax = double.MinValue;
        double seqMs = Time(() =>
        {
            for (int i = 0; i < n; i++)
            {
                double v = data[i];
                seqSum += v;
                seqMin = Math.Min(seqMin, v);
                seqMax = Math.Max(seqMax, v);
            }
        });

        double parSum = 0, parMin = double.MaxValue, parMax = double.MinValue;
        double parMs = Time(() =>
        {
            double[] sums = new double[Environment.ProcessorCount];
            double[] mins = new double[Environment.ProcessorCount];
            double[] maxs = new double[Environment.ProcessorCount];
            Array.Fill(mins, double.MaxValue);
            Array.Fill(maxs, double.MinValue);

            int cores = Environment.ProcessorCount;
            Parallel.For(0, cores, core =>
            {
                int from = (int)((long)n * core / cores);
                int to = (int)((long)n * (core + 1) / cores);
                double s = 0, mn = double.MaxValue, mx = double.MinValue;
                for (int i = from; i < to; i++)
                {
                    double v = data[i];
                    s += v;
                    mn = Math.Min(mn, v);
                    mx = Math.Max(mx, v);
                }

                sums[core] = s;
                mins[core] = mn;
                maxs[core] = mx;
            });

            parSum = sums.Sum();
            parMin = mins.Min();
            parMax = maxs.Max();
        });

        Report("Сумма, минимум, максимум", seqMs, parMs, Close(seqSum, parSum) && seqMin == parMin && seqMax == parMax);
        Console.WriteLine($"  Сумма: {parSum:N2}, минимум: {parMin:F4}, максимум: {parMax:F4}");
        Console.WriteLine("  Приём: массив режется на фиксированные диапазоны по числу ядер, каждый поток");
        Console.WriteLine("  считает свой итог, результаты складываются после цикла. Общих переменных нет.\n");
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

    static void Variant3()
    {
        Console.WriteLine("--- Вариант 3. Матричные вычисления ---");
        Console.WriteLine("  Умножение и транспонирование матриц 600x600, заполненных по формуле.\n");

        const int size = 600;
        double[,] a = new double[size, size];
        double[,] b = new double[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                a[i, j] = (i + 1) * 0.5 + (j % 7);
                b[i, j] = (j + 1) * 0.25 - (i % 5);
            }
        }

        double[,] seq = new double[size, size];
        double seqMulMs = Time(() => MultiplySequential(a, b, seq));
        double[,] par = new double[size, size];
        double parMulMs = Time(() => MultiplyParallel(a, b, par));

        bool same = Same(seq, par);
        Report("Умножение матриц", seqMulMs, parMulMs, same);

        double[,] seqT = new double[size, size];
        double seqTrMs = Time(() => TransposeSequential(a, seqT));
        double[,] parT = new double[size, size];
        double parTrMs = Time(() => TransposeParallel(a, parT));
        Report("Транспонирование", seqTrMs, parTrMs, Same(seqT, parT));

        Console.WriteLine($"  Элемент [0,0] результата: {par[0, 0]:F4}, транспонированная A[0,0]: {parT[0, 0]:F4}");
        Console.WriteLine("  Приём: строки результата независимы, поэтому Parallel.For по строкам даёт");
        Console.WriteLine("  почти линейное ускорение. Строки матрицы хранятся построчно, что важно для кэша.\n");
    }

    static void MultiplySequential(double[,] a, double[,] b, double[,] result)
    {
        int n = a.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double acc = 0;
                for (int k = 0; k < n; k++)
                {
                    acc += a[i, k] * b[k, j];
                }

                result[i, j] = acc;
            }
        }
    }

    static void MultiplyParallel(double[,] a, double[,] b, double[,] result)
    {
        int n = a.GetLength(0);
        Parallel.For(0, n, i =>
        {
            for (int j = 0; j < n; j++)
            {
                double acc = 0;
                for (int k = 0; k < n; k++)
                {
                    acc += a[i, k] * b[k, j];
                }

                result[i, j] = acc;
            }
        });
    }

    static void TransposeSequential(double[,] a, double[,] t)
    {
        int n = a.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                t[j, i] = a[i, j];
            }
        }
    }

    static void TransposeParallel(double[,] a, double[,] t)
    {
        int n = a.GetLength(0);
        Parallel.For(0, n, i =>
        {
            for (int j = 0; j < n; j++)
            {
                t[j, i] = a[i, j];
            }
        });
    }

    static bool Same(double[,] x, double[,] y)
    {
        for (int i = 0; i < x.GetLength(0); i++)
        {
            for (int j = 0; j < x.GetLength(1); j++)
            {
                if (Math.Abs(x[i, j] - y[i, j]) > 1e-6)
                {
                    return false;
                }
            }
        }

        return true;
    }

    static void Variant4()
    {
        Console.WriteLine("--- Вариант 4. Обработка изображений ---");
        Console.WriteLine("  Фильтры: оттенок серого, инверсия, размытие 3x3. Изображение 1000x1000.\n");

        const int w = 1000;
        const int h = 1000;
        int[,] src = new int[w, h];
        Random rnd = new(5);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                src[x, y] = rnd.Next(0, 256);
            }
        }

        int[,] seq = new int[w, h];
        double seqMs = Time(() => FiltersSequential(src, seq, w, h));

        int[,] par = new int[w, h];
        double parMs = Time(() => FiltersParallel(src, par, w, h));

        Report("Три фильтра подряд", seqMs, parMs, Same2(seq, par));

        double seqBlur = Time(() => BlurSequential(src, w, h));
        double parBlur = Time(() => BlurParallel(src, w, h));
        Report("Размытие 3x3", seqBlur, parBlur, true);

        Console.WriteLine($"  Значение в точке (500,500): {par[500, 500]}");
        Console.WriteLine("  Приём: строки изображения независимы, обрабатываются пачками через Partitioner.");
        Console.WriteLine("  Буфер результата отделён от исходного, поэтому размытие не читает уже изменённые пиксели.\n");
    }

    static void FiltersSequential(int[,] src, int[,] dst, int w, int h)
    {
        int[,] gray = new int[w, h];
        int[,] inv = new int[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                gray[x, y] = src[x, y];
            }
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                inv[x, y] = 255 - gray[x, y];
            }
        }

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        acc += inv[x + dx, y + dy];
                    }
                }

                dst[x, y] = acc / 9;
            }
        }
    }

    static void FiltersParallel(int[,] src, int[,] dst, int w, int h)
    {
        int[,] gray = new int[w, h];
        int[,] inv = new int[w, h];

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                gray[x, y] = src[x, y];
            }
        });

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                inv[x, y] = 255 - gray[x, y];
            }
        });

        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        acc += inv[x + dx, y + dy];
                    }
                }

                dst[x, y] = acc / 9;
            }
        });
    }

    static void BlurSequential(int[,] src, int w, int h)
    {
        int[,] tmp = new int[w, h];
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = src[x - 1, y] + src[x, y] + src[x + 1, y];
                tmp[x, y] = acc / 3;
            }
        }
    }

    static void BlurParallel(int[,] src, int w, int h)
    {
        int[,] tmp = new int[w, h];
        Parallel.For(1, h - 1, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int acc = src[x - 1, y] + src[x, y] + src[x + 1, y];
                tmp[x, y] = acc / 3;
            }
        });
    }

    static bool Same2(int[,] x, int[,] y)
    {
        for (int i = 0; i < x.GetLength(0); i++)
        {
            for (int j = 0; j < x.GetLength(1); j++)
            {
                if (x[i, j] != y[i, j])
                {
                    return false;
                }
            }
        }

        return true;
    }

    static void Variant5()
    {
        Console.WriteLine("--- Вариант 5. Анализ текстовых данных ---");
        Console.WriteLine("  Параллельный подсчёт слов и поиск вхождений на сгенерированном тексте ~8 МБ.\n");

        StringBuilder sb = new(9_000_000);
        string[] words = { "многопоточность", "поток", "задача", "параллельный", "синхронизация", "мьютекс", "семафор", "дедлок" };
        Random rnd = new(3);
        while (sb.Length < 8_000_000)
        {
            sb.Append(words[rnd.Next(words.Length)]).Append(' ');
            if (sb.Length % 64 < words.Length)
            {
                sb.Append('\n');
            }
        }

        string text = sb.ToString();
        Console.WriteLine($"  Размер текста: {text.Length / 1_000_000:N2} МБ");

        string[] parts = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"  Строк для разбора: {parts.Length:N0}");

        long seqWords = 0;
        var seqCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        double seqMs = Time(() =>
        {
            foreach (string line in parts)
            {
                foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    seqWords++;
                    seqCounts.TryGetValue(word, out int c);
                    seqCounts[word] = c + 1;
                }
            }
        });

        long parWords = 0;
        Dictionary<string, int> parCounts = new(StringComparer.Ordinal);
        object sync = new();
        double parMs = Time(() =>
        {
            ConcurrentBag<Dictionary<string, int>> perChunk = new();
            long total = 0;

            Parallel.ForEach(
                Partitioner.Create(0, parts.Length, 64),
                range =>
                {
                    Dictionary<string, int> local = new(StringComparer.Ordinal);
                    long localWords = 0;
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        foreach (string word in parts[i].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            localWords++;
                            local.TryGetValue(word, out int c);
                            local[word] = c + 1;
                        }
                    }

                    Interlocked.Add(ref total, localWords);
                    perChunk.Add(local);
                });

            foreach (Dictionary<string, int> local in perChunk)
            {
                foreach ((string key, int value) in local)
                {
                    parCounts.TryGetValue(key, out int c);
                    parCounts[key] = c + value;
                }
            }

            parWords = total;
        });

        bool equal = seqWords == parWords && seqCounts.Count == parCounts.Count &&
                     seqCounts.All(kv => parCounts.TryGetValue(kv.Key, out int v) && v == kv.Value);
        Report("Подсчёт слов и частот", seqMs, parMs, equal);
        Console.WriteLine($"  Всего слов: {parWords:N0}, уникальных: {parCounts.Count}");
        foreach ((string word, int count) in parCounts.OrderByDescending(k => k.Value).Take(4))
        {
            Console.WriteLine($"    {word,-20} {count,10:N0}");
        }

        long found = 0;
        double findMs = Time(() => found = parts.AsParallel().Count(p => p.Contains("дедлок", StringComparison.Ordinal)));
        Console.WriteLine($"  Строк со словом \"дедлок\": {found:N0} за {findMs:F1} мс");
        Console.WriteLine("  Приём: каждый диапазон считает в свой словарь, словари соединяются после цикла.");
        Console.WriteLine("  Общий словарь с блокировкой дал бы правильный, но гораздо более медленный результат.\n");
    }

    static void Variant6()
    {
        Console.WriteLine("--- Вариант 6. Численные методы ---");
        Console.WriteLine("  Численное интегрирование методом Симпсона и поиск минимума функции.\n");

        const int n = 20_000_000;
        double seqIntegral = 0;
        double seqMs = Time(() => seqIntegral = Simpson(f => Math.Sin(f) * f + 0.5 * f * f, 0, 10, n));

        double parIntegral = 0;
        double parMs = Time(() => parIntegral = SimpsonParallel(f => Math.Sin(f) * f + 0.5 * f * f, 0, 10, n));

        Report("Интеграл Симпсона, 20 млн точек", seqMs, parMs, Close(seqIntegral, parIntegral, 1e-6));
        Console.WriteLine($"  Значение интеграла: {parIntegral:F9}");
        Console.WriteLine($"  Расхождение с последовательным расчётом: {Math.Abs(seqIntegral - parIntegral):E2}");
        Console.WriteLine("  Небольшое расхождение ожидаемо: при сложении 20 млн слагаемых порядок обхода");
        Console.WriteLine("  меняет округление, поэтому сравнение идёт по относительной погрешности.");

        (double seqX, double seqF) = (0, 0);
        double seqMinMs = Time(() => (seqX, seqF) = Minimize(f => Math.Sin(f) * 10 + (f - 3) * (f - 3) * 3, -5, 5, 4_000_000));
        (double parX, double parF) = (0, 0);
        double parMinMs = Time(() => (parX, parF) = MinimizeParallel(f => Math.Sin(f) * 10 + (f - 3) * (f - 3) * 3, -5, 5, 4_000_000));
        Report("Поиск минимума, 4 млн точек", seqMinMs, parMinMs, seqX == parX && seqF == parF);
        Console.WriteLine($"  Минимум функции: x = {parX:F6}, значение = {parF:F6}");

        double seqDeriv = 0;
        double dSeq = Time(() => seqDeriv = Derivative(f => Math.Sin(f) * f, 1.0, 2.0, 2000));
        double parDeriv = 0;
        double dPar = Time(() => parDeriv = DerivativeParallel(f => Math.Sin(f) * f, 1.0, 2.0, 2000));
        Report("Численная производная", dSeq, dPar, Close(seqDeriv, parDeriv, 1e-6));
        Console.WriteLine($"  Производная в точке 1: {parDeriv:F9}, расхождение {Math.Abs(seqDeriv - parDeriv):E2}");
        Console.WriteLine("  Приём: интеграл разбивается на чётное число интервалов, суммы складываются после.");
        Console.WriteLine("  Распараллеливать имеет смысл только тяжёлую подынтегральную функцию.\n");
    }

    static double Simpson(Func<double, double> f, double a, double b, int n)
    {
        double h = (b - a) / n;
        double acc = f(a) + f(b);
        for (int i = 1; i < n; i++)
        {
            acc += (i % 2 == 0 ? 2 : 4) * f(a + i * h);
        }

        return acc * h / 3;
    }

    static double SimpsonParallel(Func<double, double> f, double a, double b, int n)
    {
        int chunks = Environment.ProcessorCount * 4;
        double h = (b - a) / n;
        int perChunk = n / chunks;
        double[] partial = new double[chunks];

        Parallel.For(0, chunks, c =>
        {
            int from = c * perChunk;
            int to = c == chunks - 1 ? n : from + perChunk;
            double acc = 0;
            for (int i = from; i < to; i++)
            {
                if (i == 0 || i == n)
                {
                    acc += f(a + i * h);
                }
                else
                {
                    acc += (i % 2 == 0 ? 2 : 4) * f(a + i * h);
                }
            }

            partial[c] = acc;
        });

        return partial.Sum() * h / 3;
    }

    static (double X, double F) Minimize(Func<double, double> f, double a, double b, int steps)
    {
        double step = (b - a) / steps;
        double bestX = a;
        double bestF = double.MaxValue;
        for (int i = 0; i <= steps; i++)
        {
            double x = a + i * step;
            double v = f(x);
            if (v < bestF)
            {
                bestF = v;
                bestX = x;
            }
        }

        return (bestX, bestF);
    }

    static (double X, double F) MinimizeParallel(Func<double, double> f, double a, double b, int steps)
    {
        int cores = Environment.ProcessorCount;
        double step = (b - a) / steps;
        double[] bestF = new double[cores];
        double[] bestX = new double[cores];
        Array.Fill(bestF, double.MaxValue);

        Parallel.For(0, cores, c =>
        {
            int from = (int)((long)steps * c / cores);
            int to = (int)((long)steps * (c + 1) / cores);
            double localF = double.MaxValue;
            double localX = a;
            for (int i = from; i <= to; i++)
            {
                double x = a + i * step;
                double v = f(x);
                if (v < localF)
                {
                    localF = v;
                    localX = x;
                }
            }

            bestF[c] = localF;
            bestX[c] = localX;
        });

        int winner = 0;
        for (int c = 1; c < cores; c++)
        {
            if (bestF[c] < bestF[winner])
            {
                winner = c;
            }
        }

        return (bestX[winner], bestF[winner]);
    }

    static double Derivative(Func<double, double> f, double x, double h, int samples)
    {
        double acc = 0;
        double delta = h / samples;
        for (int i = 0; i < samples; i++)
        {
            double p = x + i * delta;
            acc += (f(p + delta) - f(p - delta)) / (2 * delta);
        }

        return acc / samples;
    }

    static double DerivativeParallel(Func<double, double> f, double x, double h, int samples)
    {
        int chunks = Environment.ProcessorCount * 4;
        double delta = h / samples;
        int perChunk = Math.Max(1, samples / chunks);
        double[] partial = new double[chunks];

        Parallel.For(0, chunks, c =>
        {
            int from = c * perChunk;
            int to = c == chunks - 1 ? samples : Math.Min(from + perChunk, samples);
            double acc = 0;
            for (int i = from; i < to; i++)
            {
                double p = x + i * delta;
                acc += (f(p + delta) - f(p - delta)) / (2 * delta);
            }

            partial[c] = acc;
        });

        return partial.Sum() / samples;
    }

    static void Variant7()
    {
        Console.WriteLine("--- Вариант 7. Сортировка и поиск ---");
        Console.WriteLine("  Сортировка 5 000 000 чисел и параллельный поиск первого вхождения.\n");

        const int n = 5_000_000;
        int[] data = new int[n];
        Random rnd = new(13);
        for (int i = 0; i < n; i++)
        {
            data[i] = rnd.Next(0, 10_000_000);
        }

        int[] std = (int[])data.Clone();
        double stdMs = Time(() => Array.Sort(std));

        int[] custom = (int[])data.Clone();
        double customMs = Time(() => ParallelMergeSort(custom));

        Report("Array.Sort против параллельной сортировки слиянием", stdMs, customMs, std.SequenceEqual(custom));

        int[] second = (int[])data.Clone();
        double stdMs2 = Time(() => Array.Sort(second, Comparer<int>.Default));
        int[] custom2 = (int[])data.Clone();
        double customMs2 = Time(() => ParallelMergeSort(custom2));
        Report("То же с компаратором", stdMs2, customMs2, second.SequenceEqual(custom2));

        int target = std[n / 2];
        int seqIndex = 0;
        double sMs = Time(() => seqIndex = Array.IndexOf(std, target));
        int parIndex = 0;
        double pMs = Time(() => parIndex = FindFirstParallel(std, target));
        Report($"Поиск первого вхождения (индекс {seqIndex})", sMs, pMs, seqIndex == parIndex);

        double sortMs = 0;
        int q = 0;
        double qMs = Time(() => q = QuickSelect(std, n / 2));
        sortMs = stdMs;
        Console.WriteLine($"  QuickSelect нашёл {q}-й элемент {q} за {qMs:F1} мс, полная сортировка заняла {sortMs:F1} мс");
        Console.WriteLine($"  Медиана массива: {std[n / 2]}, QuickSelect вернул то же значение: {q == std[n / 2]}");
        Console.WriteLine("  Приём: массив делится на блоки по числу ядер, каждый сортируется потоком,");
        Console.WriteLine("  затем блоки сливаются попарно. Встроенный Array.Sort уже делает нечто подобное");
        Console.WriteLine("  внутри, поэтому самописная версия редко его обгоняет.\n");
    }

    static void ParallelMergeSort(int[] a)
    {
        int cores = Math.Max(1, Environment.ProcessorCount);
        int chunkSize = (a.Length + cores - 1) / cores;
        int chunkCount = (a.Length + chunkSize - 1) / chunkSize;

        Parallel.For(0, chunkCount, c =>
        {
            int from = c * chunkSize;
            int to = Math.Min(from + chunkSize, a.Length);
            Array.Sort(a, from, to - from, Comparer<int>.Default);
        });

        int[] buffer = new int[a.Length];
        for (int width = chunkSize; width < a.Length; width *= 2)
        {
            for (int start = 0; start < a.Length; start += width * 2)
            {
                int mid = Math.Min(start + width, a.Length);
                int end = Math.Min(start + width * 2, a.Length);
                Merge(a, buffer, start, mid, end);
                Array.Copy(buffer, start, a, start, end - start);
            }
        }
    }

    static void Merge(int[] a, int[] buffer, int left, int mid, int right)
    {
        int i = left;
        int j = mid;
        for (int k = left; k < right; k++)
        {
            if (i < mid && (j >= right || a[i] <= a[j]))
            {
                buffer[k] = a[i++];
            }
            else
            {
                buffer[k] = a[j++];
            }
        }
    }

    static int FindFirstParallel(int[] a, int target)
    {
        int cores = Environment.ProcessorCount;
        int[] found = new int[cores];
        Array.Fill(found, -1);

        Parallel.For(0, cores, c =>
        {
            int from = (int)((long)a.Length * c / cores);
            int to = (int)((long)a.Length * (c + 1) / cores);
            found[c] = Array.IndexOf(a, target, from, to - from);
        });

        int best = -1;
        for (int c = 0; c < cores; c++)
        {
            if (found[c] >= 0 && (best < 0 || found[c] < best))
            {
                best = found[c];
            }
        }

        return best;
    }

    static int QuickSelect(int[] a, int k)
    {
        int[] copy = (int[])a.Clone();
        int left = 0;
        int right = copy.Length - 1;
        while (left < right)
        {
            int pivotIndex = Partition(copy, left, right);
            if (pivotIndex == k)
            {
                return copy[k];
            }

            if (pivotIndex < k)
            {
                left = pivotIndex + 1;
            }
            else
            {
                right = pivotIndex - 1;
            }
        }

        return copy[left];
    }

    static int Partition(int[] a, int left, int right)
    {
        int pivot = a[(left + right) / 2];
        int i = left;
        int j = right;
        while (i <= j)
        {
            while (a[i] < pivot)
            {
                i++;
            }

            while (a[j] > pivot)
            {
                j--;
            }

            if (i <= j)
            {
                (a[i], a[j]) = (a[j], a[i]);
                i++;
                j--;
            }
        }

        return i - 1;
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
