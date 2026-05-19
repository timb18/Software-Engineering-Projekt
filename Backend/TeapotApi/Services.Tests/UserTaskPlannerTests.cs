using DataAccess.Models;
using Services.Planning;

namespace Services.Tests;

[TestFixture]
public class UserTaskPlannerTests
{
    [Test]
    public void GenerateTimeSlots_SubtractsUserBreaksFromWorkBlocks()
    {
        var date = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc);
        var profile = new WorkProfile
        {
            Days =
            [
                new WorkDayProfile
                {
                    Day = "Mon",
                    Blocks =
                    [
                        new WorkBlock { StartTime = "09:00", EndTime = "12:00" }
                    ],
                    Breaks =
                    [
                        new WorkBreak { StartTime = "10:00", EndTime = "10:30" }
                    ]
                }
            ]
        };

        var slots = UserTaskPlanner.GenerateTimeSlots(profile, date, date.AddDays(1));

        Assert.That(
            slots.Select(slot => (slot.Start.ToString("HH:mm"), slot.End.ToString("HH:mm"))),
            Is.EqualTo(new[] { ("09:00", "10:00"), ("10:30", "12:00") }));
    }

    [Test]
    public void GenerateTimeSlots_SubtractsBreaksThatPartiallyOverlapWorkBlocks()
    {
        var date = new DateTime(2026, 5, 18, 0, 0, 0, DateTimeKind.Utc);
        var profile = new WorkProfile
        {
            Days =
            [
                new WorkDayProfile
                {
                    Day = "Mon",
                    Blocks =
                    [
                        new WorkBlock { StartTime = "09:00", EndTime = "12:00" },
                        new WorkBlock { StartTime = "13:00", EndTime = "17:00" }
                    ],
                    Breaks =
                    [
                        new WorkBreak { StartTime = "08:45", EndTime = "09:15" },
                        new WorkBreak { StartTime = "11:45", EndTime = "13:15" },
                        new WorkBreak { StartTime = "16:45", EndTime = "17:30" }
                    ]
                }
            ]
        };

        var slots = UserTaskPlanner.GenerateTimeSlots(profile, date, date.AddDays(1));

        Assert.That(
            slots.Select(slot => (slot.Start.ToString("HH:mm"), slot.End.ToString("HH:mm"))),
            Is.EqualTo(new[] { ("09:15", "11:45"), ("13:15", "16:45") }));
    }
}
