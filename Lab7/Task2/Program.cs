using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Receiving;
using MQTTnet.Protocol;
using System.Text;

class Program
{
    private const string MQTT_BROKER = "broker.hivemq.com";
    private const int MQTT_PORT = 1883;
    private const string MQTT_TOPIC = "test/topic";

    private static int _sentCount = 0;
    private static int _receivedCount = 0;

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("🌡️ MQTT Client Test (v3.x)");
        Console.WriteLine("============================");
        Console.WriteLine($"Брокер: {MQTT_BROKER}:{MQTT_PORT}");
        Console.WriteLine($"Топик : {MQTT_TOPIC}");

        int autoMessages = 5;
        if (args.Length > 0 && int.TryParse(args[0], out int n) && n > 0)
            autoMessages = n;

        try
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MQTTnet.dll");
            if (File.Exists(dllPath))
                Console.WriteLine($"✅ DLL найдена: {dllPath}");
            else
                Console.WriteLine($"❌ DLL НЕ найдена: {dllPath}");

            MqttFactory factory = new MqttFactory();
            MqttClient client = (MqttClient)factory.CreateMqttClient();
            Console.WriteLine("✅ Клиент создан");

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(MQTT_BROKER, MQTT_PORT)
                .WithClientId("TestClient_" + Guid.NewGuid().ToString("N").Substring(0, 6))
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .Build();

            client.ConnectedHandler = new MqttClientConnectedHandlerDelegate((e) =>
            {
                Console.WriteLine("✅ Подключено!");
            });

            client.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate((e) =>
            {
                Console.WriteLine($"❌ Отключено (причина: {e.Reason})");
            });

            client.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate((e) =>
            {
                string msg = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                int number = Interlocked.Increment(ref _receivedCount);
                Console.WriteLine($"📩 [{number}] Получено: {msg}");
            });

            await client.ConnectAsync(options);
            Console.WriteLine("🔗 Соединение установлено");

            await client.SubscribeAsync(MQTT_TOPIC, MqttQualityOfServiceLevel.AtLeastOnce);
            Console.WriteLine($"📬 Подписались на {MQTT_TOPIC} (QoS 1)");

            Console.WriteLine($"\n📤 Отправка {autoMessages} сообщений...");
            for (int i = 1; i <= autoMessages; i++)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(MQTT_TOPIC)
                    .WithPayload($"Сообщение #{i} от TestClient, время {DateTime.Now:HH:mm:ss}")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                var result = await client.PublishAsync(message);
                if (result.ReasonCode == MqttClientPublishReasonCode.Success)
                {
                    _sentCount++;
                    Console.WriteLine($"📤 Отправлено: Сообщение #{i} ({DateTime.Now:HH:mm:ss.fff})");
                }
                else
                {
                    Console.WriteLine($"❌ Ошибка отправки #{i}: {result.ReasonCode}");
                }

                await Task.Delay(700);
            }

            Console.WriteLine("\n⏳ Ожидание эха собственных сообщений (макс. 10 с)...");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (_receivedCount < _sentCount && DateTime.UtcNow < deadline)
                await Task.Delay(200);

            Console.WriteLine($"\n📈 Итого: отправлено {_sentCount}, получено {_receivedCount}");

            bool interactive = !Console.IsInputRedirected;
            if (interactive)
            {
                Console.WriteLine("\n💬 Режим Эксперимента 1: вводите сообщения и нажимайте Enter,");
                Console.WriteLine("   пустая строка (Enter) — выход.\n");

                while (true)
                {
                    string? line = Console.ReadLine();
                    if (line is null || line.Length == 0) break;

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(MQTT_TOPIC)
                        .WithPayload($"Ручное сообщение: {line}")
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    var result = await client.PublishAsync(message);
                    if (result.ReasonCode == MqttClientPublishReasonCode.Success)
                    {
                        _sentCount++;
                        Console.WriteLine($"📤 Отправлено вручную: {line}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\n(неинтерактивный запуск: вход закрыт, завершаем работу)");
            }

            await client.DisconnectAsync();
            Console.WriteLine("🔌 Отключено");

            if (_sentCount > 0 && _receivedCount > 0)
                Console.WriteLine("\n=== ЭКСПЕРИМЕНТ 1 ВЫПОЛНЕН: сообщения отправлены и получены ===");
            else
                Console.WriteLine("\n⚠️ Эксперимент завершён, но эхо не получено — проверьте сеть/брокер");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            Console.WriteLine($"   {ex.StackTrace}");
            Environment.ExitCode = 1;
        }
    }
}
