namespace DataAccess.Models;

/// <summary>
/// Defines the mental/physical intensity level required to complete a task.
/// </summary>
/// <remarks>
/// Intensity affects how many tasks can be scheduled together in a single day.
/// Higher intensity tasks consume more of the user's daily capacity.
/// Used by the scheduling algorithm to distribute task load across working days.
/// </remarks>
public enum ETaskIntensity
{
    /// <summary>Low intensity task - minimal focus required, can be combined with other intensive tasks</summary>
    Light,
    
    /// <summary>Normal intensity task - moderate focus required, standard scheduling consideration</summary>
    Normal,
    
    /// <summary>High intensity task - requires significant focus, limits how many can be scheduled per day</summary>
    Intensive
}