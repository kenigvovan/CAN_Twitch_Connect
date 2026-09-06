using System.ComponentModel;
using ProtoBuf;
using System;

namespace cantwitchconnect.Network
{
    [ProtoContract]
    public sealed class PollStartedMessage
    {
        [ProtoMember(1)] public string PollName    { get; set; } = "";
        [ProtoMember(2)] public string[] Labels    { get; set; } = Array.Empty<string>();
        [ProtoMember(4)] public int      DurationSeconds { get; set; }
        [ProtoMember(5)] public string[] CardAssets     { get; set; } = Array.Empty<string>();
        [ProtoMember(6)] public string   Description    { get; set; } = "";
    }

    [ProtoContract]
    public sealed class PollFinishedMessage
    {
        [ProtoMember(1)] public bool HasWinner   { get; set; }
        [ProtoMember(2)] public int  WinnerIndex { get; set; }
    }

    [ProtoContract]
    public sealed class ForceMotionMessage
    {
        [ProtoMember(1)] public double MotionY { get; set; }
    }

    [ProtoContract]
    public sealed class CommandListMessage
    {
        [ProtoMember(1)] public string[] Names         { get; set; } = Array.Empty<string>();
        [ProtoMember(2)] public bool[]   EnabledStates { get; set; } = Array.Empty<bool>();
    }

    [ProtoContract]
    public sealed class ToggleCommandMessage
    {
        [ProtoMember(1)] public string CommandName { get; set; } = "";
        [ProtoMember(2)] public bool   Enabled     { get; set; }
    }

    [ProtoContract]
    public sealed class TriggerPollMessage
    {
        [ProtoMember(1)] public string CommandName { get; set; } = "";
    }

    [ProtoContract]
    public sealed class DebugVoteMessage
    {
        [ProtoMember(1)] public int OptionIndex { get; set; }
    }

    [ProtoContract]
    public sealed class PollVotesMessage
    {
        [ProtoMember(1)] public int[] Counts { get; set; } = Array.Empty<int>();
    }

    [ProtoContract]
    public sealed class PreviewRequestMessage
    {
        [ProtoMember(1)] public string CommandName  { get; set; } = "";
        [ProtoMember(2)] public bool   HasWinner    { get; set; }
        [ProtoMember(3)] public int    WinnerIndex  { get; set; }
        [ProtoMember(4)] public bool   ExecuteAction { get; set; }
    }
}
