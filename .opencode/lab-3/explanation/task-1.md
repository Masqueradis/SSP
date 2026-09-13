# Задание 1 — Класс с приватными полями и валидацией

**Файл:** `Lab3/Task1/Program.cs` · **Платформа:** .NET 10.0
**Статус сборки/запуска:** 0 Warning(s) / 0 Error(s)

---

## 1. Что изучаем и зачем это нужно

Рефлексия имеет смысл только тогда, когда есть «объект исследования» — класс
с содержательным внутренним устройством. В этом задании такой класс (`Product`)
создаётся с соблюдением двух требований условия:

> 1. класс должен содержать **не менее 3 приватных полей разных типов**;
> 2. для каждого поля должно быть **публичное свойство**, осуществляющее
>    **проверку корректности значения** (валидацию).

Именно приватные поля — «сырьё» для последующих заданий: в Задании 4 мы
«подсмотрим» их через рефлексию, в Задании 5 изменим их значение через
`FieldInfo.SetValue`, а в Задании 2 разберём по методам. Приватность и
валидация делают демонстрацию честной: снаружи данные защищены и проверяются
этикетом, но рефлексия видит всё.

---

## 2. Полный код программы

```csharp
using System;

public class Product
{
    private int _id;          // приватное поле 1 — int
    private string _name;     // приватное поле 2 — string
    private double _price;    // приватное поле 3 — double
    private string _category; // приватное поле 4 — string

    public static int TotalProducts = 0;   // публичное статическое поле-счётчик

    // ---- Свойства с валидацией ----
    public int Id
    {
        get => _id;
        set
        {
            if (value <= 0) throw new ArgumentException("Id должен быть больше 0");
            _id = value;
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name не может быть пустым");
            _name = value;
        }
    }

    public double Price
    {
        get => _price;
        set
        {
            if (value < 0) throw new ArgumentException("Price не может быть отрицательным");
            _price = value;
        }
    }

    public string Category
    {
        get => _category;
        private set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Категория не может быть пустой");
            _category = value;
        }
    }

    // Конструктор по умолчанию
    public Product()
    {
        _id = 1; _name = "Unknown"; _price = 0; _category = "Default";
        TotalProducts++;
    }

    // Параметризованный конструктор с проверкой аргументов до присваивания
    public Product(int id, string name, double price, string category = "Default")
    {
        if (id <= 0) throw new ArgumentException("Id должен быть больше 0");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name не может быть пустым");
        if (price < 0) throw new ArgumentException("Price не может быть отрицательным");
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Категория не может быть пустой");

        _id = id;
        _name = name;
        _price = price;
        _category = category;
        TotalProducts++;
    }

    public void DisplayInfo()
    {
        Console.WriteLine($"Product: {_name}, Price: {_price}, Category: {_category}");
    }

    public double ApplyDiscount(double percent)
    {
        return _price * (1 - percent / 100);
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
        Console.WriteLine("=== ЗАДАНИЕ 1: КЛАСС С ПРИВАТНЫМИ ПОЛЯМИ И ВАЛИДАЦИЕЙ ===\n");

        Console.WriteLine("1.1 Создание объекта с корректными данными:");
        var product = new Product(1, "Laptop", 1000.0, "Electronics");
        product.DisplayInfo();
        Console.WriteLine($"  Цена со скидкой 15%: {product.ApplyDiscount(15):F2}");
        Console.WriteLine($"  Всего создано объектов: {Product.GetTotalCount()}");

        Console.WriteLine("\n1.2 Валидация свойств (некорректные значения):");
        TryInvalid(() => product.Price = -500);
        TryInvalid(() => product.Name = "");
        TryInvalid(() => product.Id = 0);

        Console.WriteLine("\n1.3 Корректная установка значений через свойства:");
        product.Price = 1500;
        product.Name = "Gaming Laptop";
        product.Id = 42;
        product.DisplayInfo();

        var second = new Product(2, "Mouse", 25.5, "Accessories");
        Console.WriteLine($"  Всего создано объектов: {Product.GetTotalCount()}");

        Console.WriteLine("\nПриватные поля класса (перечислены через рефлексию):");
        foreach (var field in typeof(Product).GetFields(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance))
        {
            Console.WriteLine($"  {field.FieldType.Name} {field.Name}");
        }
    }

    static void TryInvalid(Action action)
    {
        try
        {
            action();
            Console.WriteLine("  Исключение не возникло (валидация пропущена)");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  Исключение: {ex.Message}");
        }
    }
}
```

---

## 3. Разбор по блокам

### 3.1 Приватные поля и публичные свойства

Четыре поля разных типов:

| Поле | Тип | Свойство | Правило валидации |
|------|-----|----------|-------------------|
| `_id` | `int` | `Id` | `> 0` |
| `_name` | `string` | `Name` | не пусто / не пробелы |
| `_price` | `double` | `Price` | `>= 0` |
| `_category` | `string` | `Category` | не пусто |

Свойство — это **пара IL-методов** `get_Имя`/`set_Имя` (это станет важно в
Задании 2, когда мы увидим их в списке `GetMethods`). Валидация находится в
сеттере `set`, поэтому любое нарушение корректности ловится сразу при
присваивании.

Обратите внимание: свойство `Category` имеет сеттер с модификатором `private`.
Это сделано намеренно — в Задании 2 будет видно, что приватный аксессор
`set_Category` появляется в списке методов только при флаге `NonPublic`.

### 3.2 Конструктор как второй «пояс валидации»

В параметризованном конструкторе проверки продублированы **до** присваивания
в поля. Это важный принцип инвариантов: объект должен быть корректным уже в
момент создания, а не только после «ручной» установки свойств.

Статическое поле `TotalProducts` (счётчик созданных объектов) демонстрирует
чёчет два момента: реальная логика (счёт) и статический член, который в
Задании 2 мы найдём методом `GetTotalCount()`.

### 3.3 Валидация: helper `TryInvalid`

```csharp
static void TryInvalid(Action action)
{
    try { action(); Console.WriteLine("  Исключение не возникло (валидация пропущена)"); }
    catch (ArgumentException ex) { Console.WriteLine($"  Исключение: {ex.Message}"); }
}
```

Лямбда-выражения вида `() => product.Price = -500` передают **само присваивание**
как делегат `Action`. Так внутри одного helper-метода иллюстрируются сразу три
попытки нарушения валидации, а в консоль выводится текст исключения, заданный
в сеттере. Это пример того, как лямбды упрощают тест-код.

### 3.4 Приватные поля «глазами» рефлексии

```csharp
typeof(Product).GetFields(
    System.Reflection.BindingFlags.NonPublic |
    System.Reflection.BindingFlags.Instance)
```

`BindingFlags.NonPublic | Instance` — единственный способ «увидеть» приватные
поля метаданными. Без этих флагов `GetFields()` вернул бы только публичные члены
(`TotalProducts`). На выходе получаем 4 поля — они же станут «мишенями»
в Задании 5.

---

## 4. Полный вывод программы

```
=== ЗАДАНИЕ 1: КЛАСС С ПРИВАТНЫМИ ПОЛЯМИ И ВАЛИДАЦИЕЙ ===

1.1 Создание объекта с корректными данными:
Product: Laptop, Price: 1000, Category: Electronics
  Цена со скидкой 15%: 850.00
  Всего создано объектов: 1

1.2 Валидация свойств (некорректные значения):
  Исключение: Price не может быть отрицательным
  Исключение: Name не может быть пустым
  Исключение: Id должен быть больше 0

1.3 Корректная установка значений через свойства:
Product: Gaming Laptop, Price: 1500, Category: Electronics
  Всего создано объектов: 2

Приватные поля класса (перечислены через рефлексию):
  Int32 _id
  String _name
  Double _price
  String _category
```

---

## 5. Что спросят/о чем может спросить преподаватель

1. **Почему проверка в конструкторе и в сеттере дублируется?**
   — Потому что объект может быть создан конструктором (проверка до присваивания)
   и изменён позже через свойства (проверка при каждом `set`). Если убрать
   проверку из конструктора, удастся создать «недопустимый» объект, минуя сеттер.

2. **Что такое свойство с точки зрения CLR?**
   — Пара методов `get_X`/`set_X` + метаданные свойства; на уровне IL никаких
   «полей» у свойства нет. Поэтому рефлексией свойство ищется через
   `GetProperty()`, а не `GetField()`.

3. **Зачем `IsNullOrWhiteSpace`, а не сравнение со `""`?**
   — Он отсекает и `""`, и строку из пробелов — более строгое «пустое значение».

4. **Почему поля именно private?**
   — Это требование задания 1 и «сырьё» для заданий 4–5: мы нарочно прячем
   состояние, чтобы продемонстрировать, как рефлексия его обходит.

---

## 6. Выводы

- Создан «исследуемый» класс: 4 приватных поля разных типов, публичные свойства
  с валидацией, защищённые инварианты.
- Корректные значения проходят, некорректные — бросают `ArgumentException`.
- Через `GetFields(NonPublic | Instance)` приватные поля перечисляются списком —
  мостик к заданиям 4–5.