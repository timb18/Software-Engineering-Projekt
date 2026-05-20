using System.Text.Json.Serialization;

namespace DataAccess.Models;

public class RecurringBlocker
{
    public Guid Id { get; set; }

    public Guid WorkProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Comma-separated three-letter day abbreviations, e.g. "Mon,Wed,Fri"</summary>
    public string DaysOfWeek { get; set; } = string.Empty;

    /// <summary>Start time in HH:mm format, e.g. "09:00"</summary>
    public string StartTime { get; set; } = string.Empty;

    /// <summary>End time in HH:mm format, e.g. "10:00"</summary>
    public string EndTime { get; set; } = string.Empty;

    /// <summary>Optional: blocker only applies from this date onwards (inclusive).</summary>
    public DateOnly? ValidFrom { get; set; }

    /// <summary>Optional: blocker only applies up to and including this date.</summary>
    public DateOnly? ValidUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    [JsonIgnore] public virtual WorkProfile? WorkProfile { get; set; }
}