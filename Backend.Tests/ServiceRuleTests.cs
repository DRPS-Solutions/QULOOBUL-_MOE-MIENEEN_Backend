using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Collaboration;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using Xunit;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;

namespace Backend.Tests;

// Service rules the web client relies on. The hub is never touched by these paths, so it is null.
public class ServiceRuleTests
{
    // Requests are the one list filtered per caller; if this breaks, staff read each other's requests.
    [Fact]
    public async Task Reviewers_see_every_request_and_staff_only_their_own()
    {
        using var db = NewDb();
        var reviewerRole = new Role { RoleName = "Admin", HierarchyLevel = 1 };
        db.RolePermissions.Add(new RolePermission
        {
            Role = reviewerRole,
            Permission = new Permission { PermissionName = RequestReviewService.ReviewPermission },
        });
        var staffRole = new Role { RoleName = "Staff", HierarchyLevel = 4 };
        var reviewer = new User { Role = reviewerRole, Email = "reviewer@test" };
        var alice = new User { Role = staffRole, Email = "alice@test" };
        var bob = new User { Role = staffRole, Email = "bob@test" };
        var category = new RequestCategory { CategoryName = "Equipment" };
        db.Users.AddRange(reviewer, alice, bob);
        db.ResourceRequests.AddRange(
            new ResourceRequest { Requester = alice, Category = category, Status = "Pending" },
            new ResourceRequest { Requester = bob, Category = category, Status = "Pending" });
        await db.SaveChangesAsync();

        var requests = new RequestReviewService(db, new AuditService(db), null!, null!);

        Assert.Equal(2, (await requests.ListAsync(reviewer.UserId)).Count);
        Assert.Equal(new[] { alice.UserId }, (await requests.ListAsync(alice.UserId)).Select(r => r.RequestedBy));
    }
//----------------------------------------------------------------------------------------------------------------------//
    // Test plan R6: facility managers read every request so the same thing isn't ordered twice.
    [Fact]
    public async Task Facility_managers_see_every_request()
    {
        using var db = NewDb();
        var category = new RequestCategory { CategoryName = "Equipment" };
        var managerRole = new Role { RoleName = Roles.FacilityManager, HierarchyLevel = 3 };
        var manager = new User { Role = managerRole, Email = "fm1@test" };
        var otherManager = new User { Role = managerRole, Email = "fm2@test" };
        db.Users.AddRange(manager, otherManager);
        db.ResourceRequests.Add(new ResourceRequest { Requester = otherManager, Category = category, Status = "Pending" });
        await db.SaveChangesAsync();

        var requests = new RequestReviewService(db, new AuditService(db), null!, null!);

        Assert.Single(await requests.ListAsync(manager.UserId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    // Test plan M5: ViewAllMeetings (Secondary Admin) reads every meeting; without it, only your own.
    [Fact]
    public async Task ViewAllMeetings_holders_see_every_meeting_and_others_only_their_own()
    {
        using var db = NewDb();
        var secondaryRole = new Role { RoleName = Roles.SecondaryAdmin, HierarchyLevel = 2 };
        db.RolePermissions.Add(new RolePermission
        {
            Role = secondaryRole,
            Permission = new Permission { PermissionName = Permissions.ViewAllMeetings },
        });
        var secondary = new User { Role = secondaryRole, Email = "second@test" };
        var staff = new User { Role = new Role { RoleName = Roles.Staff, HierarchyLevel = 4 }, Email = "staff@test" };
        var organiser = new User { Role = new Role { RoleName = Roles.Imaamat, HierarchyLevel = 2 }, Email = "org@test" };
        db.Users.AddRange(secondary, staff, organiser);
        db.Meetings.Add(new Meeting { Organiser = organiser, Title = "Board", MeetingDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var meetings = new MeetingMinutesService(db, new AuditService(db), new AccessGuard(db));

        Assert.Single(await meetings.ListAsync(secondary.UserId));
        Assert.Empty(await meetings.ListAsync(staff.UserId));
    }
//----------------------------------------------------------------------------------------------------------------------//
    // Submitting a request notifies several reviewers with a type that may not exist yet.
    [Fact]
    public async Task Queueing_a_new_notification_type_twice_stores_the_type_once()
    {
        using var db = NewDb();
        var user = new User { Role = new Role { RoleName = "Staff" }, Email = "alice@test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var notify = new NotificationService(db, null!);
        await notify.QueueAsync(user.UserId, "BrandNewType", "first", "Test", 1);
        await notify.QueueAsync(user.UserId, "BrandNewType", "second", "Test", 1);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.NotificationTypes.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
    }
//----------------------------------------------------------------------------------------------------------------------//
    // SD-04 validates before the SAS is handed out; once the client holds one the API is out of
    // the loop, so an oversized or executable upload has to be refused here or not at all.
    [Theory]
    [InlineData("report.pdf", 1024, true)]
    [InlineData("photo.JPG", 1024, true)]                       // extension match is case-insensitive
    [InlineData("huge.pdf", 30L * 1024 * 1024, false)]          // over 25 MB
    [InlineData("payload.exe", 1024, false)]                    // not a permitted type
    [InlineData("noextension", 1024, false)]
    [InlineData("empty.pdf", 0, false)]
    public async Task Uploads_are_validated_before_a_SAS_is_issued(string fileName, long size, bool permitted)
    {
        using var db = NewDb();
        var uploads = new FileUploadService(db, null!, new AuditService(db), new ConfigurationBuilder().Build());
        var context = new FileUploadService.AttachmentContext();

        var attempt = () => uploads.CreateUploadAsync(1, 1, fileName, "application/octet-stream", size, context);

        if (permitted)
            // A permitted file gets past validation and fails later, on the unknown category —
            // which is the proof that validation let it through.
            await Assert.ThrowsAsync<InvalidOperationException>(attempt);
        else
            await Assert.ThrowsAsync<ArgumentException>(attempt);
    }
//----------------------------------------------------------------------------------------------------------------------//
    // Reference: Microsoft, "Testing without your production database system - EF Core." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//