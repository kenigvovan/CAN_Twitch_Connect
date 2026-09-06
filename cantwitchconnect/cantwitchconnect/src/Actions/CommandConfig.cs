using System.Collections.Generic;
using cantwitchconnect.Voting;

namespace cantwitchconnect.Actions
{
    public sealed class CommandConfig
    {
        public string Name;
        public string Description = "";
        public CommandKind Kind;

        public int SecondsForVote = 30;
        public int MinimumVotes = 1;
        public bool Enabled = true;
        public int CooldownSeconds = 0;
        public bool SubscribersOnly = false;

        public string StartLangCode = "cantwitchconnect:poll_started_for_command";
        public string FinishLangCode = "cantwitchconnect:poll_finished_for_command";

        public List<AnswerInfo> Answers = new();

        public List<string> PlayerNames;
        public List<string> EntityCodes;
        public List<int> Heights;
        public List<int> Radius;
        public HealthChangeType? HealthChangeType;
        public WeatherChangeType? WeatherChangeType;
        public string CommandToCall;
        public List<string> AllowedChatCommands;
        public List<string> ItemCodes;
        public List<int> Quantities;
        public int? LightningRadius;
    }
}
