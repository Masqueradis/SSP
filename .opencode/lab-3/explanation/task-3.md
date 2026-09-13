# Задание 3 — Создание экземпляра через Activator.CreateInstance

**Файл:** `Lab3/Task3/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

**Позднее связывание** — самый узнаваемый сценарий рефлексии. Требование кода
состоит в том, чтобы объект создавался **без оператора `new`**:

> создать экземпляр класса через `Activator.CreateInstance`, используя тип,
> полученный по имени строки, и вообще не упоминая класс в коде.

В программе это делается в **двух** вариантах:
1. через конструктор без параметров (`Activator.CreateInstance(type)`);
2. через конструктор с параметрами (`Activator.CreateInstance(type, args)` и
   `ConstructorInfo.Invoke(args)`).

Плюс демонстрируются свойственные позднему связыванию операции:
установка/чтение свойств через `PropertyInfo`, вызов перегрузок, приватного и
статического методов.

---

## 2. Полный код программы

```csharp
using System;
using System.Reflection;

public class DynamicCalculator
{
    private int _result = 0;

    public string Label { get; set; } = "DynamicCalculator";

    public DynamicCalculator()
    {
        Console.WriteLine("  [Конструктор DynamicCalculator без параметров]");
    }

    public DynamicCalculator(int initialValue)
    {
        _result = initialValue;
        Console.WriteLine($"  [Конструктор с параметром: начальное значение = {initialValue}]");
    }

    public int Add(int a, int b)
    {
        int res = a + b;
        Console.WriteLine($"  Add({a}, {b}) = {res}");
        return res;
    }

    public double Add(double a, double b)
    {
        double res = a + b;
        Console.WriteLine($"  Add({a}, {b}) = {res}");
        return res;
    }

    public int Multiply(int a, int b) => a * b;

    private void LogOperation(string operation)
    {
        Console.WriteLine($"  [LOG] Выполнена операция: {operation}");
    }

    public int Result => _result;                 // свойство только для чтения

    public void SetResult(int value) => _result = value;

    public static string GetVersion() => "DynamicCalculator v1.0";
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 3: СОЗДАНИЕ ЭКЗЕМПЛЯРА ЧЕРЕЗ Activator.CreateInstance ===\n");

        Console.WriteLine("3.1 Получение типа по имени (позднее связывание):");
        string typeName = "DynamicCalculator";
        Type? calcType = Type.GetType(typeName);
        if (calcType == null)
        {
            Console.WriteLine("  Тип не найден");
            return;
        }
        Console.WriteLine($"  Тип найден: {calcType.Name}, FullName: {calcType.FullName}");

        Console.WriteLine("\n3.2 Создание экземпляра без оператора new (конструктор без параметров):");
        object calculator = Activator.CreateInstance(calcType)!;
        Console.WriteLine($"  Экземпляр создан: {calculator.GetType().Name}");

        Console.WriteLine("\n3.3 Установка значений свойств через рефлексию:");
        PropertyInfo labelProp = calcType.GetProperty("Label")!;
        labelProp.SetValue(calculator, "CalcFromReflection");
        Console.WriteLine($"  Label установлен через SetValue: {labelProp.GetValue(calculator)}");

        Console.WriteLine("\n3.4 Вызов методов через рефлексию:");
        MethodInfo addInt = calcType.GetMethod("Add", new[] { typeof(int), typeof(int) })!;
        int r1 = (int)addInt.Invoke(calculator, new object[] { 10, 20 })!;
        Console.WriteLine($"  Результат Add(10, 20): {r1}");

        MethodInfo addDouble = calcType.GetMethod("Add", new[] { typeof(double), typeof(double) })!;
        double r2 = (double)addDouble.Invoke(calculator, new object[] { 5.5, 3.2 })!;
        Console.WriteLine($"  Результат Add(5.5, 3.2): {r2}");

        MethodInfo multiplyMethod = calcType.GetMethod("Multiply")!;
        int r3 = (int)multiplyMethod.Invoke(calculator, new object[] { 7, 8 })!;
        Console.WriteLine($"  Результат Multiply(7, 8): {r3}");

        Console.WriteLine("\n3.5 Чтение значений через рефлексию (подтверждение создания):");
        PropertyInfo resultProp = calcType.GetProperty("Result")!;
        Console.WriteLine($"  Result: {resultProp.GetValue(calculator)}");
        Console.WriteLine($"  Label:  {labelProp.GetValue(calculator)}");

        Console.WriteLine("\n3.6 Вызов приватного метода:");
        MethodInfo logMethod = calcType.GetMethod("LogOperation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        logMethod.Invoke(calculator, new object[] { "Тестовая операция" });
        Console.WriteLine("  Приватный метод LogOperation вызван успешно");

        Console.WriteLine("\n3.7 Вызов статического метода:");
        MethodInfo versionMethod = calcType.GetMethod("GetVersion", BindingFlags.Public | BindingFlags.Static)!;
        Console.WriteLine($"  Версия: {versionMethod.Invoke(null, null)}");

        Console.WriteLine("\n3.8 Создание экземпляра через конструктор с параметрами:");
        object calcWithParam = Activator.CreateInstance(calcType, new object[] { 42 })!;
        Console.WriteLine($"  Result нового экземпляра: {resultProp.GetValue(calcWithParam)}");

        ConstructorInfo ctor = calcType.GetConstructor(new[] { typeof(int) })!;
        object calcViaCtor = ctor.Invoke(new object[] { 100 });
        Console.WriteLine($"  Через ConstructorInfo.Invoke: Result = {resultProp.GetValue(calcViaCtor)}");
    }
}
```

---

## 3. Разбор по шагам

### 3.1 Найти тип «по имени» — `Type.GetType("...")`

```csharp
Type? calcType = Type.GetType(typeName);
```

`typeName` — обычная **строка**. Тип берётся из текущей сборки по полному имени.
Это и есть **позднее связывание**: на этапе компиляции про `DynamicCalculator`
«ничего не знаем» — только во время исполнения имя превращается в объект
метаданных `Type`. Возврат `null` обрабатывается выходом с сообщением.

Для типов из других сборок сюда передаётся «полное имя, ИмяСборки»
(мы увидим это в Задании 7 в `plugins.config`).

### 3.2 Создание без `new` — `Activator.CreateInstance`

```csharp
object calculator = Activator.CreateInstance(calcType)!;
```

- Вызывается конструктор без параметров (сообщение в консоли это подтверждает).
- Возвращается `object?` — статически неизвестный тип, отсюда `!` (null-forgiving).
- `calculator.GetType().Name` возвращает *фактический* runtime-тип → `DynamicCalculator`.
  Здесь же уместно напомнить: `Type.GetType`, `typeof`, `GetType()` — три разных
  источника `Type` (см. answers.md, вопрос 3).

### 3.3 Запись свойства через `PropertyInfo.SetValue`

```csharp
PropertyInfo labelProp = calcType.GetProperty("Label")!;
labelProp.SetValue(calculator, "CalcFromReflection");
```

`GetProperty("Label")` — поиск по имени. `SetValue(объект, значение)` вызывает
IL-метод `set_Label`. Такой подход позволяет присваивать значения, когда тип
объекта известен только в рантайме.

### 3.4 Вызов метода с выбором перегрузки

```csharp
MethodInfo addInt = calcType.GetMethod("Add", new[] { typeof(int), typeof(int) })!;
int r1 = (int)addInt.Invoke(calculator, new object[] { 10, 20 })!;
```

Ключевой момент — **перегрузки `Add`**: у класса их две
(`Add(int,int)` и `Add(double,double)`). Просто `GetMethod("Add")` бросил бы
`AmbiguousMatchException`. Решение — второй аргумент `GetMethod`: массив типов
параметров `typeof(int[]{ ... })`, однозначно фиксирующий сигнатуру.

`Invoke` принимает аргументы как `object[]` (упаковка значений) и возвращает
`object?` (распаковка результата в `int`). Чтобы выбрать шагом выше перегрузку,
параметры передаются в строгом порядке типов.

### 3.5 Чтение свойства — контроль результата

```csharp
PropertyInfo resultProp = calcType.GetProperty("Result")!;
Console.WriteLine(resultProp.GetValue(calculator));   // 0
```

`Result` — `int Result => _result;`, т.е. **property без сеттера**. `GetValue`
работает и для read-only свойств — геттер вызывается как `get_Result`.
Значение 0 подтверждает, что экземпляр реально создан и конструктор выполнен.

### 3.6 Приватный метод

```csharp
MethodInfo logMethod = calcType.GetMethod("LogOperation",
    BindingFlags.NonPublic | BindingFlags.Instance)!;
logMethod.Invoke(calculator, new object[] { "Тестовая операция" });
```

Знакомый по Заданию 2 флаг `NonPublic`. Вызов приватного метода извне — то, что
обычный C# не разрешил бы — рефлексия выполняет без проблем.

### 3.7 Статический метод

```csharp
MethodInfo versionMethod = calcType.GetMethod("GetVersion", BindingFlags.Public | BindingFlags.Static)!;
versionMethod.Invoke(null, null)
```

Для статического метода первый аргумент `Invoke` — `null` (нет экземпляра), а
второй — `null` (нет параметров). Возвращённая строка выводится в консоль.

### 3.8 Конструктор с параметрами — два способа

```csharp
// Способ А: фабрика Activator (проще)
object calcWithParam = Activator.CreateInstance(calcType, new object[] { 42 })!;

// Способ Б: «ручной» ConstructorInfo
ConstructorInfo ctor = calcType.GetConstructor(new[] { typeof(int) })!;
object calcViaCtor = ctor.Invoke(new object[] { 100 });
```

`new object[] { 42 }` — параметры конструктора в массиве; CLR подбирает нужный
конструктор по типам (здесь int). `GetConstructor(new[] { typeof(int) })`
возвращает явный `ConstructorInfo` для вызова. Оба пути дают объект с
`Result == 42` и `Result == 100` соответственно.

---

## 4. Полный вывод программы

```
=== ЗАДАНИЕ 3: СОЗДАНИЕ ЭКЗЕМПЛЯРА ЧЕРЕЗ Activator.CreateInstance ===

3.1 Получение типа по имени (позднее связывание):
  Тип найден: DynamicCalculator, FullName: DynamicCalculator

3.2 Создание экземпляра без оператора new (конструктор без параметров):
  [Конструктор DynamicCalculator без параметров]
  Экземпляр создан: DynamicCalculator

3.3 Установка значений свойств через рефлексию:
  Label установлен через SetValue: CalcFromReflection

3.4 Вызов методов через рефлексию:
  Add(10, 20) = 30
  Результат Add(10, 20): 30
  Add(5.5, 3.2) = 8.7
  Результат Add(5.5, 3.2): 8.7
  Multiply(7, 8) = 56
  Результат Multiply(7, 8): 56

3.5 Чтение значений через рефлексию (подтверждение создания):
  Result: 0
  Label:  CalcFromReflection

3.6 Вызов приватного метода:
  [LOG] Выполнена операция: Тестовая операция
  Приватный метод LogOperation вызван успешно

3.7 Вызов статического метода:
  Версия: DynamicCalculator v1.0

3.8 Создание экземпляра через конструктор с параметрами:
  [Конструктор с параметром: начальное значение = 42]
  Result нового экземпляра: 42
  [Конструктор с параметром: начальное значение = 100]
  Через ConstructorInfo.Invoke: Result = 100
```

---

## 5. Типичные ошибки и их профилактика

| Ошибка | Причина | Решение |
|--------|---------|---------|
| `AmbiguousMatchException` | Вызов `GetMethod("Add")` при двух перегрузках | Передать массив типов параметров |
| `MissingMethodException` | Нет конструктора с такими типами | `GetConstructor(new[] { typeof(int) })` |
| `TargetInvocationException` | Ошибка *внутри* вызываемого метода | Смотреть `InnerException` |
| `TypeLoadException` | `Type.GetType("Имя")` в другой сборке | Указать «полное имя, ИмяСборки» |
| `InvalidCastException` | `(double)` из `object` несовместим | Преобразовывать через `Convert` |

---

## 6. Вопросы для защиты

1. **Почему здесь не используется `new`?**
   — `Activator.CreateInstance` создаёт объект по метаданным `Type`, когда тип
   заранее неизвестен (позднее связывание). `new` требует точного
   compile-типа, которого у нас нет.

2. **Чем `CreateInstance(type, args)` отличается от `ConstructorInfo.Invoke`?**
   — Функционально почти ничем: `Activator` внутри сам ищет `ConstructorInfo` и
   вызывает `Invoke`. Разница в удобстве API (одна строка против явного поиска).

3. **Зачем в `GetMethod` передавать массив `typeof(int), typeof(int)`?**
   — Чтобы однозначно выбрать перегрузку `Add(int, int)`, иначе будет
   `AmbiguousMatchException`.

4. **Откуда 8.7 в double-перегрузке?**
   — `Activator`/`Invoke` используют точные типы аргументов (`5.5` и `3.2` —
   `double`), поэтому выбралась перегрузка для `double`, а не для `int`.

5. **Почему код возврата `Invoke` обёрнут `(int)`?**
   — Возврат метода — `object`; реальный сэмпл — `int`. Нужна распаковка
   (unboxing) в переменную конкретного типа.

---

## 7. Выводы

- Тип извлекается строкой (`Type.GetType`) — позднее связывание;
- `Activator.CreateInstance` создаёт объекты без `new` (конструктор без/с параметрами);
- свойства и методы работают через `PropertyInfo`/`MethodInfo` по имени;
- перегрузки выбираются массивами `typeof(...)`, приватные/статические методы
  вызываются флагами и `null`-объектом соответственно;
- тот же механизм будет использован в Заданиях 7–8 для создания плагинов.