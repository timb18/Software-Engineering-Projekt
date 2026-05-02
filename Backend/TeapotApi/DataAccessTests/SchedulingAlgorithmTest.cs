using DataAccess.Models;
using Services;

namespace DataAccessTests;

[TestFixture]
public class SchedulingAlgorithmTest
{
    [Test]
    public async Task TestSchedulingAlgorithm()
    {
        var task1Guid = Guid.NewGuid();
        var task2Guid = Guid.NewGuid();
        var task3Guid = Guid.NewGuid();
        var workProfileGuid = Guid.NewGuid();
        var userTasks = new List<UserTask>()
        {
            new UserTask()
            {
                Id = task1Guid,
                Name = "Task 1",
                Description = "Task 1",
                Allowsplitting = false,
                EarlyStart = DateTime.Parse("2024-01-01T09:00:00"),
                EarlyFinish = DateTime.Parse("2024-01-01T11:00:00"),
                LateStart = DateTime.Parse("2024-01-01T09:00:00"),
                LateFinish = DateTime.Parse("2024-01-01T11: 00:00"),
                TimeEstimate = TimeSpan.FromHours(2),
                IsFixed = false,
                Deadline = DateTime.Parse("2024-01-04T12:00:00"),
                Minblockduration = 900,
                Maxblockduration = 14400,
                Maxsplits = 5,
                Intensity = ETaskIntensity.Normal,
                Priority = ETaskPriority.Medium,
                Status = "todo",
                WorkProfileId = workProfileGuid,
            },
            new UserTask()
            {
                Id = task2Guid,
                Name = "Task 2",
                Description = "Task 2",
                Allowsplitting = false,
                EarlyStart = DateTime.Parse("2024-01-01T11:00:00"),
                EarlyFinish = DateTime.Parse("2024-01-01T15:00:00"),
                LateStart = DateTime.Parse("2024-01-01T12:00:00"),
                LateFinish = DateTime.Parse("2024-01-01T16: 00:00"),
                TimeEstimate = TimeSpan.FromHours(4),
                IsFixed = false,
                Deadline = DateTime.Parse("2024-01-05T12:00:00"),
                Minblockduration = 900,
                Maxblockduration = 14400,
                Maxsplits = 5,
                Intensity = ETaskIntensity.Intensive,
                Priority = ETaskPriority.High,
                Status = "todo",
                WorkProfileId = workProfileGuid,
            },
            new UserTask()
            {
                Id = task3Guid,
                Name = "Task 3",
                Description = "Task 3",
                Allowsplitting = false,
                EarlyStart = DateTime.Parse("2024-01-01T11:00:00"),
                EarlyFinish = DateTime.Parse("2024-01-01T15:00:00"),
                LateStart = DateTime.Parse("2024-01-01T12:00:00"),
                LateFinish = DateTime.Parse("2024-01-01T16: 00:00"),
                TimeEstimate = TimeSpan.FromHours(4),
                IsFixed = true,
                Deadline = null,
                Minblockduration = 900,
                Maxblockduration = 14400,
                Maxsplits = 5,
                Intensity = ETaskIntensity.Light,
                Priority = ETaskPriority.Medium,
                Status = "todo",
                WorkProfileId = workProfileGuid,
            }
        };

        var dependencies = new List<TaskDependency>()
        {
            new TaskDependency()
            {
                TaskId = task1Guid,
                DependsOnTaskId = task2Guid,
            }
        };

        var workProfile = new WorkProfile()
        {
            Id = workProfileGuid,
            MaxDailyLoad = TimeSpan.FromHours(8),
            WorkDayProfiles = new List<WorkDayProfile>()
            {
                new WorkDayProfile()
                {
                    Day = "Mon",
                    WorkBlocks = new List<WorkBlock>()
                    {
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(9).ToString(),
                            EndTime = TimeSpan.FromHours(12).ToString(),
                        },
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(13).ToString(),
                            EndTime = TimeSpan.FromHours(17).ToString(),
                        }
                    },
                    WorkBreaks = new List<WorkBreak>()
                    {
                        new WorkBreak()
                        {
                            StartTime = TimeSpan.FromHours(12).ToString(),
                            EndTime = TimeSpan.FromHours(13).ToString(),
                        }
                    },
                },
                new WorkDayProfile()
                {
                    Day = "Tue",
                    WorkBlocks = new List<WorkBlock>()
                    {
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(9).ToString(),
                            EndTime = TimeSpan.FromHours(12).ToString(),
                        },
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(13).ToString(),
                            EndTime = TimeSpan.FromHours(17).ToString(),
                        }
                    },
                    WorkBreaks = new List<WorkBreak>()
                    {
                        new WorkBreak()
                        {
                            StartTime = TimeSpan.FromHours(12).ToString(),
                            EndTime = TimeSpan.FromHours(13).ToString(),
                        }
                    },
                },
                new WorkDayProfile()
                {
                    Day = "Wed",
                    WorkBlocks = new List<WorkBlock>()
                    {
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(9).ToString(),
                            EndTime = TimeSpan.FromHours(12).ToString(),
                        },
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(13).ToString(),
                            EndTime = TimeSpan.FromHours(17).ToString(),
                        }
                    },
                    WorkBreaks = new List<WorkBreak>()
                    {
                        new WorkBreak()
                        {
                            StartTime = TimeSpan.FromHours(12).ToString(),
                            EndTime = TimeSpan.FromHours(13).ToString(),
                        }
                    },

                },
                new WorkDayProfile()
                {
                    Day = "Thu",
                    WorkBlocks = new List<WorkBlock>()
                    {
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(9).ToString(),
                            EndTime = TimeSpan.FromHours(12).ToString(),
                        },
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(13).ToString(),
                            EndTime = TimeSpan.FromHours(17).ToString(),
                        }
                    },
                    WorkBreaks = new List<WorkBreak>()
                    {
                        new WorkBreak()
                        {
                            StartTime = TimeSpan.FromHours(12).ToString(),
                            EndTime = TimeSpan.FromHours(13).ToString(),
                        }
                    },
                },
                new WorkDayProfile()
                {
                    Day = "Fri",
                    WorkBlocks = new List<WorkBlock>()
                    {
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(9).ToString(),
                            EndTime = TimeSpan.FromHours(12).ToString(),
                        },
                        new WorkBlock()
                        {
                            StartTime = TimeSpan.FromHours(13).ToString(),
                            EndTime = TimeSpan.FromHours(17).ToString(),
                        }
                    },
                    WorkBreaks = new List<WorkBreak>()
                    {
                        new WorkBreak()
                        {
                            StartTime = TimeSpan.FromHours(12).ToString(),
                            EndTime = TimeSpan.FromHours(13).ToString(),
                        }
                    },
                },
            }
        };

        var fixedBlocks = new List<TaskBlock>()
        {
            new TaskBlock()
            {
                TaskId = task3Guid,
                StartDate = DateTime.Parse("2024-01-01T11:00:00"),
                EndDate = DateTime.Parse("2024-01-01T16:00:00"),
                IsFixed = true,
            }
        };
        
        var planningStart = DateTime.Parse("2024-01-01T00:00:00");
        var planningEnd = DateTime.Parse("2024-01-07T00:00:00");
        
        SchedulingAlgorithm algorithm = new SchedulingAlgorithm();
        
        var result = algorithm.PlanTasks(userTasks, dependencies, workProfile, fixedBlocks, planningStart, planningEnd);
        
        Assert.That(result.NewBlocks, Is.Not.Empty);
        Assert.That(result.Conflicts, Is.Empty);
    }
}