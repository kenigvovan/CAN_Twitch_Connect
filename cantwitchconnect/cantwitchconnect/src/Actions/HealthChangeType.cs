using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cantwitchconnect.Actions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HealthChangeType
    {
        RESTORE_FULL,
        RESTORE_HALF,
        REMOVE_HALF,
        SET_HALF
    }
}
