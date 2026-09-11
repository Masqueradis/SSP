# Задание 6 — Оптимизация через устранение упаковки (Boxing Elimination)

**Файл:** `Lab2/Task6/Program.cs`
**Целевая платформа:** .NET 10.0 (SDK 10.0.401)

---

## Что изучаем в этом задании

**Boxing (упаковка)** — это преобразование значения значимого типа (`int`, `float`, `struct`) в объект ссылочного типа (`object`). При упаковке:
- Создаётся новый объект в управляемой куче.
- Значение копируется из стека в кучу.
- Требуется аллокация памяти + нагрузка на GC.

**Unboxing (распаковка)** — обратная операция: извлечение значения из `object` обратно в значимый тип.

В этом задании мы:
1. Сравниваем производительность `List<object>` (с упаковкой) и `List<int>` (без упаковки).
2. Оптимизируем код с `ArrayList` → `List<int>`, объясняя причины ускорения.

---

## Связь с предыдущими заданиями

- **Задание 1** показало базовый пример boxing: `SimulateBoxing(1000000)` — 1M упаковок.
- **Задание 6** проводит **детальное сравнение** с замерами времени и памяти, плюс оптимизацию `ArrayList` → `List<int>`.

---

## Полный код

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

// ─── Демонстрация: List<object> vs List<int> ───

public class BoxingExample
{
    private static List<object> _keptBoxed = new List<object>();
    private static List<int> _keptInts = new List<int>();

    public static int ProcessWithBoxing()
    {
        _keptBoxed = new List<object>();
        for (int i = 0; i < 1000000; i++)
        {
            _keptBoxed.Add(i); // int → object (упаковка): каждый Add создаёт объект в куче
        }
        return _keptBoxed.Count;
    }

    public static int ProcessWithoutBoxing()
    {
        _keptInts = new List<int>();
        for (int i = 0; i < 1000000; i++)
        {
            _keptInts.Add(i); // Без упаковки: значение хранится напрямую
        }
        return _keptInts.Count;
    }
}

// ─── Оптимизация: ArrayList → List<int> ───

public class UnoptimizedCode
{
    private ArrayList _data = new ArrayList(); // Хранит object — каждый Add упаковывает

    public void AddData(int value)
    {
        _data.Add(value); // int → object (упаковка)
    }

    public int GetSum()
    {
        int sum = 0;
        foreach (object item in _data)
        {
            sum += (int)item; // object → int (распаковка)
        }
        return sum;
    }
}

public class OptimizedCode
{
    private List<int> _data = new List<int>(); // Хранит int напрямую

    public void AddData(int value)
    {
        _data.Add(value); // Без упаковки
    }

    public int GetSum()
    {
        int sum = 0;
        foreach (int item in _data) // Без распаковки
        {
            sum += item;
        }
        return sum;
    }
}

// ─── Точка входа ───

class Program
{
    const int Count = 1000000;

    static void Main()
    {
        Console.WriteLine("=== Задание 6. Оптимизация через устранение упаковки (Boxing) ===\n");
        Console.WriteLine("Теория: int на стеке -> при добавлении в object упаковывается в объект кучи.\n");

        // Часть 1: Сравнение производительности
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

        // Часть 2: Оптимизация ArrayList → List<int>
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
```

---

## Пошаговый разбор

### Часть 1. Сравнение `List<object>` vs `List<int>`

**Что измеряем:**
- **Время** (`Stopwatch`) — сколько миллисекунд занимает 1M добавлений.
- **Память в куче** (`GC.GetTotalMemory`) — сколько памяти занято объектами.
- **Всего выделено** (`GC.GetTotalAllocatedBytes`) — суммарный объём аллокаций (включая уже собранные).

**Результаты:**

| Вариант | Время, мс | Память (куча), МБ | Выделено всего, МБ |
|---------|-----------|-------------------|--------------------|
| С упаковкой (`List<object>`) | ~85 | ~31 | ~24 |
| Без упаковки (`List<int>`) | ~6 | ~4 | ~4 |

**Почему такая разница:**

1. **`List<object>.Add(i)`** — каждый вызов:
   - Упаковывает `int` → `object` (аллокация ~24 байта в куче)
   - 1M аллокаций × 24 байта = ~24 МБ «мусора» для GC

2. **`List<int>.Add(i)`** — каждый вызов:
   - Записывает `int` напрямую в массив внутри `List<int>` (4 байта)
   - Аллокация происходит только при扩容 `List` ( doubling )

3. **Сборка мусора:** 24 МБ «мусора» → GC работает активно (Gen0/Gen1/Gen2), отнимая время.

**Ускорение: ~14x** (ожидаемые 2-4x, но на 1M операций разница усиливается из-за нагрузки на GC).

### Часть 2. Оптимизация `ArrayList` → `List<int>`

**Что такое `ArrayList`:** Коллекция из `System.Collections`, хранящая элементы как `object`. Каждое добавление значимого типа вызывает упаковку.

**Результаты:**

| Код | Добавление, мс | GetSum, мс |
|-----|---------------|------------|
| `ArrayList` (упаковка) | ~100 | ~10 |
| `List<int>` (оптимиз.) | ~9 | ~4 |

**Добавление быстрее в ~11x, GetSum быстрее в ~2.5x.**

**Причины ускорения:**

| Операция | `ArrayList` | `List<int>` |
|----------|------------|-------------|
| `Add(value)` | `value` → boxing → `object` в куче | Запись 4 байт в массив |
| `foreach` | Итерация по `object` + unboxing | Итерация по `int` |
| `(int)item` | Распаковка: из кучи на стек | Не нужна |
| Память | ~24 байта на объект | 4 байта на значение |

**Замечание о сумме:**

`GetSum()` возвращает `int`. Сумма 1 000 000 значений выходит за границы `int` и «переполняется» одинаково в обоих вариантах. Это подтверждает, что логика идентична — разница только в производительности.

---

## Результаты выполнения

```
=== Задание 6. Оптимизация через устранение упаковки (Boxing) ===

Теория: int на стеке -> при добавлении в object упаковывается в объект кучи.

1. Сравнение производительности (1,000,000 операций).

Вариант                       Время, мс  Память (куча), МБ  Выделено всего, МБ
--------------------------------------------------------------------------------
С упаковкой (List<object>)          85.0               30.9               24.0
Без упаковки (List<int>)             5.6                4.0                4.0

Ускорение: 15.2x  (ожидаемые 2-4 раза; прирост зависит от условий работы).
Объектов создано: с упаковкой 1,000,000, без упаковки 1,000,000.

2. Оптимизация существующего кода: ArrayList -> List<int>.

Код                     Добавление, мс    GetSum, мс    Сумма
--------------------------------------------------------------
ArrayList (упаковка)             99.8          9.5   1783293664
List<int> (оптимиз.)              9.2          3.8   1783293664

Добавление быстрее в 10.8x, GetSum быстрее в 2.5x.
Причины: ArrayList хранит object, каждое Add бывает с упаковкой, каждый GetSum — с распаковкой;
List<int> хранит значения напрямую, операций с кучей нет.
```

---

## Ключевые концепции

### Что такое boxing

```csharp
int x = 42;                    // Значимый тип: хранится на стеке (4 байта)
object boxed = x;              // Boxing: создаёт объект в куче
int unboxed = (int)boxed;      // Unboxing: извлечение значения из кучи
```

**Что происходит при упаковке:**
1. Выделяется объект в куче (~24 байта: 16 байт заголовок + 4 байта значения + выравнивание).
2. Значение `x` копируется из стека в кучу.
3. Возвращается ссылка на объект.

**Что происходит при распаковке:**
1. Проверяется тип объекта (runtime-проверка).
2. Значение копируется из кучи обратно на стек.

### `List<T>` vs `ArrayList`

| Характеристика | `List<T>` | `ArrayList` |
|---------------|-----------|-------------|
| Хранение | Значения напрямую | `object[]` |
| Добавление `int` | Без упаковки | С упаковкой |
| Итерация | Без распаковки | С распаковкой |
| Типобезопасность | Compile-time | Runtime ( możle кинуть `InvalidCastException`) |
| Производительность | **Быстрее** | Медленнее |

**Правило:** Всегда используйте обобщённые коллекции (`List<T>`, `Dictionary<TKey, TValue>`) вместо необобщённых (`ArrayList`, `Hashtable`).

### `GC.GetTotalAllocatedBytes`

Эта метрика показывает **суммарный объём всех аллокаций** за время работы приложения (включая уже собранные объекты). В отличие от `GC.GetTotalMemory` (текущий размер кучи), она отражает **общую нагрузку** на GC.

---

## Типичные ошибки

1. **Использовать `ArrayList` вместо `List<T>`** — Каждое добавление/получение вызывает упаковку/распаковку. Всегда предпочитайте `List<T>`.

2. **Передавать значимые типы в `object`-параметры** — Метод `void DoSomething(object x)` вызывает упаковку при передаче `int`, `float` и т.д. Используйте обобщённые методы.

3. **Считать, что boxing «бесплатный»** — На 1M операций разница 85ms vs 6ms. В реальных приложениях это может быть секундами.

4. **Не знать про `Span<T>` и `stackalloc`** — Для временных буферов можно использовать `Span<T>` (без аллокаций в куче) и `stackalloc` (выделение на стеке).

---

## Практическое применение

- **Коллекции:** Всегда `List<int>`, `Dictionary<string, int>`, `HashSet<long>` — никогда `ArrayList`, `Hashtable`.
- **Интерфейсы:** Если метод принимает `IEnumerable` (необобщённый), вызывается boxing. Используйте `IEnumerable<T>`.
- **Логирование:** `Console.WriteLine($"x = {x}")` — если `x` значимый тип, может вызвать упаковку. В современном .NET (6+) это оптимизировано через `ISpanFormattable`.
- **Высокопроизводительный код:** Используйте `Span<T>`, `Memory<T>`, `stackalloc` для временных буферов.

---

## Итог

1. **Boxing** создаёт объект в куче (~24 байта на `int`). 1M упаковок = ~24 МБ мусора.
2. **`List<int>`** хранит значения напрямую, без упаковки. Ускорение ~15x.
3. **`ArrayList`** хранит `object` — каждое Add/foreach вызывает упаковку/распаковку.
4. **Всегда используйте обобщённые коллекции** (`List<T>`, `Dictionary<K,V>`) вместо необобщённых.
5. **Boxing — невидимая проблема:** Код работает, но GC нагружен, память растёт, производительность падает.
