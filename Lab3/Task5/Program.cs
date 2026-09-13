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