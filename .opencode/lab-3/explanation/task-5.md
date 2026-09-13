# Задание 5 — Доступ к приватному полю через FieldInfo/SetValue

**Файл:** `Lab3/Task5/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Рефлексия позволяет не только *увидеть* приватное поле (Задание 4), но и
**изменить** его значение — то, что обычный C# запрещает. В задании:

> получить приватное поле через `GetField` + `BindingFlags`, изменить его
> значение вызовом `FieldInfo.SetValue` и убедиться, что публичный метод
> чтения вернул новое значение.

Дополнительно разбираются «ошибочные» сценарии: поиск без флага `NonPublic`
и запрос к несуществующему полю, а также изменение `readonly`-поля.

---

## 2. Полный код программы

```csharp
using System;
using System.Reflection;

public class BlackBox
{
    private int _secret;
    private readonly int _multiplier;

    public BlackBox(int seed)
    {
        _multiplier = 3;
        _secret = Compute(seed);
    }

    public int ReadSecret()
    {
        return _secret;
    }

    private int Compute(int value)
    {
        return (value * _multiplier + 5) % 100;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 5: ДОСТУП К ПРИВАТНОМУ ПОЛЮ ЧЕРЕЗ РЕФЛЕКСИЮ ===\n");

        var box = new BlackBox(42);
        Type type = typeof(BlackBox);

        Console.WriteLine($"Начальное значение (публичное чтение): {box.ReadSecret()}");

        Console.WriteLine("\n5.1 Поле не найдено без флага NonPublic:");
        FieldInfo? plain = type.GetField("_secret");
        Console.WriteLine($"  GetField(\"_secret\") без флагов -> {(plain == null ? "null (в поиске участвуют только публичные члены)" : "найдено")}");

        Console.WriteLine("\n5.2 Несуществующее поле -> исключение обработано:");
        FieldInfo? missing = type.GetField("_nonexistent", BindingFlags.NonPublic | BindingFlags.Instance);
        if (missing == null)
        {
            Console.WriteLine("  GetField(\"_nonexistent\") вернул null, случай обработан.");
        }

        Console.WriteLine("\n5.3 Получение приватного поля с корректными флагами:");
        FieldInfo? secretField = type.GetField("_secret", BindingFlags.NonPublic | BindingFlags.Instance);
        if (secretField != null)
        {
            try
            {
                int oldValue = (int)secretField.GetValue(box)!;
                Console.WriteLine($"  Чтение через рефлексию: _secret = {oldValue}");

                secretField.SetValue(box, 777);
                int newValue = (int)secretField.GetValue(box)!;
                Console.WriteLine($"  После SetValue(777):      _secret = {newValue}");
                Console.WriteLine($"  Проверка публичным методом ReadSecret(): {box.ReadSecret()}");

                Console.WriteLine("\n  Демонстрация нарушения инкапсуляции: значение изменено без обращения");
                Console.WriteLine("  к публичному API — только через FieldInfo.SetValue.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ошибка доступа: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine("\n5.4 Изменение приватного поля readonly:");
        FieldInfo? multiplierField = type.GetField("_multiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            multiplierField!.SetValue(box, 9);
            Console.WriteLine($"  Поле _multiplier с помощью SetValue теперь = {multiplierField.GetValue(box)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Исключение: {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("\n5.5 Попытка применить рефлексию без прав (продемонстрировано выше 5.1):");
        Console.WriteLine("  Без BindingFlags.NonPublic рефлексия видит публичную поверхность типа.");
    }
}
```

---

## 3. Разбор пошагово

### 3.0 Отправная точка

```csharp
var box = new BlackBox(42);          // _secret = (42*3+5)%100 = 31
Console.WriteLine($"Начальное значение: {box.ReadSecret()}");   // 31
```

Публичный метод `ReadSecret()` — «эталон правды»: любое изменение через
рефлексию мы будем сверять именно с ним.

### 3.1 `GetField("_secret")` без флагов → null

```csharp
FieldInfo? plain = type.GetField("_secret");
```

Флаговый механизм применим и к полям: по умолчанию `GetField` ищет только
**публичные** члены. `_secret` приватный → результат `null`. Это не исключение,
а обычное «не найдено», поэтому в выводе — пояснение в скобках.

### 3.2 Несуществующее поле → тоже null (обработка)

```csharp
FieldInfo? missing = type.GetField("_nonexistent", BindingFlags.NonPublic | BindingFlags.Instance);
if (missing == null) { Console.WriteLine("...  вернул null, случай обработан."); }
```

Вывод: **отсутствие поля и недоступность дают одинаковый результат — `null`**.
Поэтому перед использованием `FieldInfo` обязательна проверка на `null` и
защита `try/catch` (в реальном коде — обработка `MissingFieldException` при
строгой проверке).

### 3.3 Чтение и запись приватного поля

```csharp
FieldInfo? secretField = type.GetField("_secret", BindingFlags.NonPublic | BindingFlags.Instance);
if (secretField != null)
{
    int oldValue = (int)secretField.GetValue(box)!;   // читаем: 31
    secretField.SetValue(box, 777);                   // пишем
    int newValue = (int)secretField.GetValue(box)!;   // читаем: 777
    // сверка легальным API:
    Console.WriteLine($"  Проверка публичным методом ReadSecret(): {box.ReadSecret()}"); // 777
}
```

Механика: `GetValue(box)` возвращает `object?`, распаковывается в `int`.
`SetValue(box, 777)` — упаковка `777`, вызов геттера/сеттера поля на уровне CLR.
Сверка подтверждает: **инкапсуляция обойдена без единого вызова public API**.

### 3.4 `readonly`-поле тоже меняется

```csharp
FieldInfo? multiplierField = type.GetField("_multiplier", BindingFlags.NonPublic | BindingFlags.Instance);
multiplierField!.SetValue(box, 9);   // успешно!
```

`readonly` — ограничение, которое накладывает компилятор (и фиксированный
`IsInitOnly` в метаданных). Рефлексия пишет по факту: значение меняется на 9
(первоначально было 3). После обновления `_secret` (777) и изменения
`_multiplier` логика `Compute` уже не будет давать прежние результаты — 
побочный эффект, о котором стоит помнить.

### 3.5 Резюме «без прав»

Программа возвращается к примеру 5.1: без `BindingFlags.NonPublic` рефлексия
видит лишь публичную поверхность типа — и поля, и методы ищутся только среди
публичных.

---

## 4. Полный вывод программы

```
=== ЗАДАНИЕ 5: ДОСТУП К ПРИВАТНОМУ ПОЛЮ ЧЕРЕЗ РЕФЛЕКСИЮ ===

Начальное значение (публичное чтение): 31

5.1 Поле не найдено без флага NonPublic:
  GetField("_secret") без флагов -> null (в поиске участвуют только публичные члены)

5.2 Несуществующее поле -> исключение обработано:
  GetField("_nonexistent") вернул null, случай обработан.

5.3 Получение приватного поля с корректными флагами:
  Чтение через рефлексию: _secret = 31
  После SetValue(777):      _secret = 777
  Проверка публичным методом ReadSecret(): 777

  Демонстрация нарушения инкапсуляции: значение изменено без обращения
  к публичному API — только через FieldInfo.SetValue.

5.4 Изменение приватного поля readonly:
  Поле _multiplier с помощью SetValue теперь = 9

5.5 Попытка применить рефлексию без прав (продемонстрировано выше 5.1):
  Без BindingFlags.NonPublic рефлексия видит публичную поверхность типа.
```

---

## 5. Опасности, о которых важно сказать

- **Не использовать в боевом коде** без острой необходимости: обход
  инкапсуляции ломает инварианты класса.
- `readonly`-поля и «теневое» изменение `_multiplier` портят вычисления
  (`Compute` начнёт давать другие результаты) — на этом примере видно, что
  рефлексия может «незаметно» сломать логику.
- Имена полей — строки. Переименование `_secret` в `_secretValue` молча
  сломает рантайм; компилятор об этом не предупредит.
- Есть контексты, где рефлексия ограничена: AOT-платформы (iOS/UWP),
  частичное доверие, protected-sandbox.

---

## 6. Вопросы для защиты

1. **Почему `GetField("_secret")` вернул null, а не исключение?**
   — `GetField` по контракту возвращает `null`, если член не найден. Исключение
   (например, `AmbiguousMatchException`) бросается в других случаях: несколько
   кандидатов, конфликт типов.

2. **Разрешено ли менять `readonly`-поле?**
   — Модификатор защищает от присваивания в конструкторе и в коде C#;
   переопределённая семантика «инициализируется один раз» — ограничение
   компилятора, а не CLR. `SetValue` работает.

3. **Что такое unboxing/boxing при чтении поля?**
   — `GetValue` возвращает `object` (значение упаковано). `(int)` распаковывает
   его в ячейку типа `int`. Обратно — `SetValue(object)` упаковывает `777`.

4. **Как «безопасно» вернуть значение после рефлексии?**
   — Только через легальный API (`ReadSecret()`) — и именно это показано:
   `Проверка публичным методом ReadSecret(): 777`.