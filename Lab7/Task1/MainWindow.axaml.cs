using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Task1;

public partial class MainWindow : Window
{
    private readonly EnvironmentAwareSensor _sensor = new EnvironmentAwareSensor();
    private readonly DispatcherTimer _timer;
    private readonly string _readingsFile = Path.Combine(AppContext.BaseDirectory, "readings.txt");
    private readonly object _fileLock = new object();
    private int _count = 0;
    private bool _fileNoteShown = false;

    public MainWindow()
    {
        InitializeComponent();

        BtnAdd.Click += (s, e) => AddReading();
        BtnClear.Click += (s, e) =>
        {
            Chart.Data.Clear();
            Chart.Times.Clear();
            _count = 0;
            Chart.InvalidateVisual();
        };

        // Таймер автоматического обновления
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (s, e) => AddReading();
        _timer.Start();

        // Добавляем начальные точки
        for (int i = 0; i < 5; i++)
            AddReading();

        // Запускаем имитацию внешних воздействий
        _ = SimulateEnvironmentalChanges();
    }

    private void AddReading()
    {
        var data = _sensor.ReadData();
        Chart.Data.Add(data.Temperature);
        Chart.Times.Add(data.Timestamp);
        _count++;

        LblTemp.Text = $"Температура: {data.Temperature:F1}°C | Влажность: {data.Humidity}%";

        bool doorOpen = (bool)data.Metadata["doorOpen"];
        bool acOn = (bool)data.Metadata["acOn"];
        bool heaterOn = (bool)data.Metadata["heaterOn"];
        int peopleCount = (int)data.Metadata["peopleCount"];

        string status = $"Дверь: {(doorOpen ? "открыта" : "закрыта")} | ";
        status += $"Кондиционер: {(acOn ? "вкл" : "выкл")} | ";
        status += $"Обогреватель: {(heaterOn ? "вкл" : "выкл")} | ";
        status += $"Людей: {peopleCount}";
        LblStatus.Text = status;

        // Строка состояния температуры и влажности (Задание 1)
        string line = $"[{data.Timestamp:yyyy-MM-dd HH:mm:ss}] Температура: {data.Temperature:F2}°C, " +
                      $"Влажность: {data.Humidity:F1}%, Дверь: {(doorOpen ? "открыта" : "закрыта")}, " +
                      $"Кондиционер: {(acOn ? "вкл" : "выкл")}, Обогреватель: {(heaterOn ? "вкл" : "выкл")}, " +
                      $"Людей: {peopleCount}";
        AppendReading(line);

        // Вывод в консоль (IoT логирование)
        Console.WriteLine($"📊 [{data.Timestamp:HH:mm:ss}] Температура: {data.Temperature:F1}°C, Влажность: {data.Humidity}%");
        Console.WriteLine($"   Статус: {status}");
        Console.WriteLine(new string('-', 60));

        Chart.InvalidateVisual();
    }

    // Сохранение строки состояния в текстовый файл (Задание 1)
    private void AppendReading(string line)
    {
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_readingsFile, line + Environment.NewLine);
            }

            if (!_fileNoteShown)
            {
                Console.WriteLine($"💾 Строки состояния пишутся в файл: {_readingsFile}");
                Console.WriteLine("=== ЗАДАНИЕ 1 ВЫПОЛНЕНО ===");
                _fileNoteShown = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка записи в файл: {ex.Message}");
        }
    }

    // Имитация внешних воздействий (как в IoT)
    private async Task SimulateEnvironmentalChanges()
    {
        await RunLater(TimeSpan.FromSeconds(8),
            () => _sensor.OpenDoor(),
            "  🚪 Дверь открыта!");

        await RunLater(TimeSpan.FromSeconds(12), () =>
        {
            _sensor.TurnOnAC();
            _sensor.CloseDoor();
        }, "  ❄️ Кондиционер включен!");

        await RunLater(TimeSpan.FromSeconds(15), () =>
        {
            _sensor.TurnOnHeater();
            _sensor.TurnOffAC();
        }, "  🔥 Обогреватель включен!");

        await RunLater(TimeSpan.FromSeconds(12), () =>
        {
            _sensor.AddPerson();
            _sensor.AddPerson();
            _sensor.AddPerson();
        }, "  👥 Добавлено 3 человека!");

        await RunLater(TimeSpan.FromSeconds(10),
            () => _sensor.TurnOffHeater(),
            "  🌡️ Обогреватель выключен!");
    }

    private async Task RunLater(TimeSpan delay, Action action, string note)
    {
        await Task.Delay(delay);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            action();
            // Строка события дописывается в статус до следующего показания (как в методичке)
            LblStatus.Text += note;
            Console.WriteLine($"🔔 Событие:{note}");
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
