using System;

public class Product
{
    private int _id;
    private string _name;
    private double _price;
    private string _category;

    public static int TotalProducts = 0;

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

    public Product()
    {
        _id = 1;
        _name = "Unknown";
        _price = 0;
        _category = "Default";
        TotalProducts++;
    }

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