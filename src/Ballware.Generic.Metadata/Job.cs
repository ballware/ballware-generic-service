using System.Runtime.Serialization;

namespace Ballware.Generic.Metadata;

public class JobCreatePayload
{
    public required string Scheduler { get; set; }

    public required string Identifier { get; set; }

    public string? Options { get; set; }
}

public class JobUpdatePayload
{
    public required Guid Id { get; set; }
    public JobStates State { get; set; }
    public string? Result { get; set; }
}

public enum JobStates
{
    [EnumMember(Value = @"Unknown")]
    Unknown = 0,

    [EnumMember(Value = @"Queued")]
    Queued = 1,

    [EnumMember(Value = @"InProgress")]
    InProgress = 2,

    [EnumMember(Value = @"Finished")]
    Finished = 3,

    [EnumMember(Value = @"Error")]
    Error = 4,

}