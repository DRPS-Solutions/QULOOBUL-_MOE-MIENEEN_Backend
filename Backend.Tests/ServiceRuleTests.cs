using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Backend.Models.Request;
using QULOOBUL__MOE_MIENEEN_Backend.Services;
using Xunit;

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

        var requests = new RequestReviewService(db, new AuditService(db), null!);

        Assert.Equal(2, (await requests.ListAsync(reviewer.UserId)).Count);
        Assert.Equal(new[] { alice.UserId }, (await requests.ListAsync(alice.UserId)).Select(r => r.RequestedBy));
    }

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

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
