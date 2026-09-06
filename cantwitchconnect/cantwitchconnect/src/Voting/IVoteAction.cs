namespace cantwitchconnect.Voting
{
    public interface IVoteAction
    {
        void Execute(int winnerIndex, AnswerInfo winner);
    }
}
