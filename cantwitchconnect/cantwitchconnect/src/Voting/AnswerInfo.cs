namespace cantwitchconnect.Voting
{
    public sealed class AnswerInfo
    {
        public string AnswerName { get; set; }
        public string LangCode   { get; set; }
        public string[] Aliases  { get; set; }

        /// <summary>
        /// Optional tarot card image for this answer.
        /// Asset location, e.g. "cantwitchconnect:textures/tarot/death.png"
        /// </summary>
        public string TarotCard  { get; set; }

        public AnswerInfo() { }

        public AnswerInfo(string answerName, string langCode, string[] aliases, string tarotCard = null)
        {
            AnswerName = answerName;
            LangCode   = langCode;
            Aliases    = aliases;
            TarotCard  = tarotCard;
        }
    }
}
