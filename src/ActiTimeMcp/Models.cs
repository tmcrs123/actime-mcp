using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActiTimeMcp;

// Shapes follow https://www.actitime.com/api-documentation (REST API v1). Durations are minutes, dates are yyyy-MM-dd.

public sealed record PagedResult<T>(int Offset, int Limit, List<T> Items);

public sealed record ActiUser(
    long Id,
    string Username,
    bool Active,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Email,
    long? DepartmentId,
    long? TimeZoneGroupId);

public sealed record Customer(long Id, string Name, bool Archived, string? Description);

public sealed record Project(long Id, long CustomerId, string? CustomerName, string Name, bool Archived, string? Description);

public sealed record ActiTask(
    long Id,
    string Name,
    string? Description,
    string? Status,
    long? ProjectId,
    string? ProjectName,
    long? CustomerId,
    string? CustomerName,
    long? TypeOfWorkId,
    string? TypeOfWorkName,
    string? Deadline,
    int? EstimatedTime);

public sealed record TimeTrackResponse(
    string DateFrom,
    string? DateTo,
    string? NextDateFrom,
    List<TimeTrackDay> Data,
    // Referenced objects (includeReferenced=tasks,...) come back as maps keyed by id.
    Dictionary<string, JsonElement>? Tasks);

public sealed record TimeTrackDay(long UserId, string Date, int DayOffset, bool? Approved, List<TimeTrackRecord> Records);

public sealed record TimeTrackRecord(long TaskId, int Time, string? Comment);

public sealed record ApiError(string? Key, string? Message);

// ----- Shapes returned to the model -----

public sealed record TimeEntry(
    string Date,
    long TaskId,
    string? TaskName,
    string? ProjectName,
    string? CustomerName,
    int Minutes,
    string Duration,
    string? Comment);

public sealed record DaySummary(string Date, int TotalMinutes, string Total, List<TimeEntry> Entries);
