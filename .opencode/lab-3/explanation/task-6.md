# Задание 6 — Поиск и вызов приватных методов

**Файл:** `Lab3/Task6/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Найти приватные методы через `BindingFlags.NonPublic` и **вызвать** их. По
условию:

> найти закрытый метод и вызвать его: первый способ — через `MethodInfo.Invoke`
> с разным числом параметров, второй — создание делегата, третий — через
> `Expression`.

В программе последовательно показаны все **три способа**, каждый на трёх
методах: с 2 параметрами (`HiddenAdd`), с 1 параметром (`LogIt`) и
статический (`Triple`).

---

## 2. Полный код программы

```csharp
using System;
using System.Linq.Expressions;
using System.Reflection;

public class HiddenService
{
    private int HiddenAdd(int a, int b)      // приватный, 2 параметра
    {
        int res = a + b;
        Console.WriteLine($"  [HiddenAdd] {a} + {b} = {res}");
        return res;
    }

    private void LogIt(string message)       // приватный, 1 параметр
    {
        Console.WriteLine($"  [LOG] {message}");
    }

    private static int Triple(int value)     // приватный статический
    {
        int res = value * 3;
        Console.WriteLine($"  [Triple] {value} * 3 = {res}");
        return res;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 6: ПОИСК И ВЫЗОВ ПРИВАТНЫХ МЕТОДОВ ===\n");

        var service = new HiddenService();
        Type type = typeof(HiddenService);

        Console.WriteLine("6.1 Поиск приватных методов:");
        MethodInfo addMi = type.GetMethod("HiddenAdd", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Метод HiddenAdd не найден");
        MethodInfo logMi = type.GetMethod("LogIt", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Метод LogIt не найден");
        MethodInfo tripleMi = type.GetMethod("Triple", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Метод Triple не найден");
        Console.WriteLine($"  HiddenAdd: {addMi.Name}");
        Console.WriteLine($"  LogIt:     {logMi.Name}");
        Console.WriteLine($"  Triple:    {tripleMi.Name} (статический)");

        Console.WriteLine("\n6.2 Способ 1: вызов через MethodInfo.Invoke (разное число параметров):");
        object r1 = addMi.Invoke(service, new object[] { 5, 7 })!;
        Console.WriteLine($"  HiddenAdd(5, 7) = {r1} (2 параметра)");

        logMi.Invoke(service, new object[] { "вызов с одним параметром" });
        Console.WriteLine("  LogIt(\"...\") вызван (1 параметр)");

        object r2 = tripleMi.Invoke(null, new object[] { 10 })!;
        Console.WriteLine($"  Triple(10) = {r2} (статический)");

        Console.WriteLine("\n6.3 Способ 2: создание делегата через рефлексию (CreateDelegate):");
        Func<int, int, int> d1 = (Func<int, int, int>)Delegate.CreateDelegate(
            typeof(Func<int, int, int>), service, addMi)!;
        Console.WriteLine($"  Делегат d1: d1(3, 4) = {d1(3, 4)}");

        Action<string> d2 = (Action<string>)Delegate.CreateDelegate(
            typeof(Action<string>), service, logMi)!;
        d2("вызов через делегат");
        Console.WriteLine("  Делегат d2 (Action) выполнен");

        Func<int, int> d3 = (Func<int, int>)Delegate.CreateDelegate(
            typeof(Func<int, int>), null, tripleMi)!;
        Console.WriteLine($"  Делегат d3 (статический): d3(5) = {d3(5)}");

        Console.WriteLine("\n6.4 Способ 3: создание делегата через Expression: ");
        var pa = Expression.Parameter(typeof(int), "a");
        var pb = Expression.Parameter(typeof(int), "b");
        var call = Expression.Call(Expression.Constant(service), addMi, pa, pb);
        Func<int, int, int> d4 = Expression.Lambda<Func<int, int, int>>(call, pa, pb).Compile();
        Console.WriteLine($"  Делегат d4 (Expression): d4(11, 22) = {d4(11, 22)}");

        Console.WriteLine("\n6.5 Приватные методы доступны через рефлексию вопреки ограничениям доступа:");
        Console.WriteLine("  MethodInfo.Invoke, CreateDelegate и Expression работают с private-членами.");
    }
}
```

---

## 3. Поиск методов (шаг 6.1)

```csharp
MethodInfo addMi = type.GetMethod("HiddenAdd", BindingFlags.NonPublic | BindingFlags.Instance)
    ?? throw new InvalidOperationException("Метод HiddenAdd не найден");
```

- `BindingFlags.NonPublic | Instance` — «достать» приватный экземплярный метод;
- `BindingFlags.NonPublic | Static` — для `Triple` (статический);
- `?? throw ...` — если метод отсутствует, программа сразу останавливается
  с понятным сообщением (минималистичная защита от null).

Все три метода найдены и распечатаны по имени — подтверждение, что рефлексия
«пробила» private-видимость на этапе поиска.

---

## 4. Способ 1 — `MethodInfo.Invoke` (шаг 6.2)

```csharp
object r1 = addMi.Invoke(service, new object[] { 5, 7 })!;   // 12
logMi.Invoke(service, new object[] { "вызов с одним параметром" });
object r2 = tripleMi.Invoke(null, new object[] { 10 })!;     // 30
```

Механика:
- **первый аргумент** `Invoke` — объект-экземпляр (`service`), для статического
  метода — `null`;
- **второй аргумент** — массив параметров `object[]` (значения упаковываются);
- **результат** приходит как `object?` (упаковка int), распаковывается в `int`.

Так работают «универсальные» вызовы: существует риск `TargetInvocationException`,
если внутри метода происходит исключение — его «обёртка» видна в `InnerException`.

---

## 5. Способ 2 — `Delegate.CreateDelegate` (шаг 6.3)

```csharp
Func<int, int, int> d1 = (Func<int, int, int>)Delegate.CreateDelegate(
    typeof(Func<int, int, int>), service, addMi)!;

Action<string> d2 = (Action<string>)Delegate.CreateDelegate(
    typeof(Action<string>), service, logMi)!;

Func<int, int> d3 = (Func<int, int>)Delegate.CreateDelegate(
    typeof(Func<int, int>), null, tripleMi)!;
```

Особенности:
- типы делегатов (`Func<...>`, `Action<...>`) совпадают с сигнатурами методов по
  параметрам и возвращаемому типу — иначе `CreateDelegate` бросит исключение;
- для **экземплярных** методов передаётся `service` (объект «прибивается»
  к делегату), для **статических** — `null`;
- делегат связывает сигнатуру C# с IL-сигнатурой метода: вызовы `d1(3,4)`
  типизированы и заметно быстрее, чем `Invoke` с массивами.

---

## 6. Способ 3 — `Expression` (шаг 6.4)

```csharp
var pa = Expression.Parameter(typeof(int), "a");
var pb = Expression.Parameter(typeof(int), "b");
var call = Expression.Call(Expression.Constant(service), addMi, pa, pb);
Func<int, int, int> d4 = Expression.Lambda<Func<int, int, int>>(call, pa, pb).Compile();
```

Построение «дерева вызова»:
1. `Expression.Parameter(typeof(int), "a")` — параметры a, b;
2. `Expression.Call(выражение-цель, methodInfo, аргументы)` — узел вызова
   приватного метода на объекте `service` (через `Constant`);
3. `Expression.Lambda<Func<int,int,int>>(call, pa, pb)` — «обёртка» дерева
   в типизированный делегат;
4. `.Compile()` — компиляция дерева в машинный код; результат — делегат `d4`.

Это самый гибкий вариант: дерево можно строить динамически, кэшировать,
комбинировать (здесь можно применить обёртки, преобразования типов и т.п.).

---

## 7. Полный вывод программы

```
=== ЗАДАНИЕ 6: ПОИСК И ВЫЗОВ ПРИВАТНЫХ МЕТОДОВ ===

6.1 Поиск приватных методов:
  HiddenAdd: HiddenAdd
  LogIt:     LogIt
  Triple:    Triple (статический)

6.2 Способ 1: вызов через MethodInfo.Invoke (разное число параметров):
  [HiddenAdd] 5 + 7 = 12
  HiddenAdd(5, 7) = 12 (2 параметра)
  [LOG] вызов с одним параметром
  LogIt("...") вызван (1 параметр)
  [Triple] 10 * 3 = 30
  Triple(10) = 30 (статический)

6.3 Способ 2: создание делегата через рефлексию (CreateDelegate):
  [HiddenAdd] 3 + 4 = 7
  Делегат d1: d1(3, 4) = 7
  [LOG] вызов через делегат
  Делегат d2 (Action) выполнен
  [Triple] 5 * 3 = 15
  Делегат d3 (статический): d3(5) = 15

6.4 Способ 3: создание делегата через Expression: 
  [HiddenAdd] 11 + 22 = 33
  Делегат d4 (Expression): d4(11, 22) = 33

6.5 Приватные методы доступны через рефлексию вопреки ограничениям доступа:
  MethodInfo.Invoke, CreateDelegate и Expression работают с private-членами.
```

---

## 8. Сравнение способов вызова

| Способ | Скорость | Гибкость | Когда применять |
|--------|----------|----------|-----------------|
| `MethodInfo.Invoke` | низкая (боксинг, поиск по имени) | высокая — любая сигнатура | разовые вызовы, имя известно в рантайме |
| `Delegate.CreateDelegate` | высокая | ниже — сигнатура задана типом делегата | многократные вызовы одного метода |
| `Expression` | высокая | максимальная — дерево строится программно | динамическая генерация, кэширование, маппинг |

---

## 9. Вопросы для защиты

1. **Зачем в `GetMethod` передавать `BindingFlags.NonPublic`?**
   — Без него приватные методы исключены из поиска и `GetMethod` вернёт null.

2. **Почему для `Invoke` статического метода первый аргумент `null`?**
   — У статического метода нет экземпляра; объект передаётся только для
   экземплярных методов.

3. **Чем делегат лучше `Invoke`?**
   — Делегат получает сигнатуру на этапе построения, вызывается со строгими
   типами и быстрее; `Invoke` каждый раз упаковывает/распаковывает параметры.

4. **Что делает `.Compile()` у `Expression`?**
   — Создаёт выполняемый делегат из дерева выражений; ускоряет вызов и позволяет
   повторно использовать построенную функцию.