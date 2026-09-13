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

    public Product()
    {
        _id = 0;
        _name = "Unknown";
        _price = 0;
        Name = "Unknown";
        Category = "Default";
        TotalProducts++;
    }

    public Product(int id, string name, double price)
    {
        _id = id;
        _name = name;
        _price = price;
        Id = id;
        Name = name;
        Price = price;
        Category = "Default";
        TotalProducts++;
    }

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
            string visibility = m.IsPublic ? "public" : m.IsPrivate ? "private" : m.IsFamily ? "protected" : m.IsAssembly ? "internal" : "protected internal";
            string modifiers = "";
            if (m.IsStatic) modifiers += " static";
            if (m.IsAbstract) modifiers += " abstract";
            if (m.IsVirtual && !m.IsFinal) modifiers += " virtual";
            var parameters = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"  {visibility} {m.ReturnType.Name} {m.Name}({parameters}){modifiers}");
        }
    }
}