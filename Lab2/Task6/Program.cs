using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class BoxingExample
{
    private static List<object> _keptBoxed = new List<object>();
    private static List<int> _keptInts = new List<int>();

    public static int ProcessWithBoxing()
    {
        _keptBoxed = new List<object>();
        for (int i = 0; i < 1000000; i++)
        {
            _keptBoxed.Add(i);
        }
        return _keptBoxed.Count;
    }

    public static int ProcessWithoutBoxing()
    {
        _keptInts = new List<int>();
        for (int i = 0; i < 1000000; i++)
        {
            _keptInts.Add(i);
        }
        return _keptInts.Count;
    }
}

public class UnoptimizedCode
{
    private ArrayList _data = new ArrayList();

    public void AddData(int value)
    {
        _data.Add(value);
    }

    public int GetSum()
    {
        int sum = 0;
        foreach (object item in _data)
        {
            sum += (int)item;
        }
        return sum;
    }
}

public class OptimizedCode
{
    private List<int> _data = new List<int>();

    public void AddData(int value)
    {
        _data.Add(value);
    }

    public int GetSum()
    {
        int sum = 0;
        foreach (int item in _data)
        {
            sum += item;
        }
        return sum;
    }
}

class Program
{
    const int Count = 1000000;

    static void Main()
    {
        Console.WriteLine("=== Задание 6. Оптимизация через устранение упаковки (Boxing) ===\n");

        Console.WriteLine("Теория: int на стеке -> при добавлении в object упаковывается в объект кучи.\n");

        Console.WriteLine("1. Сравнение производительности (1,000,000 операций).\n");

        GC.Collect();
        long memBefore = GC.GetTotalMemory(true);
        long allocBefore = GC.GetTotalAllocatedBytes(true);

        var sw = Stopwatch.StartNew();
        int countBoxed = BoxingExample.ProcessWithBoxing();
        sw.Stop();
        long allocAfterBoxed = GC.GetTotalAllocatedBytes(true);
        long memWithBoxing = GC.GetTotalMemory(true) - memBefore;
        double timeWithBoxing = sw.Elapsed.TotalMilliseconds;

        GC.Collect();
        memBefore = GC.GetTotalMemory(true);

        sw.Restart();
        int countUnboxed = BoxingExample.ProcessWithoutBoxing();
        sw.Stop();
        long allocAfterUnboxed = GC.GetTotalAllocatedBytes(true);
        long memWithoutBoxing = GC.GetTotalMemory(true) - memBefore;
        double timeWithoutBoxing = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"{"Вариант",-28} {"Время, мс",12} {"Память (куча), МБ",18} {"Выделено всего, МБ",18}");
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"{"С упаковкой (List<object>)",-28} {timeWithBoxing,12:F1} {memWithBoxing / 1024.0 / 1024.0,16:F1} {(allocAfterBoxed - allocBefore) / 1024.0 / 1024.0,17:F1}");
        Console.WriteLine($"{"Без упаковки (List<int>)",-28} {timeWithoutBoxing,12:F1} {memWithoutBoxing / 1024.0 / 1024.0,16:F1} {(allocAfterUnboxed - allocBefore) / 1024.0 / 1024.0,17:F1}");

        double speedup = timeWithBoxing / timeWithoutBoxing;
        Console.WriteLine($"\nУскорение: {speedup:F1}x  (ожидаемые 2-4 раза; прирост зависит от условий работы).");
        Console.WriteLine($"Объектов создано: с упаковкой {countBoxed:N0}, без упаковки {countUnboxed:N0}.");

        Console.WriteLine("\n2. Оптимизация существующего кода: ArrayList -> List<int>.\n");
        CompareOptimization();

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }

    static void CompareOptimization()
    {
        var unopt = new UnoptimizedCode();
        var opt = new OptimizedCode();

        var sw = new Stopwatch();

        sw.Start();
        for (int i = 0; i < Count; i++) unopt.AddData(i);
        sw.Stop();
        double addUnopt = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        for (int i = 0; i < Count; i++) opt.AddData(i);
        sw.Stop();
        double addOpt = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        int sumUnopt = unopt.GetSum();
        sw.Stop();
        double sumUnoptTime = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        int sumOpt = opt.GetSum();
        sw.Stop();
        double sumOptTime = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"{"Код",-22} {"Добавление, мс",16} {"GetSum, мс",12} {"Сумма",8}");
        Console.WriteLine(new string('-', 62));
        Console.WriteLine($"{"ArrayList (упаковка)",-22} {addUnopt,15:F1} {sumUnoptTime,11:F1} {sumUnopt,8}");
        Console.WriteLine($"{"List<int> (оптимиз.)",-22} {addOpt,15:F1} {sumOptTime,11:F1} {sumOpt,8}");

        Console.WriteLine($"\nДобавление быстрее в {addUnopt / addOpt:F1}x, GetSum быстрее в {sumUnoptTime / sumOptTime:F1}x.");
        Console.WriteLine("Причины: ArrayList хранит object, каждое Add бывает с упаковкой, каждый GetSum — с распаковкой;");
        Console.WriteLine("List<int> хранит значения напрямую, операций с кучей нет.");
    }
}