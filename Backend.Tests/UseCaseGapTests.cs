using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Backend.Models.File;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.NotificationsAudit;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Reports;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;

namespace Backend.Tests;

// UC-03 / UC-07 / UC-08 / UC-09 / recurring schedules / booking integrity. Hub and blob are never reached.
public class UseCaseGapTests
{
    [Fact]
    public async Task A_muted_type_is_not_stored_but_an_admin_announcement_still_is()
    {
        using var db = NewDb();
        var user = User(db, "alice", 4);
        var type = new NotificationType { TypeName = "TaskAssigned" };
        db.NotificationPreferences.Add(new NotificationPreference { User = user, Type = type, IsEnabled = false });
        await db.SaveChangesAsync();

        var notify = new NotificationService(db, null!);
        var muted = await notify.QueueAsync(user.UserId, "TaskAssigned", "m", "Test", 1);
        await notify.QueueAsync(user.UserId, "TaskAssigned", "direct", "announcement", 0, ignorePreferences: true);
        await db.SaveChangesAsync();

        Assert.True(muted.IsMuted);
        Assert.Equal("direct", (await db.Notifications.SingleAsync()).Message);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task A_meeting_cannot_double_book_someone_already_at_an_event()
    {
        using var db = NewDb();
        var organiser = User(db, "org", 1);
        var busy = User(db, "busy", 4);
        var when = new DateTime(2026, 10, 1, 9, 0, 0);
        var ev = new Event { Organiser = organiser, Title = "Fete", EventDate = when };
        db.EventAttendees.Add(new EventAttendee { Event = ev, User = busy, Status = "Accepted" });
        await db.SaveChangesAsync();

        var meetings = new MeetingMinutesService(db, new AuditService(db), new AccessGuard(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => meetings.CreateMeetingAsync(
            new CreateMeetingRequest { Title = "Clash", MeetingDate = when, AttendeeIds = [busy.UserId] }, organiser.UserId));
        Assert.Empty(db.Meetings);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Minutes_append_as_a_new_version_and_only_the_permitted_may_edit()
    {
        using var db = NewDb();
        var organiser = User(db, "org", 2);
        var recorder = User(db, "rec", 4);
        var bystander = User(db, "by", 4);
        var meeting = new Meeting { Organiser = organiser, Title = "AGM", MeetingDate = DateTime.UtcNow };
        db.MeetingAttendees.AddRange(
            new MeetingAttendee { Meeting = meeting, User = recorder },
            new MeetingAttendee { Meeting = meeting, User = bystander });
        await db.SaveChangesAsync();
        var svc = new MeetingMinutesService(db, new AuditService(db), new AccessGuard(db));

        var v1 = await svc.RecordAsync(meeting.MeetingId, new RecordMinutesRequest { MinutesText = "Opened." }, recorder.UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.EditAsync(v1.MinutesId, new EditMinutesRequest { MinutesText = "x" }, bystander.UserId));

        var v2 = await svc.EditAsync(v1.MinutesId, new EditMinutesRequest { MinutesText = "Closed.", Append = true }, organiser.UserId);
        Assert.Equal("Opened." + Environment.NewLine + "Closed.", v2.MinutesText);
        Assert.Equal(2, await db.MeetingMinutes.CountAsync());           // the original is kept
        await Assert.ThrowsAsync<InvalidOperationException>(() =>        // stale version refused
            svc.EditAsync(v1.MinutesId, new EditMinutesRequest { MinutesText = "y" }, recorder.UserId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Archived_files_are_hidden_unless_asked_for_and_search_matches_the_name()
    {
        using var db = NewDb();
        var admin = User(db, "admin", 1);
        var cat = new FileCategory { CategoryName = "Other" };
        db.FileUploads.AddRange(
            new FileUpload { Category = cat, Uploader = admin, FileName = "budget.xlsx" },
            new FileUpload { Category = cat, Uploader = admin, FileName = "old-budget.xlsx", IsArchived = true },
            new FileUpload { Category = cat, Uploader = admin, FileName = "photo.png" });
        await db.SaveChangesAsync();
        var files = new FileManagementService(db, null!, new AuditService(db), new AccessGuard(db));

        Assert.Equal(["budget.xlsx"], (await files.ListAsync(null, null, null, "budget", false, admin.UserId)).Select(f => f.FileName));
        Assert.Equal(2, (await files.ListAsync(null, null, null, "budget", true, admin.UserId)).Count);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task An_empty_period_asks_for_a_wider_range_and_is_not_logged_as_run()
    {
        using var db = NewDb();
        var reports = new ReportingService(db, null!);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => reports.BuildAsync(
            new ReportRequest { PeriodStart = new(2020, 1, 1), PeriodEnd = new(2020, 1, 31) }, 1));
        Assert.Contains("wider date range", ex.Message);
        Assert.Empty(db.Reports);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public void Trend_buckets_by_day_up_to_a_month_and_counts_a_reopened_task_once_per_bucket()
    {
        var start = new DateTime(2026, 9, 1);
        var trend = ReportingService.Trend(start, start.AddDays(2),
            [(1, start.AddHours(1)), (1, start.AddHours(5)), (2, start.AddDays(2))]);

        Assert.Equal([1, 0, 1], trend.Select(p => p.Completed));
        Assert.Equal(2, trend[^1].CumulativeCompleted);
        var weekly = ReportingService.Trend(start, start.AddDays(60), []);
        Assert.Equal(TimeSpan.FromDays(7), weekly[1].BucketStart - weekly[0].BucketStart);
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]   // formula neutralised, quotes doubled
    [InlineData("-5", "\"-5\"")]                                     // a real negative number is left alone
    [InlineData("Smith, J", "\"Smith, J\"")]
    public void Csv_cells_are_quoted_and_formula_safe(string value, string expected) =>
        Assert.Equal(expected, ReportingService.Cell(value));
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public async Task Schedules_follow_the_assignment_rule_and_take_only_real_day_names()
    {
        using var db = NewDb();
        var staff = User(db, "staff", 4);
        var boss = User(db, "boss", 1);
        await db.SaveChangesAsync();
        var svc = new TaskScheduleService(db, new AuditService(db), new AccessGuard(db),
            new TaskAssignmentService(db, null!, null!, null!, null!));

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(   // never assign up
            new CreateTaskScheduleRequest { Title = "Sweep", DayOfWeek = "Friday", AssignedTo = boss.UserId }, staff.UserId));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(         // would parse as Wednesday
            new CreateTaskScheduleRequest { Title = "Sweep", DayOfWeek = "Monday,Tuesday", AssignedTo = staff.UserId }, boss.UserId));

        var s = await svc.CreateAsync(
            new CreateTaskScheduleRequest { Title = "Sweep", DayOfWeek = "saturday", AssignedTo = staff.UserId }, boss.UserId);
        Assert.Equal("Saturday", s.DayOfWeek);                                     // what MaintenanceJobs compares
        Assert.False((await svc.UpdateAsync(s.ScheduleId, new UpdateTaskScheduleRequest { IsActive = false }, boss.UserId)).IsActive);
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static User User(AppDbContext db, string name, int level)
    {
        var u = new User { Role = new Role { RoleName = name, HierarchyLevel = level }, Email = $"{name}@test", FirstName = name };
        db.Users.Add(u);
        return u;
    }
//----------------------------------------------------------------------------------------------------------------------//
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//