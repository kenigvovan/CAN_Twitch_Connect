using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace cantwitchconnect.Actions
{
    internal static class PlayerLookup
    {
        // No names configured means "every online player".
        public static IEnumerable<IPlayer> FindOnline(ICoreServerAPI sapi, string[] names)
        {
            var online = sapi.World.AllOnlinePlayers;
            for (int i = 0; i < online.Length; i++)
            {
                var p = online[i];
                if (names == null || names.Length == 0)
                {
                    yield return p;
                    continue;
                }
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(p.PlayerName, names[j], System.StringComparison.OrdinalIgnoreCase))
                    {
                        yield return p;
                        break;
                    }
                }
            }
        }
    }
}
