using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lab4Task3;

public class UnoptimizedCode
{
    private readonly ArrayList _data = new();

    public int Count => _data.Count;

    public void Add(int value) => _data.Add(value);
}

public class OptimizedCode
{
    private readonly List<int> _data = new();

    public int Count => _data.Count;

    public void Add(int value) => _data.Add(value);
}

static class Benchmark
{
    public const int Iterations = 1_000_000;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Measurement Measure(Action<int> action, int iterations)
    {
        action(0);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long collectionsBefore = GC.CollectionCount(0);

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action(i);
        }
        sw.Stop();

        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long gen0Collections = GC.CollectionCount(0) - collectionsBefore;
        GC.KeepAlive(action);

        return new Measurement(sw.Elapsed.TotalMilliseconds, allocated, gen0Collections);
    }

    public static void Report(string name, Measurement m)
    {
        Console.WriteLine($"  {name,-46} {m.Milliseconds,9:F2} мс  {m.AllocatedBytes / 1024.0 / 1024.0,9:F2} МБ  сборок Gen0: {m.Gen0Collections}");
    }
}

readonly record struct Measurement(double Milliseconds, long AllocatedBytes, long Gen0Collections)
{
    public double Megabytes => AllocatedBytes / 1024.0 / 1024.0;
}

static class Program
{
    private const int N = Benchmark.Iterations;

    private static int _intSink;
    private static object? _objectSink;

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
        Console.WriteLine("ЛАБОРАТОРНАЯ РАБОТА 4 — ЗАДАНИЕ 3: ОПТИМИЗАЦИЯ BOXING/UNBOXING");
        Console.WriteLine($"Среда: {Environment.Version}  |  Число элементов: {N:N0}");
        Console.WriteLine($"Серверный GC: {System.Runtime.GCSettings.IsServerGC}  |  Процессоров: {Environment.ProcessorCount}");

        ListObjectVsListInt();
        ArrayListVsListOfInt();
        ImplicitBoxing();
        Conclusion();

        Console.WriteLine();
        Console.WriteLine("Нажмите любую клавишу...");
        try { Console.ReadKey(); } catch { }
    }

    private static void ListObjectVsListInt()
    {
        Header("ЗАДАНИЕ 3.1 — СРАВНЕНИЕ ПРОИЗВОДИТЕЛЬНОСТИ");

        SubHeader("С упаковкой: List<object>.Add(int) -> boxing на каждом вызове");
        var boxed = new List<object>();
        Benchmark.Report("List<object> (упаковка)", Benchmark.Measure(i => boxed.Add(i), N));
        Measurement boxedResult = Benchmark.Measure(i => boxed.Add(i), N);
        Benchmark.Report("List<object> (упаковка), прогрев", boxedResult);

        SubHeader("Без упаковки: List<int>.Add(int) -> значение лежит в массиве");
        var unboxed = new List<int>();
        Measurement unboxedResult = Benchmark.Measure(unboxed.Add, N);
        Benchmark.Report("List<int> (без упаковки)", unboxedResult);

        SubHeader("Итог");
        Console.WriteLine($"  Элементов: {N:N0}");
        Console.WriteLine($"  List<object>: {boxedResult.Megabytes:F2} МБ аллокаций, {boxed.Count:N0} элементов");
        Console.WriteLine($"  List<int>:    {unboxedResult.Megabytes:F2} МБ аллокаций, {unboxed.Count:N0} элементов");
        Console.WriteLine($"  Разница по аллокациям: {boxedResult.Megabytes / Math.Max(unboxedResult.Megabytes, 0.0001):F1} x");
        Console.WriteLine($"  Упаковка одного int создаёт объект на 24 байта: {N} * 24 = {N * 24 / 1024.0 / 1024.0:F2} МБ");
        Console.WriteLine($"  Массив List<int> с данными занимает ~{(N * 4 + 24) / 1024.0 / 1024.0:F2} МБ, " +
                          $"разница {(boxedResult.AllocatedBytes - unboxedResult.AllocatedBytes) / 1024.0 / 1024.0:F2} МБ — это и есть мусор от упаковки");
        Console.WriteLine($"  Сборок Gen0: упаковка {boxedResult.Gen0Collections}, без упаковки {unboxedResult.Gen0Collections}");
    }

    private static void ArrayListVsListOfInt()
    {
        Header("ЗАДАНИЕ 3.2 — ОПТИМИЗАЦИЯ СУЩЕСТВУЮЩЕГО КОДА");

        Console.WriteLine("  НЕОПТИМИЗИРОВАНО: UnoptimizedCode хранит ArrayList,");
        Console.WriteLine("  поэтому Add(int) упаковывает каждое значение.");
        var unoptimized = new UnoptimizedCode();
        Measurement unoptimizedResult = Benchmark.Measure(unoptimized.Add, N);
        Benchmark.Report("UnoptimizedCode.Add -> ArrayList", unoptimizedResult);

        Console.WriteLine();
        Console.WriteLine("  ОПТИМИЗИРОВАНО: OptimizedCode хранит List<int>,");
        Console.WriteLine("  значения хранятся в типизированном массиве без упаковки.");
        var optimized = new OptimizedCode();
        Measurement optimizedResult = Benchmark.Measure(optimized.Add, N);
        Benchmark.Report("OptimizedCode.Add -> List<int>", optimizedResult);

        SubHeader("Сравнение чтения: 1 000 000 элементов");
        var arrayList = new ArrayList();
        var listInt = new List<int>();
        for (int i = 0; i < 1_000_000; i++)
        {
            arrayList.Add(i);
            listInt.Add(i);
        }

        Stopwatch swBoxedRead = Stopwatch.StartNew();
        long sumBoxed = 0;
        for (int repeat = 0; repeat < 10; repeat++)
        {
            for (int i = 0; i < arrayList.Count; i++)
            {
                sumBoxed += (int)arrayList[i]!;
            }
        }
        swBoxedRead.Stop();

        Stopwatch swIntRead = Stopwatch.StartNew();
        long sumUnboxed = 0;
        for (int repeat = 0; repeat < 10; repeat++)
        {
            for (int i = 0; i < listInt.Count; i++)
            {
                sumUnboxed += listInt[i];
            }
        }
        swIntRead.Stop();

        Console.WriteLine($"  Чтение из ArrayList (с unboxing): {swBoxedRead.Elapsed.TotalMilliseconds:F2} мс, сумма {sumBoxed}");
        Console.WriteLine($"  Чтение из List<int>  (без него):  {swIntRead.Elapsed.TotalMilliseconds:F2} мс, сумма {sumUnboxed}");
        Console.WriteLine($"  Ускорение: {swBoxedRead.Elapsed.TotalMilliseconds / swIntRead.Elapsed.TotalMilliseconds:F2} x");
        Console.WriteLine("  Важно: сам unboxing НЕ выделяет память — он лишь копирует значение и проверяет тип.");
        Console.WriteLine("  Разница в скорости — это накладные расходы приведения типа и запрет JIT-оптимизаций.");

        SubHeader("А вот здесь упаковка происходит неявно и незаметно");
        IEnumerable asNonGeneric = listInt;
        long before = GC.GetTotalAllocatedBytes(precise: true);
        long sumViaInterface = 0;
        foreach (object o in asNonGeneric)
        {
            sumViaInterface += (int)o;
        }
        long interfaceBytes = GC.GetTotalAllocatedBytes(precise: true) - before;

        before = GC.GetTotalAllocatedBytes(precise: true);
        long sumDirect = 0;
        foreach (int i in listInt)
        {
            sumDirect += i;
        }
        long directBytes = GC.GetTotalAllocatedBytes(precise: true) - before;

        Console.WriteLine($"  foreach по IEnumerable (объектная версия): аллокаций {interfaceBytes} байт, сумма {sumViaInterface}");
        Console.WriteLine($"  foreach напрямую по List<int>:            аллокаций {directBytes} байт, сумма {sumDirect}");
        Console.WriteLine("  Причина: IEnumerable — объектный интерфейс, поэтому перечислитель упаковывается,");
        Console.WriteLine("  а int внутри foreach(object) приходится приводить обратно.");

        SubHeader("Результат оптимизации");
        Console.WriteLine($"  ArrayList: {unoptimizedResult.Megabytes:F2} МБ   List<int>: {optimizedResult.Megabytes:F2} МБ");
        Console.WriteLine($"  Экономия аллокаций: {unoptimizedResult.Megabytes / Math.Max(optimizedResult.Megabytes, 0.0001):F1} x");
        Console.WriteLine($"  Ускорение по времени: {unoptimizedResult.Milliseconds / Math.Max(optimizedResult.Milliseconds, 0.0001):F2} x");
        GC.KeepAlive(unoptimized);
        GC.KeepAlive(optimized);
    }

    private static void ImplicitBoxing()
    {
        Header("ДОПОЛНИТЕЛЬНО — UNBOXING НЕ АЛЛОЦИРУЕТ, А UBOXING АЛЛОЦИРУЕТ");

        var numbers = new List<int> { 1, 2, 3, 4, 5 };
        var boxed = new List<object> { 1, 2, 3, 4, 5 };

        Console.WriteLine("  Список List<int>: foreach НЕ вызывает ни boxing, ни unboxing");
        long before = GC.GetTotalAllocatedBytes(precise: true);
        long sum1 = 0;
        foreach (int n in numbers)
        {
            sum1 += n;
        }
        long listAlloc = GC.GetTotalAllocatedBytes(precise: true) - before;

        Console.WriteLine("  Список List<object>: foreach выполняет UNBOXING (объект -> int), память не выделяется");
        before = GC.GetTotalAllocatedBytes(precise: true);
        long sum2 = 0;
        foreach (object n in boxed)
        {
            sum2 += (int)n;
        }
        long objectAlloc = GC.GetTotalAllocatedBytes(precise: true) - before;

        Console.WriteLine($"  int:    сумма {sum1}, аллокаций {listAlloc} байт");
        Console.WriteLine($"  object: сумма {sum2}, аллокаций {objectAlloc} байт");
        Console.WriteLine("  => UNBOXING не выделяет память: он лишь извлекает значение из уже существующего объекта.");

        Console.WriteLine();
        Console.WriteLine("  А вот неявная УПАКОВКА при присваивании int в object аллоцирует всегда:");
        before = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < 10_000; i++)
        {
            object temp = i;
            _objectSink = temp;
        }
        long implicitBoxingBytes = GC.GetTotalAllocatedBytes(precise: true) - before;
        Console.WriteLine($"  10 000 неявных упаковок int -> object: {implicitBoxingBytes} байт " +
                          $"({implicitBoxingBytes / 10_000.0:F0} байт на упаковку)");

        before = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < 10_000; i++)
        {
            int temp = i;
            _intSink += temp;
        }
        long noBoxingBytes = GC.GetTotalAllocatedBytes(precise: true) - before;
        Console.WriteLine($"  10 000 присваиваний int -> int:      {noBoxingBytes} байт (упаковки нет)");

        Header("ЛОВУШКА, НАЙДЕННАЯ ПРИ ЗАМЕРЕ: GC.KeepAlive УПАКОВЫВАЕТ int");
        Console.WriteLine("  GC.KeepAlive имеет сигнатуру KeepAlive(object), поэтому вызов");
        Console.WriteLine("  GC.KeepAlive(temp) с переменной типа int САМ создаёт объект 24 байта.");
        Console.WriteLine("  Первая версия этого замера показала одинаковые 240 000 байт в обоих циклах.");
        Console.WriteLine();

        before = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < 10_000; i++)
        {
            int temp = i;
            GC.KeepAlive(temp);
        }
        long keepAliveBytes = GC.GetTotalAllocatedBytes(precise: true) - before;
        Console.WriteLine($"  Цикл int -> int с GC.KeepAlive(temp):     {keepAliveBytes} байт " +
                          $"({keepAliveBytes / 10_000.0:F0} байт на итерацию) <- ловушка");
        Console.WriteLine($"  Цикл int -> int с накопителем _intSink:  {noBoxingBytes} байт (упаковки нет)");
        Console.WriteLine("  Вывод: чтобы измерить отсутствие упаковки, значение нужно сохранять");
        Console.WriteLine("  в типизированную переменную, а не передавать в API, принимающий object.");
    }

    private static void Conclusion()
    {
        Header("ВЫВОДЫ ПО ЗАДАНИЮ 3");

        Console.WriteLine("1) Boxing — упаковка значимого типа (int, double, struct) в объект на куче.");
        Console.WriteLine("   Для int создаётся объект 24 байта, поэтому 1 000 000 упаковок = ~23 МБ мусора.");
        Console.WriteLine();
        Console.WriteLine("2) Цена упаковки складывается из трёх частей:");
        Console.WriteLine("   - выделение памяти в куче (нужен GC для освобождения);");
        Console.WriteLine("   - запись значения в объект и обратное чтение (копирование данных);");
        Console.WriteLine("   - отсутствие JIT-оптимизаций, возможных для типизированного кода.");
        Console.WriteLine();
        Console.WriteLine("3) Когда boxing НЕ происходит:");
        Console.WriteLine("   - int -> object -> int в одном выражении компилятор оптимизирует;");
        Console.WriteLine("   - List<T>, Dictionary<TKey,TValue> и массивы T хранят значения без упаковки;");
        Console.WriteLine("   - параметр обобщённого метода T не упаковывается.");
        Console.WriteLine();
        Console.WriteLine("4) Когда boxing происходит:");
        Console.WriteLine("   - ArrayList, Hashtable и любые коллекции нетипизированных значений;");
        Console.WriteLine("   - object o = 42; неявное приведение int -> object;");
        Console.WriteLine("   - вызов перегруженного метода, когда выбирается вариант с object;");
        Console.WriteLine("   - присваивание в переменную типа object или интерфейс.");
        Console.WriteLine();
        Console.WriteLine("5) Способ измерить boxing без сторонних инструментов:");
        Console.WriteLine("   GC.GetTotalAllocatedBytes(precise: true) — счётчик аллокаций процесса.");
    }
}
