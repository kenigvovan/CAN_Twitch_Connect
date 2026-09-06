using System;
using System.Collections.Generic;
using cantwitchconnect.Actions;
using cantwitchconnect.Voting;

namespace cantwitchconnect
{
    public class Config
    {
        public string AccessCode;
        public string Channel;
        public string ClientId;
        public string ClientSecret;
        public string AccessToken;
        public string RefreshToken;
        public DateTime TokenExpiry;
        public string RedirectUri = "http://localhost";

        public int GlobalCooldownSeconds = 60;
        public bool AnnounceIngame = true;

        // Runs the poll system without connecting to Twitch, for local testing.
        public bool OfflineDebug;
        public float ActionDelaySeconds = 3f;
        public List<string> VoterWhitelist;
        public List<string> VoterBlacklist;

        // Fallback targets for commands without PlayerNames. Empty = all online players.
        public List<string> DefaultPlayerNames = new();

        // How long a weather override stays forced. 0 = never reset.
        public int WeatherOverrideSeconds = 600;

        public List<CommandConfig> Commands;

        public void InitConfig()
        {
            AccessCode = "";
            Channel = "";
            ClientId = "";
            ClientSecret = "";
            RedirectUri = "http://localhost";

            Commands = new List<CommandConfig>
            {
                new()
                {
                    Name = "kill",
                    Description = "Should the streamer meet their end?",
                    Kind = CommandKind.KillPlayers,
                    Answers = new List<AnswerInfo>
                    {
                        new("killthem",  "cantwitchconnect:killthem",  new[] { "kill", "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_Death.jpg"),
                        new("sparethem", "cantwitchconnect:sparethem", new[] { "spare", "1", "-", "no" }, "cantwitchconnect:textures/Cups02.jpg")
                    }
                },
                new()
                {
                    Name = "fullhealth",
                    Description = "Restore the streamer to full health?",
                    Kind = CommandKind.HealthChange,
                    HealthChangeType = HealthChangeType.RESTORE_FULL,
                    Answers = new List<AnswerInfo>
                    {
                        new("heal",  "cantwitchconnect:healthem", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg"),
                        new("letbe", "cantwitchconnect:letbe",    new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "halfhealth",
                    Description = "Cut the streamer's health in half?",
                    Kind = CommandKind.HealthChange,
                    HealthChangeType = HealthChangeType.SET_HALF,
                    Answers = new List<AnswerInfo>
                    {
                        new("halve", "cantwitchconnect:halvehealth", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Swords02.jpg"),
                        new("letbe", "cantwitchconnect:letbe",       new[] { "1", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg")
                    }
                },
                new()
                {
                    Name = "wolves",
                    Description = "How many wolves should join the hunt?",
                    Kind = CommandKind.SpawnEntity,
                    EntityCodes = new List<string> { "game:wolf-eurasian-adult-male", "game:wolf-eurasian-adult-female" },
                    Answers = new List<AnswerInfo>
                    {
                        new("dontspawn", "cantwitchconnect:dontspawnwolves", new[] { "0", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg"),
                        new("spawn1",    "cantwitchconnect:spawn1wolf",      new[] { "1" }, "cantwitchconnect:textures/Wands01.jpg"),
                        new("spawn2",    "cantwitchconnect:spawn2wolf",      new[] { "2" }, "cantwitchconnect:textures/Wands02.jpg"),
                        new("spawn3",    "cantwitchconnect:spawn3wolf",      new[] { "3" }, "cantwitchconnect:textures/Wands03.jpg")
                    }
                },
                new()
                {
                    Name = "bear",
                    Description = "Shall a bear pay the streamer a visit?",
                    Kind = CommandKind.SpawnEntity,
                    EntityCodes = new List<string> { "game:bear-brown-adult-male", "game:bear-brown-adult-female" },
                    Answers = new List<AnswerInfo>
                    {
                        new("dontspawn", "cantwitchconnect:dontspawnbear", new[] { "0", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg"),
                        new("spawn1",    "cantwitchconnect:spawn1bear",    new[] { "1" },              "cantwitchconnect:textures/Wands01.jpg")
                    }
                },
                new()
                {
                    Name = "drifters",
                    Description = "Send drifters to haunt the night.",
                    Kind = CommandKind.SpawnEntity,
                    EntityCodes = new List<string>
                    {
                        "game:drifter-normal", "game:drifter-deep", "game:drifter-tainted",
                        "game:drifter-corrupt", "game:drifter-nightmare"
                    },
                    Answers = new List<AnswerInfo>
                    {
                        new("dontspawn", "cantwitchconnect:dontspawndrifters", new[] { "0", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg"),
                        new("spawn1",    "cantwitchconnect:spawn1drifter",     new[] { "1" },           "cantwitchconnect:textures/Wands01.jpg"),
                        new("spawn2",    "cantwitchconnect:spawn2drifter",     new[] { "2" },           "cantwitchconnect:textures/Wands02.jpg"),
                        new("spawn3",    "cantwitchconnect:spawn3drifter",     new[] { "3" },           "cantwitchconnect:textures/Wands03.jpg")
                    }
                },
                new()
                {
                    Name = "toss",
                    Description = "How high should the streamer fly?",
                    Kind = CommandKind.TossPlayer,
                    Heights = new List<int> { 15, 30, 60 },
                    Answers = new List<AnswerInfo>
                    {
                        new("donttoss", "cantwitchconnect:donttoss", new[] { "0", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg"),
                        new("toss1",    "cantwitchconnect:toss1",    new[] { "1" }, "cantwitchconnect:textures/Pents01.jpg"),
                        new("toss2",    "cantwitchconnect:toss2",    new[] { "2" }, "cantwitchconnect:textures/Pents02.jpg"),
                        new("toss3",    "cantwitchconnect:toss3",    new[] { "3" }, "cantwitchconnect:textures/Pents03.jpg")
                    }
                },
                new()
                {
                    Name = "setonfire",
                    Description = "Light the streamer ablaze?",
                    Kind = CommandKind.SetOnFire,
                    Answers = new List<AnswerInfo>
                    {
                        new("setonfire", "cantwitchconnect:setonfire", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Pents10.jpg"),
                        new("spare",     "cantwitchconnect:spare",     new[] { "1", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg")
                    }
                },
                new()
                {
                    Name = "setday",
                    Description = "Force the sun to rise?",
                    Kind = CommandKind.CallChatCommand,
                    CommandToCall = "/time set day",
                    AllowedChatCommands = new List<string> { "time" },
                    Answers = new List<AnswerInfo>
                    {
                        new("setday",         "cantwitchconnect:setday",          new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_19_Sun.jpg"),
                        new("dontchangetime", "cantwitchconnect:dontchangetime",  new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "setnight",
                    Description = "Plunge the world into darkness?",
                    Kind = CommandKind.CallChatCommand,
                    CommandToCall = "/time set night",
                    AllowedChatCommands = new List<string> { "time" },
                    Answers = new List<AnswerInfo>
                    {
                        new("setnight",       "cantwitchconnect:setnight",        new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_18_Moon.jpg"),
                        new("dontchangetime", "cantwitchconnect:dontchangetime",  new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "stoprain",
                    Description = "Clear the skies?",
                    Kind = CommandKind.ChangeWeather,
                    WeatherChangeType = Actions.WeatherChangeType.STOP_RAIN,
                    Answers = new List<AnswerInfo>
                    {
                        new("stoprain",          "cantwitchconnect:stoprain",          new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_01_Magician.jpg"),
                        new("dontchangeweather", "cantwitchconnect:dontchangeweather", new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "startrain",
                    Description = "Bring down the rain?",
                    Kind = CommandKind.ChangeWeather,
                    WeatherChangeType = Actions.WeatherChangeType.START_RAIN,
                    Answers = new List<AnswerInfo>
                    {
                        new("startrain",         "cantwitchconnect:startrain",         new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Cups07.jpg"),
                        new("dontchangeweather", "cantwitchconnect:dontchangeweather", new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "rtp",
                    Description = "Cast the streamer into the unknown?",
                    Kind = CommandKind.RtpPlayer,
                    Radius = new List<int> { 500, 1000, 1500 },
                    Answers = new List<AnswerInfo>
                    {
                        new("letthembe",   "cantwitchconnect:letthembe",   new[] { "0", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg"),
                        new("rtpplayer1",  "cantwitchconnect:rtpplayer1",  new[] { "1" }, "cantwitchconnect:textures/Pents01.jpg"),
                        new("rtpplayer2",  "cantwitchconnect:rtpplayer2",  new[] { "2" }, "cantwitchconnect:textures/Pents02.jpg"),
                        new("rtpplayer3",  "cantwitchconnect:rtpplayer3",  new[] { "3" }, "cantwitchconnect:textures/Pents03.jpg")
                    }
                },
                new()
                {
                    Name = "giveitem",
                    Description = "Grant the streamer a gift from the earth.",
                    Kind = CommandKind.GiveItem,
                    ItemCodes = new List<string> { "game:flint", "game:stick", "game:rock-granite" },
                    Quantities = new List<int> { 1, 4, 8 },
                    Answers = new List<AnswerInfo>
                    {
                        new("dontgive",   "cantwitchconnect:dontgive",   new[] { "0", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg"),
                        new("giveflint",  "cantwitchconnect:giveflint",  new[] { "1", "flint" }, "cantwitchconnect:textures/Pents01.jpg"),
                        new("givesticks", "cantwitchconnect:givesticks", new[] { "2", "sticks" }, "cantwitchconnect:textures/Pents02.jpg"),
                        new("giverocks",  "cantwitchconnect:giverocks",  new[] { "3", "rocks" }, "cantwitchconnect:textures/Pents03.jpg")
                    }
                },
                new()
                {
                    Name = "dropinventory",
                    Description = "Scatter everything the streamer carries?",
                    Kind = CommandKind.DropInventory,
                    Answers = new List<AnswerInfo>
                    {
                        new("drop", "cantwitchconnect:dropinventory", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_08_Strength.jpg"),
                        new("keep", "cantwitchconnect:keepinventory", new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "shuffle",
                    Description = "Scramble the streamer's hotbar?",
                    Kind = CommandKind.ShuffleHotbar,
                    Answers = new List<AnswerInfo>
                    {
                        new("shuffle", "cantwitchconnect:shufflehotbar", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_08_Strength.jpg"),
                        new("keep",    "cantwitchconnect:dontshuffle",   new[] { "1", "-", "no" }, "cantwitchconnect:textures/Cups05.jpg")
                    }
                },
                new()
                {
                    Name = "lightning",
                    Description = "Call down the wrath of the storm?",
                    Kind = CommandKind.LightningStrike,
                    LightningRadius = 0,
                    Answers = new List<AnswerInfo>
                    {
                        new("strike", "cantwitchconnect:strikelightning", new[] { "0", "+", "yes" }, "cantwitchconnect:textures/Tarot_20_Judgement.jpg"),
                        new("spare",  "cantwitchconnect:sparelightning",  new[] { "1", "-", "no" }, "cantwitchconnect:textures/Tarot_14_Temperance.jpg")
                    }
                }
            };
        }
    }
}
