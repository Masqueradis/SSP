using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Задание 5. Исследование Large Object Heap (LOH) ===\n");

        RunLOHCreation();
        RunLOHMonitoring();

        Console.WriteLine("\nНажмите любую клавишу для завершения...");
        try { Console.ReadKey(); } catch { }
    }

    static void RunLOHCreation()
    {
        Console.WriteLine("1. Создание объектов в LOH: 100 массивов по 100 КБ.\n");
        Console.WriteLine($"Память ДО выделения: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");

        var holder = new List<byte[]>();
        for (int i = 0; i < 100; i++)
        {
            holder.Add(new byte[100000]);
        }

        Console.WriteLine($"Память ПОСЛЕ выделения: {GC.GetTotalMemory(false) / (1024 * 1024)} MB");

        GC.Collect();
        GC.WaitForPendingFinalizers();

        Console.WriteLine($"Память ПОСЛЕ GC.Collect(): {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        Console.WriteLine($"Поколение LOH-объекта (100 КБ): {GC.GetGeneration(holder[0])}");

        Console.WriteLine("\nСравнение размещения 90 КБ и 80 КБ:");
        var arr90 = new byte[90000];
        var arr80 = new byte[80000];
        Console.WriteLine($"  90 КБ (> 85 КБ) -> поколение {GC.GetGeneration(arr90)}, размещён в LOH");
        Console.WriteLine($"  80 КБ (< 85 КБ) -> поколение {GC.GetGeneration(arr80)}, размещён в SOH (Gen0)");
        Console.WriteLine("=> Объекты больше 85 КБ сразу попадают в LOH и не уплотняются GC.");
    }

    static void RunLOHMonitoring()
    {
        Console.WriteLine("\n2. Мониторинг LOH с помощью GC.GetGeneration().\n");

        int[] sizesKB = { 10, 50, 90, 200 };
        var objects = new List<byte[]>();

        foreach (int kb in sizesKB)
        {
            var data = new byte[kb * 1024];
            objects.Add(data);
            bool inLoh = kb * 1024 > 85000;
            Console.WriteLine($"размер {kb,3} КБ: поколение {GC.GetGeneration(data)}, в LOH: {inLoh}");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Console.WriteLine("\nПосле GC.Collect():");
        for (int i = 0; i < sizesKB.Length; i++)
        {
            int kb = sizesKB[i];
            var data = objects[i];
            bool inLoh = kb * 1024 > 85000;
            Console.WriteLine($"размер {kb,3} КБ: поколение {GC.GetGeneration(data)}, в LOH: {inLoh}");
        }
        Console.WriteLine("\nВывод: объекты 10 и 50 КБ живут в SOH и перемещаются по поколениям,");
        Console.WriteLine("90 и 200 КБ сразу попадают в LOH (поколение 2) и переживают сборки.");
    }
}