using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cantwitchconnect.Actions
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum CommandKind
    {
        KillPlayers,
        HealthChange,
        SpawnEntity,
        TossPlayer,
        RtpPlayer,
        SetOnFire,
        ChangeWeather,
        CallChatCommand,
        GiveItem,
        DropInventory,
        ShuffleHotbar,
        LightningStrike
    }
}
