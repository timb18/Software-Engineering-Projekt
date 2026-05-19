namespace DataAccess.Models;

/// <summary>
/// Defines the priority level of a task in the work planning system.
/// </summary>
/// <remarks>
/// Priority affects task scheduling and helps users prioritize work during planning.
/// Higher priority tasks are typically scheduled earlier when possible.
/// </remarks>
public enum ETaskPriority
{
    /// <summary>Low priority task - can be deferred or completed if time permits</summary>
    Low,
    
    /// <summary>Medium priority task - normal scheduling with standard considerations</summary>
    Medium,
    
    /// <summary>High priority task - should be scheduled and completed as soon as possible</summary>
    High
}