using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

// Подписчик на телеметрию симулятора: проверка, что данные дошли до облачного сервера
class Listener
{
    private const string MQTT_BROKER = "test.mosquitto.org";
    private const int MQTT_PORT = 1883;
    private const string DEFAULT_TOPIC = "iot/devices/simulated/data";

    private static int _received = 0;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string topic = args.Length > 0 ? args[0] : DEFAULT_TOPIC;
        int runSeconds = 0;
        if (args.Length > 1 && int.TryParse(args[1], out int s) && s > 0)
            runSeconds = s;

        Console.WriteLine("🛰️ MQTT Listener — проверка доставки в облако");
        Console.WriteLine("================================================");
        Console.WriteLine($"Брокер: {MQTT_BROKER}:{MQTT_PORT}");
        Console.WriteLine($"Топик : {topic}");

        try
        {
            var factory = new MqttFactory();
            IMqttClient client = factory.CreateMqttClient();

            client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate((e) =>
            {
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                int number = Interlocked.Increment(ref _received);

                Console.WriteLine($"\n📥 Сообщение #{number} [{DateTime.Now:HH:mm:ss.fff}]");
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Name == "metadata")
                        {
                            Console.WriteLine("   metadata:");
                            foreach (var m in prop.Value.EnumerateObject())
                                Console.WriteLine($"     {m.Name} = {m.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"   {prop.Name} = {prop.Value}");
                        }
                    }
                }
                catch (JsonException)
                {
                    Console.WriteLine($"   {payload}");
                }
            });

            client.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
            {
                Console.WriteLine($"✅ Подключено к {MQTT_BROKER} — ждём телеметрию...");
            });

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                .WithClientId("Listener_" + Guid.NewGuid().ToString("N").Substring(0, 6))
                .WithCleanSession()
                .Build();

            await client.ConnectAsync(options);
            await client.SubscribeAsync(topic, MqttQualityOfServiceLevel.AtLeastOnce);
            Console.WriteLine($"📬 Подписка на {topic} оформлена (QoS 1)\n");

            // Неинтерактивный запуск: слушаем ограниченное время
            if (runSeconds == 0 && Console.IsInputRedirected)
                runSeconds = 40;

            if (runSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(runSeconds));
            }
            else
            {
                Console.WriteLine("Нажмите Enter для выхода...");
                Console.ReadLine();
            }

            await client.DisconnectAsync();

            Console.WriteLine($"\n================================================");
            Console.WriteLine($"Получено сообщений: {_received}");
            if (_received > 0)
                Console.WriteLine("=== ДОСТАВКА В ОБЛАКО ПОДТВЕРЖДЕНА ===");
            else
                Console.WriteLine("⚠️ Сообщений не получено: запустите симулятор (Task3) в другом терминале");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
