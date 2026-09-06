using System;

namespace cantwitchconnect.Voting
{
    public sealed class VoteDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public AnswerInfo[] Answers { get; }
        public int SecondsForVote { get; }
        public int MinVotes { get; }
        public string StartLangCode { get; }
        public string FinishLangCode { get; }
        public bool Enabled { get; }
        public int CooldownSeconds { get; }
        public bool SubscribersOnly { get; }

        public VoteDefinition(
            string name,
            string description,
            AnswerInfo[] answers,
            int secondsForVote,
            int minVotes,
            string startLangCode,
            string finishLangCode,
            bool enabled,
            int cooldownSeconds,
            bool subscribersOnly = false)
        {
            Name = name;
            Description = description ?? "";
            Answers = answers ?? Array.Empty<AnswerInfo>();
            SecondsForVote = secondsForVote;
            MinVotes = minVotes;
            StartLangCode = startLangCode;
            FinishLangCode = finishLangCode;
            Enabled = enabled;
            CooldownSeconds = cooldownSeconds;
            SubscribersOnly = subscribersOnly;
        }

        public int TryPlaceVote(string message)
        {
            if (string.IsNullOrEmpty(message)) return -1;
            var trimmed = message.Trim();
            for (int i = 0; i < Answers.Length; i++)
            {
                var aliases = Answers[i].Aliases;
                if (aliases == null) continue;
                for (int j = 0; j < aliases.Length; j++)
                {
                    if (string.Equals(aliases[j], trimmed, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }
    }
}
