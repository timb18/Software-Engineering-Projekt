using DataAccess.Models;

namespace Api.Dtos;

/// <summary>Response DTO for task planning operation.</summary>
public class PlanTasksResponseDto
{
    /// <summary>Newly created task blocks from the planning algorithm.</summary>
    public List<TaskBlock> NewBlocks { get; set; } = new();

    /// <summary>Hard constraint violations that prevented scheduling (e.g., deadline missed, capacity exceeded).</summary>
    public List<string> Conflicts { get; set; } = new();

    /// <summary>Warnings for soft constraint violations (e.g., preferred light task not scheduled after intensive).</summary>
    public List<string> Warnings { get; set; } = new();
}

