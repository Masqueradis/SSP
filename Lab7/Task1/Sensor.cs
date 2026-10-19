namespace Task1;

// Класс датчика с учетом окружающей среды
public class EnvironmentAwareSensor
{
    private readonly Random _random = new Random();
    private double _baseTemperature = 22.0;
    private readonly double _noiseAmplitude = 3.0;
    private bool _doorOpen = false;
    private bool _acOn = false;
    private bool _heaterOn = false;
    private int _peopleCount = 0;

    public SensorData ReadData()
    {
        // Базовые показания
        double noise = (_random.NextDouble() * 2 - 1) * _noiseAmplitude;
        double temperature = _baseTemperature + noise;
        double humidity = 60 + (temperature - _baseTemperature) * 2 + (_random.NextDouble() * 2 - 1) * 5;

        // Влияние внешних факторов
        if (_doorOpen) temperature += 3.0;
        if (_acOn) temperature -= 2.0;
        if (_heaterOn) temperature += 4.0;
        temperature += _peopleCount * 0.5;

        return new SensorData
        {
            Temperature = Math.Round(temperature, 2),
            Humidity = Math.Round(humidity, 1),
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["doorOpen"] = _doorOpen,
                ["acOn"] = _acOn,
                ["heaterOn"] = _heaterOn,
                ["peopleCount"] = _peopleCount
            }
        };
    }

    public void OpenDoor() { _doorOpen = true; }
    public void CloseDoor() { _doorOpen = false; }
    public void TurnOnAC() { _acOn = true; }
    public void TurnOffAC() { _acOn = false; }
    public void TurnOnHeater() { _heaterOn = true; }
    public void TurnOffHeater() { _heaterOn = false; }
    public void AddPerson() { _peopleCount++; }
    public void RemovePerson() { if (_peopleCount > 0) _peopleCount--; }
}

public class SensorData
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
