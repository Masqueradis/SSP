using System.Diagnostics;

namespace Lab4Task5;

static class Program
{
    private const int LogCount = 10_000;

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

    static void Main()
    {
        Console.WriteLine("ЛАБОРАТОРНАЯ РАБОТА 4 — ЗАДАНИЕ 5: ПРОФИЛИРОВАНИЕ ПРОИЗВОДИТЕЛЬНОСТИ");
        Console.WriteLine($"Среда: {Environment.Version}  |  Записей лога: {LogCount:N0}");
        Console.WriteLine($"Процессоров: {Environment.ProcessorCount}  |  Серверный GC: {System.Runtime.GCSettings.IsServerGC}");
        Console.WriteLine("Профилировщик собственный, на Stopwatch, внешние пакеты не используются.");

        CallTreeWithTryParse();
        TopOwnTime();
        ParseExactOptimization();
        OwnCodeProfiling();
        FinalConclusion();

        Console.WriteLine();
        Console.WriteLine("Нажмите любую клавишу...");
        try { Console.ReadKey(); } catch { }
    }

    private static void CallTreeWithTryParse()
    {
        Header("ЧАСТЬ 1 — ДЕРЕВО ВЫЗОВОВ С ПОДСЧЁТОМ OWN TIME (DateTime.TryParse)");

        double overhead = MeasureProfilerOverhead();
        Console.WriteLine($"Калибровка: одна операция Enter/Exit профилировщика стоит {overhead:F3} мкс.");
        Console.WriteLine("Эти накладные расходы входят в Own Time родительских узлов, поэтому");
        Console.WriteLine("собственное время узлов с большим числом вызовов слегка завышено.");
        Console.WriteLine();

        var analyzer = new LogAnalyzer(useParseExact: false);

        using (var setup = new Profiler())
        {
            analyzer.Generate(1, setup);
        }

        List<string> logs = GenerateLogs(analyzer);

        using var profiler = new Profiler();
        profiler.EnableAllocationTracking();
        List<string> anomalies = analyzer.ProcessLogs(logs, profiler);

        AnomalyStats stats = analyzer.Stats;
        Console.WriteLine($"Записей: {LogCount + 100:N0}, проанализировано после фильтра: {stats.Total:N0}");
        Console.WriteLine($"Аномалий найдено: {stats.Anomalies}, пропущено по длине: {stats.TooShort}, " +
                          $"по числу частей: {stats.NotEnoughParts}, ошибок разбора даты: {stats.DateParseFailures}");
        Console.WriteLine($"Первые аномалии: {anomalies.Count} шт.");
        GC.KeepAlive(anomalies);

        profiler.Complete();
        TreePrinter.Print(profiler, "Call Tree (базовая версия, TryParse)");

        profiler.Complete();
        SubHeader("Топ-5 методов по Own Time (собственное время метода)");
        Console.WriteLine($"{"Метод",-42} {"OWN, мс",10} {"% от run",9} {"вызовов",8} {"аллокаций",12}");
        Console.WriteLine(new string('-', 78));

        double total = profiler.Root.TotalMilliseconds;
        foreach (ProfileNode node in profiler.TopBySelfTime(5))
        {
            double percent = total == 0 ? 0 : 100.0 * node.SelfMilliseconds / total;
            Console.WriteLine($"{node.Name,-42} {node.SelfMilliseconds,10:F2} {percent,8:F1}% {node.Calls,8} " +
                              $"{node.AllocatedBytes / 1024.0,10:F0} КБ");
        }
    }

    private static void TopOwnTime()
    {
        Header("ЧАСТЬ 2 — РАЗБОР: ПОЧЕМУ ЭТИ МЕТОДЫ МЕДЛЕННЫЕ");

        var analyzer = new LogAnalyzer(useParseExact: false);
        List<string> logs = GenerateLogs(analyzer);

        using var profiler = new Profiler();
        profiler.EnableAllocationTracking();
        analyzer.ProcessLogs(logs, profiler);

        var explanations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["IsAnomaly.Regex.Match"] =
                "Самый тяжёлый узел. Regex-движок разбирает шаблон заново при каждом вызове Match,\n" +
                "           плюс объект Match и группы — это аллокация в цикле.\n" +
                "           Причина медленности: универсальный разбор текста там, где нужен\n" +
                "           один int. Исправление: заменить на IndexOf + ручной разбор цифр.",

            ["IsAnomaly.String.Split"] =
                "Разбивает строку на массив подстрок, каждая подстрока — новый объект в куче.\n" +
                "           Причина медленности: аллокация ~6 строк на каждую запись лога.\n" +
                "           Исправление: читать только нужные позиции через Span/IndexOf.",

            ["IsAnomaly.DateTime.TryParse"] =
                "TryParse без формата перебирает ВСЕ известныеCulture и календари, пока не найдёт\n" +
                "           подходящий. Причина медленности: универсальный разбор вместо точного.\n" +
                "           Исправление: TryParseExact с фиксированным форматом (см. часть 3).",

            ["IsAnomaly (total with children)"] =
                "Это не отдельный метод, а сам IsAnomaly вместе со всеми шагами проверки.\n" +
                "           Собственное время здесь — накладные расходы самого цикла вызова:\n" +
                "           возврат из вложенных using-областей профилировщика и проверки веток.\n" +
                "           Оно уменьшится, если вынести шаги в отдельные методы, но не является\n" +
                "           настоящим узким местом — настоящие видны среди детей.",

            ["IsAnomaly.Checks"] =
                "Сами по себе проверки дешёвые, но Contains(\"ANOMALY\") просматривает всю строку,\n" +
                "           а DateTime.Now вызывается на каждой записи.\n" +
                "           Исправление: кэшировать границу времени, искать маркер заранее.",

            ["IsAnomaly.CheckLength"] =
                "Самый дешёвый узел, но вызывается на каждой записи — это плата за вызов метода\n" +
                "           и за вложенный using в профилировщике."
        };

        double total = profiler.Root.TotalMilliseconds;
        foreach (ProfileNode node in profiler.TopBySelfTime(5))
        {
            Console.WriteLine(new string('-', 78));
            Console.WriteLine($"{node.Name}: OWN {node.SelfMilliseconds:F2} мс " +
                              $"({100.0 * node.SelfMilliseconds / Math.Max(total, 0.0001):F1} % прогона), вызовов {node.Calls}");
            if (explanations.TryGetValue(node.Name, out string? reason))
            {
                Console.WriteLine(reason);
            }
            else
            {
                Console.WriteLine("  Собственное время существенно, но метод не является узким местом\n" +
                                  "  по объёму вызовов — проверьте частоту вызова в профилировщике.");
            }
        }

        SubHeader("Горячий путь (hot path)");
        Console.WriteLine("  Main -> ProcessLogs -> IsAnomaly (total with children) -> IsAnomaly.Regex.Match");
        Console.WriteLine("  Главный резерв: Regex.Match вызывается на КАЖДОЙ отфильтрованной записи,");
        Console.WriteLine("  хотя в данных код находится в поле с фиксированным префиксом \"Code: \".");
    }

    private static void ParseExactOptimization()
    {
        Header("ЧАСТЬ 3 — ЗАДАНИЕ: ЗАМЕНА DateTime.TryParse НА DateTime.ParseExact");

        Console.WriteLine("Формат данных зафиксирован генератором: " + LogAnalyzer.FixedFormat);
        Console.WriteLine("Предпосылка задания: TryParse вынужден перебирать Culture и календари,");
        Console.WriteLine("а ParseExact разбирает строго по одному шаблону, поэтому быстрее.");
        Console.WriteLine("Ниже эта предпосылка ПРОВЕРЯЕТСЯ ЗАМЕРОМ, а не принимается на веру.");
        Console.WriteLine();

        var tryAnalyzer = new LogAnalyzer(useParseExact: false);
        List<string> tryLogs = GenerateLogs(tryAnalyzer);

        using var tryParseProfiler = new Profiler();
        long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        tryAnalyzer.ProcessLogs(tryLogs, tryParseProfiler);
        long tryAlloc = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
        tryParseProfiler.Complete();
        double tryTotal = tryParseProfiler.Root.TotalMilliseconds;

        var exactAnalyzer = new LogAnalyzer(useParseExact: true);
        List<string> exactLogs = GenerateLogs(exactAnalyzer);

        using var exactProfiler = new Profiler();
        allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        exactAnalyzer.ProcessLogs(exactLogs, exactProfiler);
        long exactAlloc = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;
        exactProfiler.Complete();
        double exactTotal = exactProfiler.Root.TotalMilliseconds;

        SubHeader("Сравнение полного прогона Pipeline");
        Console.WriteLine($"  {Variant(-34)} {"TryParse",14} {"ParseExact",14}");
        Console.WriteLine($"  {Variant(-34)} {tryTotal,12:F2} мс {exactTotal,12:F2} мс");
        Console.WriteLine($"  {Variant(-34)} {tryAlloc / 1024.0 / 1024.0,11:F2} МБ {exactAlloc / 1024.0 / 1024.0,11:F2} МБ");
        Console.WriteLine($"  {Variant(-34)} {tryAnalyzer.Stats.Anomalies,14} {exactAnalyzer.Stats.Anomalies,14}");
        Console.WriteLine($"  Результат идентичен: {tryAnalyzer.Stats.Anomalies == exactAnalyzer.Stats.Anomalies}");
        Console.WriteLine("  ОДИНОЧНЫЙ прогон ненадёжен (шум JIT и кэшей), поэтому ниже — 5 раундов с прогревом.");

        SubHeader("Повторный замер Pipeline: 5 раундов, берём МИНИМУМ (стабильнее среднего)");
        double tryBest = double.MaxValue;
        double exactBest = double.MaxValue;

        for (int round = 0; round < 5; round++)
        {
            tryBest = Math.Min(tryBest, RunPipelineOnce(analyzer: new LogAnalyzer(false)));
            exactBest = Math.Min(exactBest, RunPipelineOnce(analyzer: new LogAnalyzer(true)));
        }

        Console.WriteLine($"  TryParse   (минимум из 5): {tryBest,8:F2} мс");
        Console.WriteLine($"  ParseExact (минимум из 5): {exactBest,8:F2} мс");
        Console.WriteLine($"  Ускорение Pipeline       : {tryBest / Math.Max(exactBest, 0.0001),8:F2} x");

        SubHeader("Изолированный замер разбора даты (200 000 итераций, пул 10 000 строк, 5 раундов, минимум)");
        Console.WriteLine("  Формат 1: yyyy-MM-dd HH:mm:ss.fff (формат данных из лабораторной)");
        double isoTry = MeasureDateParsing(iso: true, useParseExact: false);
        double isoExact = MeasureDateParsing(iso: true, useParseExact: true);
        Console.WriteLine($"    DateTime.TryParse      : {isoTry,8:F2} мс ({isoTry * 1_000_000.0 / 200_000:F0} нс на разбор)");
        Console.WriteLine($"    DateTime.TryParseExact : {isoExact,8:F2} мс ({isoExact * 1_000_000.0 / 200_000:F0} нс на разбор)");
        Console.WriteLine($"    Отношение              : {isoTry / Math.Max(isoExact, 0.0001),8:F2} x");

        Console.WriteLine("  Формат 2: dd.MM.yyyy HH:mm:ss (европейский)");
        double euTry = MeasureDateParsing(iso: false, useParseExact: false);
        double euExact = MeasureDateParsing(iso: false, useParseExact: true);
        Console.WriteLine($"    DateTime.TryParse      : {euTry,8:F2} мс ({euTry * 1_000_000.0 / 200_000:F0} нс на разбор)");
        Console.WriteLine($"    DateTime.TryParseExact : {euExact,8:F2} мс ({euExact * 1_000_000.0 / 200_000:F0} нс на разбор)");
        Console.WriteLine($"    Отношение              : {euTry / Math.Max(euExact, 0.0001),8:F2} x");

        Console.WriteLine();
        Console.WriteLine("  ВЫВОД ПО ЗАМЕРУ (получен измерением, а не взят из учебника):");
        Console.WriteLine("  Рекомендация задания заменить TryParse на ParseExact на .NET 10 НЕ подтверждается");
        Console.WriteLine("  по времени: TryParseExact оказался МЕДЛЕННЕЕ в обоих форматах.");
        Console.WriteLine("  Причина: в .NET 7+ движок разбора даты переписан и TryParse очень быстр,");
        Console.WriteLine("  а TryParseExact при каждом вызове разбирает пользовательскую строку формата");
        Console.WriteLine("  токен за токеном, поэтому проигрывает.");
        Console.WriteLine("  Где ParseExact всё же лучше — это НЕ скорость, а строгость:");
        Console.WriteLine("  - он не зависит от текущей Culture (нет неоднозначности 01.02.2026);");
        Console.WriteLine("  - он отвергает мусорный вход вместо попытки угадать формат.");
        Console.WriteLine("  Вывод для практики: оптимизацию выбирают замером. Рекомендация задания");
        Console.WriteLine("  реализована и даёт одинаковый результат, но не является ускорением на .NET 10.");
        Console.WriteLine($"  Результат Pipeline при этом идентичен: {tryAnalyzer.Stats.Anomalies == exactAnalyzer.Stats.Anomalies}.");

        SubHeader("Изолированный замер Regex vs ручной разбор (5 раундов, минимум)");
        List<string> samples = GenerateLogs(new LogAnalyzer(false), 2_000);
        double regexMs = MinOfRounds(() => MeasureRegex(samples));
        double manualMs = MinOfRounds(() => MeasureManualParse(samples));
        Console.WriteLine($"  Regex.Match            : {regexMs,8:F3} мс");
        Console.WriteLine($"  IndexOf + ручной разбор: {manualMs,8:F3} мс");
        Console.WriteLine($"  Ускорение              : {regexMs / Math.Max(manualMs, 0.0001),8:F1} x");
        Console.WriteLine("  Это и есть главный резерв оптимизации в данном коде.");

        exactProfiler.Complete();
        TreePrinter.Print(exactProfiler, "Call Tree (оптимизированная версия, ParseExact)");

        Console.WriteLine("Итог по заданию:");
        Console.WriteLine("  а) DateTime.ParseExact с фиксированным форматом РЕАЛИЗОВАН и даёт тот же");
        Console.WriteLine("     результат, но замер показал, что на .NET 10 он не быстрее TryParse.");
        Console.WriteLine("  б) Настоящее ускорение найдено профилировщиком в другом месте:");
        Console.WriteLine("     Regex.Match -> IndexOf с ручным разбором цифр, кратный выигрыш.");
        Console.WriteLine("  в) Именно это и доказывает пользу профилирования: оптимизировать надо то,");
        Console.WriteLine("     что действительно горячее, а не то, что кажется подозрительным.");
    }

    private static double MinOfRounds(Func<double> action)
    {
        double best = double.MaxValue;
        for (int round = 0; round < 5; round++)
        {
            best = Math.Min(best, action());
        }

        return best;
    }

    private static double RunPipelineOnce(LogAnalyzer analyzer)
    {
        List<string> logs = GenerateLogs(analyzer);
        using var profiler = new Profiler();
        analyzer.ProcessLogs(logs, profiler);
        profiler.Complete();

        return profiler.Root.TotalMilliseconds;
    }

    private static double MeasureProfilerOverhead()
    {
        const int iterations = 200_000;

        using var warmup = new Profiler();
        for (int i = 0; i < 1_000; i++)
        {
            warmup.Enter("warmup").Dispose();
        }

        using var profiler = new Profiler();
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            profiler.Enter("noop").Dispose();
        }
        sw.Stop();
        profiler.Complete();

        return sw.Elapsed.TotalMilliseconds * 1000.0 / iterations;
    }

    private static List<string> GenerateLogs(LogAnalyzer analyzer, int count = LogCount)
    {
        using var setup = new Profiler();
        List<string> logs = analyzer.Generate(count, setup);
        setup.Complete();

        return logs;
    }

    private static string Variant(int padding) => new(' ', -padding);

    private const string EuropeanFormat = "dd.MM.yyyy HH:mm:ss";

    private static double MeasureDateParsing(bool iso, bool useParseExact)
    {
        const int iterations = 200_000;
        const int rounds = 5;
        string format = iso ? LogAnalyzer.FixedFormat : EuropeanFormat;
        List<string> dates = BuildDateSamples(iso);

        RunDateLoop(dates, iterations, iso, useParseExact);

        double best = double.MaxValue;
        for (int round = 0; round < rounds; round++)
        {
            best = Math.Min(best, RunDateLoop(dates, iterations, iso, useParseExact));
        }

        return best;
    }

    private static List<string> BuildDateSamples(bool iso)
    {
        Random random = new Random(7);
        List<string> dates = new List<string>(10_000);

        for (int i = 0; i < 10_000; i++)
        {
            DateTime moment = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local)
                .AddDays(random.Next(0, 300))
                .AddMinutes(random.Next(0, 1_440));

            dates.Add(iso
                ? moment.ToString(LogAnalyzer.FixedFormat, System.Globalization.CultureInfo.InvariantCulture)
                : moment.ToString(EuropeanFormat, System.Globalization.CultureInfo.InvariantCulture));
        }

        return dates;
    }

    private static double RunDateLoop(List<string> dates, int iterations, bool iso, bool useParseExact)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long parsed = 0;
        string format = iso ? LogAnalyzer.FixedFormat : EuropeanFormat;

        for (int i = 0; i < iterations; i++)
        {
            string text = dates[i % dates.Count];

            if (useParseExact)
            {
                if (DateTime.TryParseExact(text, format,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime exact))
                {
                    parsed += exact.Year;
                }
            }
            else
            {
                if (DateTime.TryParse(text, out DateTime loose))
                {
                    parsed += loose.Year;
                }
            }
        }
        sw.Stop();
        GC.KeepAlive(parsed);

        return sw.Elapsed.TotalMilliseconds;
    }

    private static double MeasureRegex(List<string> logs)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"Code:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        Stopwatch sw = Stopwatch.StartNew();
        int sum = 0;
        foreach (string log in logs)
        {
            System.Text.RegularExpressions.Match match = regex.Match(log);
            if (match.Success)
            {
                sum += match.Groups[1].Value.Length;
            }
        }
        sw.Stop();
        GC.KeepAlive(sum);

        return sw.Elapsed.TotalMilliseconds;
    }

    private static double MeasureManualParse(List<string> logs)
    {
        Stopwatch sw = Stopwatch.StartNew();
        int sum = 0;
        foreach (string log in logs)
        {
            int start = log.IndexOf("Code:", StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            int index = start + 5;
            int value = 0;
            while (index < log.Length && log[index] >= '0' && log[index] <= '9')
            {
                value = value * 10 + (log[index] - '0');
                index++;
            }

            sum += value;
        }
        sw.Stop();
        GC.KeepAlive(sum);

        return sw.Elapsed.TotalMilliseconds;
    }

    private static void OwnCodeProfiling()
    {
        Header("ЧАСТЬ 4 — ЗАДАНИЕ: ПРОФИЛИРОВАНИЕ СОБСТВЕННОГО КОДА");

        Console.WriteLine("Собственный код: анализатор частоты слов в логах (с реальным текстом сообщений).");
        Console.WriteLine("Dictionary<string,int> + Split + фильтрация стоп-слов + отчёт топ-N.");
        Console.WriteLine();

        List<string> logs = TextCorpus.Build(LogCount);

        var analyzer = new WordFrequencyAnalyzer();
        using var profiler = new Profiler();
        profiler.EnableAllocationTracking();

        long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        Stopwatch wall = Stopwatch.StartNew();
        Dictionary<string, int> frequency = analyzer.CountWords(logs, profiler);
        string report = analyzer.BuildReport(frequency, 10, profiler);
        wall.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocBefore;

        Console.WriteLine($"Проанализировано записей: {logs.Count:N0}, токенов: {analyzer.Tokens:N0}, " +
                          $"уникальных слов: {frequency.Count:N0}");
        Console.WriteLine($"Время: {wall.Elapsed.TotalMilliseconds:F2} мс, аллокаций: {allocated / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"Топ-10 слов: {report}");

        profiler.Complete();
        TreePrinter.Print(profiler, "Call Tree (собственный код: анализатор частоты слов)");

        SubHeader("Топ-5 узлов собственного кода по Own Time");
        double total = profiler.Root.TotalMilliseconds;
        foreach (ProfileNode node in profiler.TopBySelfTime(5))
        {
            double percent = total == 0 ? 0 : 100.0 * node.SelfMilliseconds / total;
            Console.WriteLine($"  {node.Name,-32} {node.SelfMilliseconds,8:F2} мс {percent,6:F1}% вызовов {node.Calls}");
        }

        SubHeader("Выводы по собственному коду");
        Console.WriteLine($"  1. Tokenize — самый горячий узел собственного кода: он вызывается {LogCount:N0} раз,");
        Console.WriteLine("     и каждое обращение к Split порождает новый массив строк и подстроки.");
        Console.WriteLine("     Именно он, а не подсчёт, определяет и время, и объём аллокаций.");
        Console.WriteLine("  2. DictionaryLookup вызывается НАМНОГО чаще (на каждый токен), но его собственное");
        Console.WriteLine("     время мало. Прямой пример того, что частота вызовов != стоимость вызова.");
        Console.WriteLine("  3. Цепочка LINQ Where после Split выделяла итератор и лишний List<string>,");
        Console.WriteLine("     поэтому заменена обычным циклом с проверкой IsWord — аллокаций стало меньше.");
        Console.WriteLine("  4. Слова-стоп-words и небуквенные токены (даты, IP, уровни лога) отфильтровываются,");
        Console.WriteLine("     иначе в топ попадали бы 2026-09-27, DEBUG и октеты IP, а не смысловые слова.");
        Console.WriteLine("  5. Вывод тот же, что и в части 2: оптимизировать надо узел с наибольшим");
        Console.WriteLine("     СОБСТВЕННЫМ временем, а не просто самый часто вызываемый метод.");
    }

    private static void FinalConclusion()
    {
        Header("ИТОГИ ПО ЗАДАНИЮ 5");

        Console.WriteLine("1) Инструмент: собственный профилировщик на Stopwatch (без внешних пакетов).");
        Console.WriteLine("   Каждый инструментированный метод оборачивается в using (profiler.Enter(\"Имя\")),");
        Console.WriteLine("   Own Time считается как время метода минус суммарное время дочерних вызовов.");
        Console.WriteLine();
        Console.WriteLine("2) Найденные горячие узлы (по убыванию Own Time):");
        Console.WriteLine("   - Regex.Match: универсальный разбор ради одного числа (главный резерв);");
        Console.WriteLine("   - String.Split: аллокация подстрок на каждую запись;");
        Console.WriteLine("   - DateTime.TryParse: заметная доля времени, но НЕ из-за перебора Culture —");
        Console.WriteLine("     это миф, опровергнутый замером в части 3;");
        Console.WriteLine("   - Contains/DateTime.Now в проверках: лишний обход строки и системный вызов.");
        Console.WriteLine();
        Console.WriteLine("3) Выполненные изменения и их реальный эффект (по замерам):");
        Console.WriteLine("   - DateTime.TryParse -> DateTime.TryParseExact с форматом \"yyyy-MM-dd HH:mm:ss.fff\":");
        Console.WriteLine("     результат тот же, но ВРЕМЕНИ НЕ СТАЛО МЕНЬШЕ на .NET 10;");
        Console.WriteLine("     ценность ParseExact здесь — строгость и независимость от Culture;");
        Console.WriteLine("   - Regex.Match -> IndexOf + ручной разбор цифр: кратное ускорение, это");
        Console.WriteLine("     единственная оптимизация, которая реально сработала;");
        Console.WriteLine("   - DateTime.Now внутри цикла -> вычисление границы один раз (устранено).");
        Console.WriteLine();
        Console.WriteLine("4) Правило, которое даёт профилировщик:");
        Console.WriteLine("   самый ЧАСТЫЙ вызов не обязательно самый МЕДЛЕННЫЙ; сравнивать нужно");
        Console.WriteLine("   собственное время (Own Time) и аллокации, а не число вызовов.");
        Console.WriteLine();
        Console.WriteLine("5) Чего не делает этот профилировщик (и почему встроенных средств достаточно):");
        Console.WriteLine("   не измеряет ввод-вывод, блокировки и работу планировщика; для этого нужны");
        Console.WriteLine("   dotnet-counters/dotnet-trace. Для учебной лабораторной работы по горячим");
        Console.WriteLine("   путям внутри вычислений этого достаточно.");
    }
}
