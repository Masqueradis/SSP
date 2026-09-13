using System;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 8 (ДОПОЛНИТЕЛЬНО): СИСТЕМА ПЛАГИНОВ PluginManager ===\n");

        var manager = new PluginManager();
        Console.WriteLine($"Каталог плагинов: {manager.PluginsPath}");

        Console.WriteLine("\n1. Загрузка всех плагинов из каталога Plugins:");
        manager.LoadPlugins();

        Console.WriteLine("\n2. Список загруженных плагинов:");
        foreach (var plugin in manager.GetPlugins())
        {
            Console.WriteLine($"  {plugin.Name} v{plugin.Version}");
        }

        Console.WriteLine("\n3. Получение плагина по имени (GetPlugin):");
        IPlugin? calc = manager.GetPlugin("calculator");
        if (calc != null)
        {
            Console.WriteLine($"  Найден: {calc.Name} v{calc.Version}");
            calc.Execute();
        }

        IPlugin? logger = manager.GetPlugin("Logger");
        if (logger != null)
        {
            Console.WriteLine($"  Найден: {logger.Name} v{logger.Version}");
            logger.Execute();
            if (logger is IParameterizedPlugin parameterized)
            {
                parameterized.ExecuteWithParams(new Dictionary<string, object> { ["message"] = "Привет из LoggerPlugin" });
            }
        }

        Console.WriteLine("\n4. Вызов метода плагина через рефлексию (ExecutePluginMethod):");
        if (calc != null)
        {
            manager.ExecutePluginMethod(calc, "Add", new object[] { 5.5, 4.5 });
            double sum = manager.ExecutePluginMethod<double>(calc, "Add", new object[] { 10.0, 20.0 });
            Console.WriteLine($"  Обобщённый вызов ExecutePluginMethod<double> -> {sum}");

            manager.ExecutePluginMethod(calc, "Divide", new object[] { 10.0, 2.0 });
        }

        Console.WriteLine("\n5. Гибкость: новый плагин добавляется отдельной DLL в каталог Plugins,");
        Console.WriteLine("   основной код (PluginManager и Program) не изменяется.");
    }
}