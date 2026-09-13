using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Task7.Plugins;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== ЗАДАНИЕ 7: СИСТЕМА ДИНАМИЧЕСКОЙ ЗАГРУЗКИ ПЛАГИНОВ ===\n");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string pluginsDir = Path.Combine(baseDir, "Plugins");
        string pluginDll = Path.Combine(pluginsDir, "Task7.Plugins.dll");
        Console.WriteLine($"Каталог плагинов: {pluginsDir}");
        Console.WriteLine($"DLL плагинов:     {Path.GetFileName(pluginDll)}");

        if (!File.Exists(pluginDll))
        {
            Console.WriteLine("\nОшибка: DLL плагинов не найдена.");
            return;
        }

        Console.WriteLine("\n1. Загрузка сборки и сканирование типов (Assembly.GetTypes + GetCustomAttribute):");
        Assembly assembly = Assembly.LoadFrom(pluginDll);
        Type[] types = assembly.GetTypes();
        Type pluginInterface = typeof(IPlugin);

        var candidates = types
            .Where(t => pluginInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToArray();

        Console.WriteLine($"  Всего типов в сборке: {types.Length}");
        foreach (Type t in types)
        {
            var attr = t.GetCustomAttribute<PluginAttribute>();
            bool isPlugin = candidates.Contains(t);
            string msg = isPlugin ? "-> плагин (реализует IPlugin)" : "";
            Console.WriteLine($"    {t.FullName} [атрибут: {attr?.Name ?? "нет"}] {msg}");
        }

        Console.WriteLine("\n2. Создание экземпляров плагинов через Activator.CreateInstance:");
        foreach (Type t in candidates)
        {
            IPlugin plugin = (IPlugin)Activator.CreateInstance(t)!;
            Console.WriteLine($"    Создан плагин: {plugin.Name}");
            plugin.Execute();
        }

        Console.WriteLine("\n3. Загрузка плагинов по имени из конфигурационного файла (plugins.config):");
        string configPath = Path.Combine(baseDir, "plugins.config");
        string[] configLines = File
            .ReadAllLines(configPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        Console.WriteLine($"  Конфигурация: {Path.GetFileName(configPath)}");
        foreach (string line in configLines)
        {
            Type? pluginType = Type.GetType(line, a => assembly, null);
            if (pluginType == null)
            {
                Console.WriteLine($"    {line} -> тип не найден");
                continue;
            }
            if (!pluginInterface.IsAssignableFrom(pluginType) || pluginType.IsInterface || pluginType.IsAbstract)
            {
                Console.WriteLine($"    {line} -> не является плагином");
                continue;
            }
            IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
            Console.WriteLine($"    {line} -> загружен: {plugin.Name}");
            plugin.Execute();
        }

        Console.WriteLine("\n4. Вызов метода плагина через рефлексию (поиск метода):");
        Type calculatorType = assembly.GetType("Task7.Plugins.CalculatorPlugin")!;
        IPlugin calculator = (IPlugin)Activator.CreateInstance(calculatorType)!;
        MethodInfo addMethod = calculator.GetType().GetMethod("Add")!;
        object sum = addMethod.Invoke(calculator, new object[] { 5.0, 3.0 })!;
        Console.WriteLine($"    Add(5.0, 3.0) = {sum}");

        Console.WriteLine("\n5. Гибкость системы:");
        Console.WriteLine("  Добавление нового плагина НЕ требует изменения основного кода:");
        Console.WriteLine("    1) собрать новую DLL с классом, реализующим IPlugin;");
        Console.WriteLine("    2) скопировать её в каталог Plugins;");
        Console.WriteLine("    3) добавить строку с именем типа в plugins.config.");
        Console.WriteLine("  Программа пересобирается без правки Program.cs.");
    }
}