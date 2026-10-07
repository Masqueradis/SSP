using System.Diagnostics;

namespace Lab5Task14;

sealed record Employee(int Id, string Name, string Department, decimal Salary, int Age, DateTime HireDate);

static class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ЗАДАНИЕ 14. PARALLEL LINQ (PLINQ) ===");
        Console.WriteLine($"Среда: {Environment.ProcessorCount} логических ядер, .NET {Environment.Version}\n");

        List<Employee> staff = Generate(20_000);

        Basics();
        FilteringAndProjection(staff);
        GroupingAndAggregation(staff);
        Ordering(staff);
        PlinqCancellation(staff);
        QueryVsMethod(staff);
        ExecutionOptions(staff);

        Console.WriteLine("=== ЗАДАНИЕ 14 ВЫПОЛНЕНО ===");
        Pause();
    }

    static List<Employee> Generate(int n)
    {
        string[] departments = { "Разработка", "Продажи", "Бухгалтерия", "Поддержка", "HR" };
        string[] names = { "Иван", "Пётр", "Сидор", "Анна", "Мария", "Олег", "Ирина", "Павел" };
        string[] surnames = { "Иванов", "Петров", "Сидоров", "Кузнецова", "Попова", "Смирнов", "Соколова", "Морозов" };
        Random rnd = new(2024);

        List<Employee> list = new(n);
        for (int i = 0; i < n; i++)
        {
            list.Add(new Employee(
                i + 1,
                $"{surnames[rnd.Next(surnames.Length)]} {names[rnd.Next(names.Length)]}",
                departments[rnd.Next(departments.Length)],
                Math.Round((decimal)(40_000 + rnd.Next(90_000)), 2),
                20 + rnd.Next(40),
                new DateTime(2005, 1, 1).AddDays(rnd.Next(7500))));
        }

        return list;
    }

    static void Basics()
    {
        Console.WriteLine("--- 14.1. AsParallel: превращение обычной коллекции в параллельную ---");
        List<int> data = Enumerable.Range(0, 5_000_000).ToList();

        var sw = Stopwatch.StartNew();
        long seqSum = data.Sum(x => (long)x);
        sw.Stop();
        double seqMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        long parSum = data.AsParallel().Sum(x => (long)x);
        sw.Stop();
        double parMs = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"  Обычный LINQ (Sum):    {seqMs,7:F1} мс, результат {seqSum}");
        Console.WriteLine($"  PLINQ (AsParallel.Sum): {parMs,7:F1} мс, результат {parSum}");
        Console.WriteLine($"  Ускорение: {seqMs / parMs:F2}x, результаты совпали: {seqSum == parSum}");
        Console.WriteLine("  Простые агрегации вроде Sum или Count в PLINQ обычно не ускоряются:");
        Console.WriteLine("  это одна операция, не проходящая через все элементы конвейера.");
        Console.WriteLine("  Реальная польза появляется, когда запрос содержит несколько операций.");
        Console.WriteLine();
    }

    static void FilteringAndProjection(List<Employee> staff)
    {
        Console.WriteLine("--- 14.2. Фильтрация и проекция ---");

        var sw = Stopwatch.StartNew();
        List<Employee> seq = staff
            .Where(e => e.Department == "Разработка" && e.Salary > 80_000)
            .OrderBy(e => e.Salary)
            .ToList();
        sw.Stop();
        double seqMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        List<Employee> par = staff
            .AsParallel()
            .Where(e => e.Department == "Разработка" && e.Salary > 80_000)
            .OrderBy(e => e.Salary)
            .ToList();
        sw.Stop();
        double parMs = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"  Разработчики с зарплатой выше 80 000: {seq.Count} человек");
        Console.WriteLine($"  Обычный LINQ: {seqMs:F1} мс, PLINQ: {parMs:F1} мс, ускорение {seqMs / parMs:F2}x");
        Console.WriteLine($"  Первые три по зарплате: {string.Join(", ", par.Take(3).Select(e => $"{e.Name} ({e.Salary})"))}");
        Console.WriteLine();
        if (parMs > seqMs)
        {
            Console.WriteLine("  PLINQ здесь проиграл: затраты на разбиение и слияние не окупились, потому что");
            Console.WriteLine("  запрос короткий, а OrderBy — операция сведения, которая сшивает результат в одно место.");
        }
        else
        {
            Console.WriteLine("  PLINQ ускорил запрос: фильтр и проекция отсеивают большую часть данных до сортировки.");
        }

        Console.WriteLine();

        var names = staff
            .AsParallel()
            .Where(e => e.Age > 45)
            .Select(e => e.Name.ToUpperInvariant())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        Console.WriteLine($"  Уникальные ФИО сотрудников старше 45 лет: {names.Count}");
        Console.WriteLine($"  Пример: {string.Join(", ", names.Take(4))}...\n");
    }

    static void GroupingAndAggregation(List<Employee> staff)
    {
        Console.WriteLine("--- 14.3. Группировка и агрегация ---");

        var sw = Stopwatch.StartNew();
        var groups = staff
            .AsParallel()
            .GroupBy(e => e.Department)
            .Select(g => new
            {
                Department = g.Key,
                Count = g.Count(),
                AvgSalary = g.Average(e => e.Salary),
                MaxSalary = g.Max(e => e.Salary),
                Total = g.Sum(e => e.Salary)
            })
            .OrderByDescending(x => x.AvgSalary)
            .ToList();
        sw.Stop();

        Console.WriteLine($"  {"Отдел",-16}{"Сотрудников",13}{"Средняя з/п",14}{"Максимум",14}");
        foreach (var g in groups)
        {
            Console.WriteLine($"  {g.Department,-16}{g.Count,13}{g.AvgSalary,14:N0}{g.MaxSalary,14:N0}");
        }

        Console.WriteLine($"  Время PLINQ GroupBy: {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"  Суммарный ФОТ: {groups.Sum(g => g.Total):N0}");
        Console.WriteLine("  GroupBy в PLINQ сначала сводит данные по ключу, поэтому группировку лучше делать");
        Console.WriteLine("  до проекции: чем меньше данных попадает в каждую группу, тем дешевле агрегация.\n");
    }

    static void Ordering(List<Employee> staff)
    {
        Console.WriteLine("--- 14.4. Порядок выполнения операций в PLINQ ---");
        Console.WriteLine("  Порядок в PLINQ не гарантирован, но последовательность операций влияет на скорость:\n");

        long filterFirst = Measure(() => staff
            .AsParallel()
            .Where(e => e.Age > 40)
            .Select(e => ExpensiveProjection(e))
            .Count());

        long projectFirst = Measure(() => staff
            .AsParallel()
            .Select(e => ExpensiveProjection(e))
            .Where(id => id > 0)
            .Count());

        Console.WriteLine($"  Дешёвый Where(возраст) до дорогой проекции: {filterFirst,6} мс");
        Console.WriteLine($"  Дорогая проекция до Where:                 {projectFirst,6} мс");
        Console.WriteLine("  Смысл: сначала отсекаем дешёвым предикатом, дальше работаем с меньшим объёмом.");
        Console.WriteLine("  Порядок операций в конвейере сохраняется, и переставить их нельзя.");
        string note = projectFirst >= filterFirst ? "ничего не отсекает" : "отсекает";
        Console.WriteLine($"  Во втором варианте выигрыша нет: условие {note}, поэтому дорогая проекция выполняется для всех строк.\n");

        var unordered = staff.AsParallel().Where(e => e.Salary > 95_000).Take(5).ToList();
        Console.WriteLine($"  Take(5) без OrderBy вернул отделы: {string.Join(", ", unordered.Select(e => e.Department))}");
        Console.WriteLine("  Это нормально для PLINQ: без явной сортировки порядок не определён.");
        Console.WriteLine("  Если порядок важен, нужен OrderBy до Take, но это всегда снижает параллелизм.\n");
    }

    static long ExpensiveProjection(Employee e)
    {
        long acc = e.Id;
        for (int i = 0; i < 2000; i++)
        {
            acc = (acc * 31 + i) % 100_000_007;
        }

        return acc;
    }

    static long Measure(Func<long> run)
    {
        _ = run();
        var sw = Stopwatch.StartNew();
        long result = run();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    static void PlinqCancellation(List<Employee> staff)
    {
        Console.WriteLine("--- 14.5. Отмена запроса PLINQ ---");
        using CancellationTokenSource cts = new();
        int count = 0;
        var sw = Stopwatch.StartNew();

        Thread canceller = new(() =>
        {
            Thread.Sleep(40);
            cts.Cancel();
            Console.WriteLine($"  [{sw.ElapsedMilliseconds,5} мс] Запрос отменён");
        });

        canceller.Start();
        try
        {
            List<Employee> result = staff
                .AsParallel()
                .WithCancellation(cts.Token)
                .Where(e =>
                {
                    Thread.Sleep(1);
                    return e.Salary > 70_000;
                })
                .ToList();
            Console.WriteLine($"  Запрос завершился, найдено {result.Count}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("  Поймано OperationCanceledException: запрос прерван.");
        }

        sw.Stop();
        canceller.Join();
        Console.WriteLine($"  Запрос отменён через {sw.ElapsedMilliseconds} мс вместо полного обхода.");
        Console.WriteLine("  WithCancellation передаёт токен в конвейер, он проверяется между элементами.");
        Console.WriteLine("  Без WithCancellation токен игнорируется, и отмена не подействует.\n");
        _ = count;
    }

    static void QueryVsMethod(List<Employee> staff)
    {
        Console.WriteLine("--- 14.6. PLINQ против метода Parallel.ForEach ---");
        Console.WriteLine("  Задача: оставить сотрудников с зарплатой выше 80 000 и посчитать сумму.\n");

        var sw = Stopwatch.StartNew();
        long methodSumCents = 0;
        var kept = new List<Employee>();
        Parallel.ForEach(
            staff,
            () => 0L,
            (e, _, local) => e.Salary > 80_000 ? local + (long)(e.Salary * 100) : local,
            local => Interlocked.Add(ref methodSumCents, local));

        Parallel.ForEach(staff, e =>
        {
            if (e.Salary > 80_000)
            {
                lock (kept)
                {
                    kept.Add(e);
                }
            }
        });
        sw.Stop();
        double methodMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        decimal plinqSum = staff.AsParallel().Where(e => e.Salary > 80_000).Sum(e => e.Salary);
        var plinqKept = staff.AsParallel().Where(e => e.Salary > 80_000).ToList();
        sw.Stop();
        double plinqMs = sw.Elapsed.TotalMilliseconds;
        decimal methodSum = methodSumCents / 100m;

        Console.WriteLine($"  Parallel.ForEach: {methodMs,7:F1} мс, сумма {methodSum:N0}, отобрано {kept.Count}");
        Console.WriteLine($"  PLINQ:            {plinqMs,7:F1} мс, сумма {plinqSum:N0}, отобрано {plinqKept.Count}");
        Console.WriteLine($"  Совпадение сумм: {methodSum == plinqSum}");
        if (methodMs < plinqMs)
        {
            Console.WriteLine($"  Выиграл Parallel.ForEach ({methodMs:F1} против {plinqMs:F1} мс): у него нет");
            Console.WriteLine("  затрат на построение конвейера, есть локальное состояние диапазона.");
        }
        else
        {
            Console.WriteLine($"  Выиграл PLINQ ({plinqMs:F1} против {methodMs:F1} мс): конвейер лучше кэширует данные.");
        }

        Console.WriteLine("  Выбор по ситуации: PLINQ короче и читается как обычный LINQ, Parallel.ForEach даёт");
        Console.WriteLine("  контроль над разбиением, состоянием диапазона и отменой.\n");
    }

    static void ExecutionOptions(List<Employee> staff)
    {
        Console.WriteLine("--- 14.7. Настройка параллелизма запроса ---");

        var sw = Stopwatch.StartNew();
        int all = staff.AsParallel().WithDegreeOfParallelism(2).Count(e => e.Salary > 90_000);
        sw.Stop();
        double twoMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int all2 = staff.AsParallel().Count(e => e.Salary > 90_000);
        sw.Stop();
        double autoMs = sw.ElapsedMilliseconds;

        ThreadPool.GetMaxThreads(out int worker, out int io);
        Console.WriteLine($"  WithDegreeOfParallelism(2): {twoMs} мс, найдено {all}");
        Console.WriteLine($"  Без ограничения параллелизма: {autoMs} мс, найдено {all2}");
        Console.WriteLine($"  Доступно процессоров: {Environment.ProcessorCount}, максимум потоков пула: {worker}, для ввода-вывода: {io}");
        Console.WriteLine("  PLINQ по умолчанию не использует все потоки пула: конвейер разбивает данные");
        Console.WriteLine("  на блоки и планирует их независимо, иначе порядок результатов был бы неопределенным.");
        Console.WriteLine("  Для чистой нагрузки на CPU выгоднее обычный Parallel.ForEach.");
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
