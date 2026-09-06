using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cantwitchconnect.Actions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum WeatherChangeType
    {
        START_RAIN,
        STOP_RAIN
    }
}
