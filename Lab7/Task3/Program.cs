using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace IoTDeviceSimulator
{
    // ============================================================
    // 1. КЛАССЫ ДАННЫХ
    // ============================================================

    public class SensorData
    {
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    // ============================================================
    // 2. СИМУЛЯТОР ДАТЧИКА
    // ============================================================

    public class EnvironmentAwareSensor
    {
        private readonly Random _random = new Random();
        private double _temperature = 22.0;
        private double _humidity = 60.0;
        private DateTime _lastUpdate = DateTime.UtcNow;

        // Внешние факторы
        private bool _isDoorOpen = false;
        private bool _isHeaterOn = false;
        private bool _isAirConditionerOn = false;
        private bool _isLightOn = false;
        private int _peopleCount = 0;

        public SensorData ReadData()
        {
            var now = DateTime.UtcNow;
            double deltaMinutes = (now - _lastUpdate).TotalMinutes;
            _lastUpdate = now;

            // Ограничиваем дельту, чтобы избежать скачков при паузах
            if (deltaMinutes > 5) deltaMinutes = 0.1;

            // 1. Влияние внешних факторов
            double tempChange = 0.0;
            double humidityChange = 0.0;

            // Влияние открытой двери
            if (_isDoorOpen)
            {
                double outsideTemp = 15.0 + (_random.NextDouble() * 2 - 1) * 3.0;
                tempChange += (outsideTemp - _temperature) * 0.02 * deltaMinutes;
                humidityChange += (40 - _humidity) * 0.01 * deltaMinutes;
            }

            // Влияние обогревателя
            if (_isHeaterOn)
            {
                tempChange += 2.0 * deltaMinutes;
                humidityChange -= 0.5 * deltaMinutes;
            }

            // Влияние кондиционера
            if (_isAirConditionerOn)
            {
                tempChange -= 1.5 * deltaMinutes;
                humidityChange += 0.3 * deltaMinutes;
            }

            // Влияние освещения (лампы немного нагревают помещение)
            if (_isLightOn)
            {
                tempChange += 0.15 * deltaMinutes;
                humidityChange -= 0.02 * deltaMinutes;
            }

            // Влияние людей
            tempChange += _peopleCount * 0.08 * deltaMinutes;
            humidityChange += _peopleCount * 0.05 * deltaMinutes;

            // 2. Естественный дрейф к комнатной температуре (если нет воздействий)
            if (!_isDoorOpen && !_isHeaterOn && !_isAirConditionerOn && !_isLightOn && _peopleCount == 0)
            {
                tempChange += (22.0 - _temperature) * 0.005 * deltaMinutes;
                humidityChange += (60.0 - _humidity) * 0.003 * deltaMinutes;
            }

            // 3. Суточные колебания (синусоида)
            double dailyCycle = 5.0 * Math.Sin(
                2 * Math.PI * (now.Hour + now.Minute / 60.0) / 24.0
            );
            tempChange += (dailyCycle - (_temperature - 22)) * 0.001 * deltaMinutes;

            // 4. Случайный шум
            double noise = (_random.NextDouble() * 2 - 1) * 0.3;
            tempChange += noise * deltaMinutes;
            humidityChange += (_random.NextDouble() * 2 - 1) * 0.2 * deltaMinutes;

            // 5. Применяем изменения
            _temperature += tempChange;
            _humidity += humidityChange;

            // 6. Ограничиваем значения
            _temperature = Math.Clamp(_temperature, -10, 45);
            _humidity = Math.Clamp(_humidity, 15, 95);

            // 7. Освещение: суточный цикл дневного света + искусственный свет
            var local = DateTime.Now;
            double localHour = local.Hour + local.Minute / 60.0 + local.Second / 3600.0;
            double daylightCurve = Math.Max(0, Math.Sin(Math.PI * (localHour - 6.0) / 12.0));
            double daylightLux = 50.0 + 950.0 * daylightCurve;
            double illumination = daylightLux + (_isLightOn ? 350.0 : 0.0);

            return new SensorData
            {
                Temperature = Math.Round(_temperature, 2),
                Humidity = Math.Round(_humidity, 1),
                Timestamp = now,
                Metadata = new Dictionary<string, object>
                {
                    ["isDoorOpen"] = _isDoorOpen,
                    ["isHeaterOn"] = _isHeaterOn,
                    ["isAirConditionerOn"] = _isAirConditionerOn,
                    ["isLightOn"] = _isLightOn,
                    ["peopleCount"] = _peopleCount,
                    ["illumination"] = Math.Round(illumination, 0),
                    ["deltaMinutes"] = Math.Round(deltaMinutes, 2)
                }
            };
        }

        // Методы управления средой
        public void OpenDoor() { _isDoorOpen = true; Console.WriteLine("🚪 Дверь ОТКРЫТА"); }
        public void CloseDoor() { _isDoorOpen = false; Console.WriteLine("🚪 Дверь ЗАКРЫТА"); }
        public void TurnOnHeater() { _isHeaterOn = true; Console.WriteLine("🔥 Обогреватель ВКЛЮЧЕН"); }
        public void TurnOffHeater() { _isHeaterOn = false; Console.WriteLine("🔥 Обогреватель ВЫКЛЮЧЕН"); }
        public void TurnOnAC() { _isAirConditionerOn = true; Console.WriteLine("❄️ Кондиционер ВКЛЮЧЕН"); }
        public void TurnOffAC() { _isAirConditionerOn = false; Console.WriteLine("❄️ Кондиционер ВЫКЛЮЧЕН"); }
        public void TurnOnLight() { _isLightOn = true; Console.WriteLine("💡 Свет ВКЛЮЧЕН"); }
        public void TurnOffLight() { _isLightOn = false; Console.WriteLine("💡 Свет ВЫКЛЮЧЕН"); }
        public void AddPerson() { _peopleCount++; Console.WriteLine($"🧑 Добавлен человек. Всего: {_peopleCount}"); }
        public void RemovePerson() { if (_peopleCount > 0) _peopleCount--; Console.WriteLine($"🧑 Удален человек. Всего: {_peopleCount}"); }
    }

    // ============================================================
    // 3. ОСНОВНАЯ ПРОГРАММА
    // ============================================================

    class Program
    {
        private const string MQTT_BROKER = "test.mosquitto.org";
        private const int MQTT_PORT = 1883;
        private const string DEVICE_ID = "simulated_device_001";
        private const string MQTT_TOPIC = "iot/devices/simulated/data";

        private static IMqttClient? _mqttClient;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Файл на диске с получаемыми данными (ЗАДАНИЕ)
        private static readonly string _dataFile = Path.Combine(AppContext.BaseDirectory, "telemetry.txt");
        private static readonly object _fileLock = new object();
        private static int _fileLines = 0;
        private static bool _fileNoteShown = false;

        // Сигнализация о температуре ниже 22 °C (ЗАДАНИЕ)
        private static bool _wasBelow22 = false;
        private static int _beepCount = 0;

        private static volatile bool _running = true;
        private static int _messagesSent = 0;
        private static int _errorsCount = 0;
        private static int _cycleCount = 0;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            int runSeconds = 0;
            if (args.Length > 0 && int.TryParse(args[0], out int s) && s > 0)
                runSeconds = s;

            Console.WriteLine("🌡️ IoT Simulator v1.0");
            Console.WriteLine("========================================");
            Console.WriteLine("Управление средой (вводите команды):");
            Console.WriteLine("  door    - открыть/закрыть дверь");
            Console.WriteLine("  heater  - включить/выключить обогреватель");
            Console.WriteLine("  ac      - включить/выключить кондиционер");
            Console.WriteLine("  light   - включить/выключить освещение");
            Console.WriteLine("  add     - добавить человека");
            Console.WriteLine("  remove  - убрать человека");
            Console.WriteLine("  status  - показать текущее состояние");
            Console.WriteLine("  exit    - выход");
            Console.WriteLine("========================================");
            Console.WriteLine($"MQTT: {MQTT_BROKER}:{MQTT_PORT}, топик: {MQTT_TOPIC}");
            Console.WriteLine($"Файл данных: {_dataFile}");
            Console.WriteLine("========================================\n");

            var sensor = new EnvironmentAwareSensor();

            await InitializeMqttClient();

            // Неинтерактивный запуск: работаем ограниченное время (20 с по умолчанию)
            if (runSeconds == 0 && Console.IsInputRedirected)
                runSeconds = 20;

            if (runSeconds > 0)
            {
                int seconds = runSeconds;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds));
                    if (_running)
                    {
                        Console.WriteLine($"\n⏱️ Автостоп: программа работала {seconds} с (неинтерактивный запуск)");
                        _running = false;
                    }
                });
            }

            _ = Task.Run(() => ProcessCommands(sensor));

            var lastStatsTime = DateTime.UtcNow;

            while (_running)
            {
                try
                {
                    // 1. Читаем данные с датчика
                    var sensorData = sensor.ReadData();

                    // 2. Формируем телеметрию
                    var telemetry = new
                    {
                        deviceId = DEVICE_ID,
                        temperature = sensorData.Temperature,
                        humidity = sensorData.Humidity,
                        timestamp = sensorData.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        metadata = sensorData.Metadata
                    };

                    string jsonPayload = JsonSerializer.Serialize(telemetry, _jsonOptions);

                    // 3. Выводим получаемые данные в файл на диске (ЗАДАНИЕ)
                    AppendDataFile(jsonPayload);

                    // 4. Гудок, как только температура падает ниже 22 °C (ЗАДАНИЕ)
                    bool below22 = sensorData.Temperature < 22.0;
                    if (below22 && !_wasBelow22)
                        BeepAlarm(sensorData.Temperature);
                    _wasBelow22 = below22;

                    // 5. Отправляем в MQTT
                    if (_mqttClient != null && _mqttClient.IsConnected)
                    {
                        var message = new MqttApplicationMessageBuilder()
                            .WithTopic(MQTT_TOPIC)
                            .WithPayload(Encoding.UTF8.GetBytes(jsonPayload))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .WithRetainFlag(false)
                            .Build();

                        var publishResult = await _mqttClient.PublishAsync(message);

                        if (publishResult.ReasonCode == MqttClientPublishReasonCode.Success)
                        {
                            _messagesSent++;

                            Console.Write($"📊 [{DateTime.Now:HH:mm:ss}] ");
                            DrawTemperatureChart(sensorData.Temperature);

                            Console.WriteLine(string.Format(
                                "   💧 {0:F1}% | 💡 {1} | ☀️ {2:F0} лк | 👥 {3} чел. | 🚪 {4} | 🔥 {5} | ❄️ {6}{7}",
                                sensorData.Humidity,
                                (bool)sensorData.Metadata!["isLightOn"] ? "свет вкл" : "свет выкл",
                                sensorData.Metadata!["illumination"],
                                sensorData.Metadata!["peopleCount"],
                                (bool)sensorData.Metadata["isDoorOpen"]! ? "открыта" : "закрыта",
                                (bool)sensorData.Metadata["isHeaterOn"]! ? "вкл" : "выкл",
                                (bool)sensorData.Metadata["isAirConditionerOn"]! ? "вкл" : "выкл",
                                below22 ? " | 🚨 T < 22 °C!" : ""));
                        }
                        else
                        {
                            _errorsCount++;
                            Console.WriteLine($"❌ Ошибка отправки: {publishResult.ReasonCode}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ MQTT не подключен. Попытка переподключения...");
                        await ReconnectMqttClient();
                    }

                    // 6. Статистика каждые 30 секунд
                    if ((DateTime.UtcNow - lastStatsTime).TotalSeconds >= 30)
                    {
                        Console.WriteLine($"\n📈 Статистика за 30 сек: отправлено {_messagesSent} сообщений, " +
                                         $"ошибок: {_errorsCount}, циклов: {_cycleCount}, гудков: {_beepCount}\n");
                        _messagesSent = 0;
                        _errorsCount = 0;
                        _cycleCount = 0;
                        lastStatsTime = DateTime.UtcNow;
                    }

                    _cycleCount++;

                    // 7. Пауза между измерениями
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
                    _errorsCount++;
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            // ============================================================
            // ЗАВЕРШЕНИЕ
            // ============================================================

            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                try { await _mqttClient.DisconnectAsync(); }
                catch { /* игнорируем ошибки отключения */ }
                Console.WriteLine("🔌 От MQTT-брокера отключены");
            }

            Console.WriteLine($"\n💾 Всего записано строк в файл: {_fileLines} ({_dataFile})");
            Console.WriteLine($"🔔 Количество гудков (T < 22 °C): {_beepCount}");
            Console.WriteLine("=== ЗАДАНИЕ 3 ВЫПОЛНЕНО ===");
        }

        // ============================================================
        // 4. MQTT ФУНКЦИИ
        // ============================================================

        private static async Task InitializeMqttClient()
        {
            try
            {
                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                    .WithClientId(DEVICE_ID)
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

                var options = optionsBuilder.Build();

                _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
                {
                    Console.WriteLine($"✅ Подключено к MQTT брокеру: {MQTT_BROKER}");
                });

                _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate((e) =>
                {
                    Console.WriteLine($"⚠️ Отключено от MQTT. Причина: {e.Reason}");
                });

                await _mqttClient.ConnectAsync(options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка подключения к MQTT: {ex.Message}");
                Console.WriteLine("⚠️ Приложение продолжит работу в автономном режиме");
            }
        }

        private static async Task ReconnectMqttClient()
        {
            if (_mqttClient != null && !_mqttClient.IsConnected)
            {
                try
                {
                    await _mqttClient.ReconnectAsync();
                }
                catch
                {
                    // Игнорируем ошибки переподключения
                }
            }
        }

        // ============================================================
        // 5. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
        // ============================================================

        private static void DrawTemperatureChart(double temperature, double min = -10, double max = 40)
        {
            int width = 40;
            int position = (int)((temperature - min) / (max - min) * width);
            position = Math.Clamp(position, 0, width);

            Console.Write("[");
            Console.Write(new string('░', position));
            Console.ForegroundColor = temperature switch
            {
                > 30 => ConsoleColor.Red,
                > 25 => ConsoleColor.Yellow,
                > 15 => ConsoleColor.Green,
                > 5 => ConsoleColor.Cyan,
                _ => ConsoleColor.Blue
            };
            Console.Write("█");
            Console.ResetColor();
            Console.Write(new string('░', width - position));
            Console.WriteLine($"] {temperature:F1}°C");
        }

        // Запись получаемых данных в файл на диске (ЗАДАНИЕ)
        private static void AppendDataFile(string jsonPayload)
        {
            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(_dataFile, jsonPayload + Environment.NewLine);
                    _fileLines++;
                }

                if (!_fileNoteShown)
                {
                    Console.WriteLine($"💾 Данные пишутся в файл: {_dataFile}");
                    _fileNoteShown = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка записи в файл: {ex.Message}");
                _errorsCount++;
            }
        }

        // Гудок при падении температуры ниже 22 °C (ЗАДАНИЕ)
        private static void BeepAlarm(double temperature)
        {
            _beepCount++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.Write("\a🔔 ГУДОК");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" — температура упала ниже 22 °C ({temperature:F1} °C)! Требуется внимание!");
            Console.ResetColor();

            try
            {
                Console.Beep();
            }
            catch (PlatformNotSupportedException)
            {
                // На Unix символ BEL (\a) выше уже послан в терминал
            }
        }

        private static async Task ProcessCommands(EnvironmentAwareSensor sensor)
        {
            while (_running)
            {
                try
                {
                    string? input = Console.ReadLine();

                    // Поток ввода закрыт (неинтерактивный запуск)
                    if (input is null)
                        return;

                    input = input.ToLower().Trim();
                    if (input.Length == 0) continue;

                    switch (input)
                    {
                        case "door":
                            Console.Write("Открыть или закрыть? (open/close): ");
                            var doorAction = Console.ReadLine()?.ToLower().Trim();
                            if (doorAction == "open") sensor.OpenDoor();
                            else if (doorAction == "close") sensor.CloseDoor();
                            break;

                        case "heater":
                            Console.Write("Включить или выключить? (on/off): ");
                            var heaterAction = Console.ReadLine()?.ToLower().Trim();
                            if (heaterAction == "on") sensor.TurnOnHeater();
                            else if (heaterAction == "off") sensor.TurnOffHeater();
                            break;

                        case "ac":
                            Console.Write("Включить или выключить? (on/off): ");
                            var acAction = Console.ReadLine()?.ToLower().Trim();
                            if (acAction == "on") sensor.TurnOnAC();
                            else if (acAction == "off") sensor.TurnOffAC();
                            break;

                        case "light":
                            Console.Write("Включить или выключить? (on/off): ");
                            var lightAction = Console.ReadLine()?.ToLower().Trim();
                            if (lightAction == "on") sensor.TurnOnLight();
                            else if (lightAction == "off") sensor.TurnOffLight();
                            break;

                        case "add":
                            sensor.AddPerson();
                            break;

                        case "remove":
                            sensor.RemovePerson();
                            break;

                        case "status":
                            var data = sensor.ReadData();
                            Console.WriteLine("\n📊 Текущее состояние:");
                            Console.WriteLine($"   🌡️ Температура: {data.Temperature:F1}°C" +
                                              (data.Temperature < 22.0 ? " 🚨 (ниже 22 °C)" : ""));
                            Console.WriteLine($"   💧 Влажность: {data.Humidity:F1}%");
                            bool isDoorOpen = (bool)data.Metadata!["isDoorOpen"];
                            bool isHeaterOn = (bool)data.Metadata["isHeaterOn"]!;
                            bool isACOn = (bool)data.Metadata["isAirConditionerOn"]!;
                            bool isLightOn = (bool)data.Metadata["isLightOn"]!;

                            Console.WriteLine($"   🚪 Дверь: {(isDoorOpen ? "Открыта" : "Закрыта")}");
                            Console.WriteLine($"   🔥 Обогреватель: {(isHeaterOn ? "Вкл" : "Выкл")}");
                            Console.WriteLine($"   ❄️ Кондиционер: {(isACOn ? "Вкл" : "Выкл")}");
                            Console.WriteLine($"   💡 Свет: {(isLightOn ? "Вкл" : "Выкл")}");
                            Console.WriteLine($"   ☀️ Освещённость: {data.Metadata["illumination"]} лк");
                            Console.WriteLine($"   👥 Людей: {data.Metadata["peopleCount"]}\n");
                            break;

                        case "exit":
                            Console.WriteLine("Выход...");
                            _running = false;
                            return;

                        default:
                            Console.WriteLine("Неизвестная команда. Доступные: door, heater, ac, light, add, remove, status, exit");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в обработчике команд: {ex.Message}");
                }
            }
        }
    }
}
