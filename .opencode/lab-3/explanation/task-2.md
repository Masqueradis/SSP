# Задание 2 — Инспекция методов класса через рефлексию

**Файл:** `Lab3/Task2/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем

Ключевой инструмент инспекции — `Type.GetMethods()` с разными наборами
`BindingFlags`. Задание требует:

> показать методы класса с их **именем, возвращаемым типом, модификаторами
> доступа и параметрами**; продемонстрировать **разницу наборов флагов** и
> **фильтрацию** методов, которые компилятор сгенерировал для свойств
> (`get_...`/`set_...`).

Дополнительно со списка видно, какие методы унаследованы от `object`, какие
являются синтетическими, а какие объявлены самим классом.

---

## 2. Полный код программы

```csharp
using System;
using System.Linq;
using System.Reflection;

public class Product
{
    private int _id;
    private string _name;
    protected double _price;
    public static int TotalProducts = 0;

    public int Id { get; set; }
    public string Name { get; set; }
    public double Price { get; set; }
    public string Category { get; private set; }

    public Product() { /* ... */ }
    public Product(int id, string name, double price) { /* ... */ }

    public void DisplayInfo()
    {
        Console.WriteLine($"Product: {_name}, Price: {_price}");
    }

    public double ApplyDiscount(double percent)
    {
        return _price * (1 - percent / 100);
    }

    private void ValidatePrice()
    {
        if (_price < 0) _price = 0;
    }

    protected void UpdateCategory(string newCategory)
    {
        Category = newCategory;
    }

    public static int GetTotalCount()
    {
        return TotalProducts;
    }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 2: ИНСПЕКЦИЯ МЕТОДОВ КЛАССА ЧЕРЕЗ РЕФЛЕКСИЮ ===\n");

        Type type = typeof(Product);

        Console.WriteLine("2.1 Публичные методы (BindingFlags.Public | Instance):");
        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        PrintMethods(publicMethods, "включая унаследованные от object");

        Console.WriteLine("\n2.2 Все методы (Public | NonPublic | Instance | Static):");
        var allMethods = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        PrintMethods(allMethods, "публичные, приватные, защищенные, статические, экземплярные");

        Console.WriteLine("\n2.3 Методы без аксессоров свойств (фильтр get_/set_):");
        var withoutAccessors = allMethods
            .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
            .ToArray();
        PrintMethods(withoutAccessors, "методы, не сгенерированные для свойств");

        Console.WriteLine("\n2.4 Методы, сгенерированные для свойств (демонстрация фильтра):");
        foreach (var m in allMethods.Where(m => m.Name.StartsWith("get_") || m.Name.StartsWith("set_")))
        {
            Console.WriteLine($"  синтетический аксессор свойства: {m.Name}");
        }

        Console.WriteLine("\n2.5 Сравнение наборов флагов:");
        Console.WriteLine($"  BindingFlags.Public | Instance:                {publicMethods.Length}");
        Console.WriteLine($"  Public | NonPublic | Instance | Static:        {allMethods.Length}");
        Console.WriteLine($"  После отсева get_/set_:                       {withoutAccessors.Length}");
        Console.WriteLine("  Вывод: без NonPublic+Static видна только часть методов;");

        Console.WriteLine("\n2.6 Ссылки на конкретные методы:");
        MethodInfo? displayMethod = type.GetMethod("DisplayInfo");
        MethodInfo? discountMethod = type.GetMethod("ApplyDiscount");
        MethodInfo? validateMethod = type.GetMethod("ValidatePrice", BindingFlags.NonPublic | BindingFlags.Instance);
        Console.WriteLine($"  DisplayInfo:     {(displayMethod != null ? "найден" : "null")}");
        Console.WriteLine($"  ApplyDiscount:   {(discountMethod != null ? "найден" : "null")}");
        Console.WriteLine($"  ValidatePrice:   {(validateMethod != null ? "найден (приватный)" : "null")}");
    }

    static void PrintMethods(MethodInfo[] methods, string note)
    {
        Console.WriteLine($"  ({methods.Length} методов, {note}):");
        foreach (MethodInfo m in methods)
        {
            string visibility = m.IsPublic ? "public"
                              : m.IsPrivate ? "private"
                              : m.IsFamily ? "protected"
                              : m.IsAssembly ? "internal"
                              : "protected internal";
            string modifiers = "";
            if (m.IsStatic) modifiers += " static";
            if (m.IsAbstract) modifiers += " abstract";
            if (m.IsVirtual && !m.IsFinal) modifiers += " virtual";
            var parameters = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"  {visibility} {m.ReturnType.Name} {m.Name}({parameters}){modifiers}");
        }
    }
}
```

---

## 3. Разбор по блокам

### 3.1 `GetMethods(Public | Instance)` — что попало в список (13 методов)

`BindingFlags.Public | Instance` — «всем привычный» публичный экземплярный API.
В списке видим:
- аксессоры всех четырёх свойств: `get_Id`, `set_Id`, `get_Name`, `set_Name`,
  `get_Price`, `set_Price`, `get_Category`;
- собственные методы `DisplayInfo`, `ApplyDiscount`;
- четыре унаследованных от `object`: `GetType`, `ToString`, `Equals`, `GetHashCode`.

**Важно:** сам класс объявления methods — это только 2 «настоящих» метода + 7
аксессоров; остальное добавляет базовый тип. Рефлексия всегда включает
унаследованные члены, если не указан флаг `DeclaredOnly`.

### 3.2 `GetMethods(Public | NonPublic | Instance | Static)` — все 19

Добавление `NonPublic` и `Static` открывает «скрытую» часть класса:

```
private Void set_Category(String value)      // приватный сеттер Category
private Void ValidatePrice()                 // приватный метод
protected Void UpdateCategory(String)        // защищённый метод
public Int32 GetTotalCount() static          // публичный статический метод
protected Void Finalize() virtual            // деструктор (object)
protected internal Object MemberwiseClone()  // служебный (object)
```

Обратите внимание на различие модификаторов в выводе:
- `private set_Category` — поскольку `Category` объявлено как `{ get; private set; }`;
- `protected internal MemberwiseClone` — связка двух флагов `IsFamily OR IsAssembly`;
- `Finalize` помечен `virtual` (в `object` он виртуальный).

Логика форматирования в `PrintMethods` считывает их из битовых свойств
`MethodInfo`: `IsPublic`, `IsPrivate`, `IsFamily`, `IsAssembly`, `IsStatic`,
`IsAbstract`, `IsVirtual`.

### 3.3 Фильтр `get_`/`set_` (11 методов)

```csharp
allMethods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
```

Компилятор C# генерирует для свойства `X` пару IL-методов `get_X` и `set_X`,
и `MethodInfo.Name` у них начинается с `get_`/`set_`. Отсев по префиксу оставляет
**11 «настоящих» методов** — весь исполняемый методэмулляр класса без
«синтетики» свойств. Это стандартный приём, используемый в дескомпиляторах,
документирующих инструментах и профилировщиках.

### 3.4 Сравнение наборов флагов (вывод)

```
BindingFlags.Public | Instance:                13
Public | NonPublic | Instance | Static:        19
После отсева get_/set_:                       11
```

Разница 19 − 11 = 8 — это ровно аксессоры свойств (4 get + 4 set).
Разница 19 − 13 = 6 — «скрытая» часть: `set_Category`, `ValidatePrice`,
`UpdateCategory`, `GetTotalCount`, `Finalize`, `MemberwiseClone`.

### 3.5 `GetMethod(name, flags)` — точечный поиск

- `GetMethod("DisplayInfo")` — найдется (public, флаги по умолчанию видят его);
- `GetMethod("ValidatePrice")` **без** флагов — вернёт `null`: приватные методы
  исключены из поиска по умолчанию;
- `GetMethod("ValidatePrice", NonPublic | Instance)` — найден.

Вывод `ValidatePrice:   найден (приватный)` служит наглядным доказательством
роли флага `NonPublic`.

---

## 4. Полный вывод программы

```
=== ЗАДАНИЕ 2: ИНСПЕКЦИЯ МЕТОДОВ КЛАССА ЧЕРЕЗ РЕФЛЕКСИЮ ===

2.1 Публичные методы (BindingFlags.Public | Instance):
  (13 методов, включая унаследованные от object):
  public Int32 get_Id()
  public Void set_Id(Int32 value)
  public String get_Name()
  public Void set_Name(String value)
  public Double get_Price()
  public Void set_Price(Double value)
  public String get_Category()
  public Void DisplayInfo()
  public Double ApplyDiscount(Double percent)
  public Type GetType()
  public String ToString() virtual
  public Boolean Equals(Object obj) virtual
  public Int32 GetHashCode() virtual

2.2 Все методы (Public | NonPublic | Instance | Static):
  (19 методов, публичные, приватные, защищенные, статические, экземплярные):
  public Int32 get_Id()
  public Void set_Id(Int32 value)
  public String get_Name()
  public Void set_Name(String value)
  public Double get_Price()
  public Void set_Price(Double value)
  public String get_Category()
  private Void set_Category(String value)
  public Void DisplayInfo()
  public Double ApplyDiscount(Double percent)
  private Void ValidatePrice()
  protected Void UpdateCategory(String newCategory)
  public Int32 GetTotalCount() static
  public Type GetType()
  protected internal Object MemberwiseClone()
  protected Void Finalize() virtual
  public String ToString() virtual
  public Boolean Equals(Object obj) virtual
  public Int32 GetHashCode() virtual

2.3 Методы без аксессоров свойств (фильтр get_/set_):
  (11 методов, методы, не сгенерированные для свойств):
  public Void DisplayInfo()
  public Double ApplyDiscount(Double percent)
  private Void ValidatePrice()
  protected Void UpdateCategory(String newCategory)
  public Int32 GetTotalCount() static
  public Type GetType()
  protected internal Object MemberwiseClone()
  protected Void Finalize() virtual
  public String ToString() virtual
  public Boolean Equals(Object obj) virtual
  public Int32 GetHashCode() virtual

2.4 Методы, сгенерированные для свойств (демонстрация фильтра):
  синтетический аксессор свойства: get_Id
  синтетический аксессор свойства: set_Id
  синтетический аксессор свойства: get_Name
  синтетический аксессор свойства: set_Name
  синтетический аксессор свойства: get_Price
  синтетический аксессор свойства: set_Price
  синтетический аксессор свойства: get_Category
  синтетический аксессор свойства: set_Category

2.5 Сравнение наборов флагов:
  BindingFlags.Public | Instance:                13
  Public | NonPublic | Instance | Static:        19
  После отсева get_/set_:                       11
  Вывод: без NonPublic+Static видна только часть методов;

2.6 Ссылки на конкретные методы:
  DisplayInfo:     найден
  ApplyDiscount:   найден
  ValidatePrice:   найден (приватный)
```

---

## 5. Вопросы для защиты

1. **Почему в списке 13 методов, хотя в классе объявлено меньше?**
   — Рефлексия включает унаследованные члены (`object`: `GetType`, `ToString`,
   `Equals`, `GetHashCode`, `Finalize`, `MemberwiseClone`). Флаг `DeclaredOnly`
   ограничил бы список только объявленными в самом типе.

2. **Как отличить метод, сгенерированный для свойства?**
   — По имени: префиксы `get_` и `set_`. Это соглашение, используемое компилятором.

3. **Что такое `Finalize` и `MemberwiseClone`?**
   — Служебные методы базового класса `object`: финализатор (деструктор с точки
   зрения сборщика мусора) и «дешёвое» клонирование соответственно.

4. **Зачем `IsVirtual && !IsFinal` для пометки виртуального метода?**
   — `MethodInfo.IsVirtual` равно `true` у любого virtual/override, включая `sealed
   override` (у `Finalize` после деструминга). Чтобы показать именно «обычные
   виртуальные», мы отсекаем финальные (`IsFinal`).

5. **Что произойдёт с `GetMethod("ValidatePrice")` без флагов?**
   — Вернёт `null`: приватные методы по умолчанию исключены из поиска.

---

## 6. Выводы

- `GetMethods` + `BindingFlags` даёт исчерпывающую картину методаэмлюляру типа;
- флаг `NonPublic` включает приватные/защищённые/internal-методы;
- подход к форматированию читается из битовых свойств `MethodInfo`
  (`IsPublic`, `IsStatic`, `IsVirtual` и т.д.);
- фильтр по `get_/set_` избавляет от «шума» синтетических аксессоров.