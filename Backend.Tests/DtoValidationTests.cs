using System.ComponentModel.DataAnnotations;
using QULOOBUL__MOE_MIENEEN_Shared.Collaboration;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;
using QULOOBUL__MOE_MIENEEN_Shared.Tasks;
using Xunit;

namespace Backend.Tests;

// [Required] on a non-nullable int or DateTime never fails, so the DTOs pair it with a Range.
// These pin that the unset defaults (0, 0001-01-01) and free-text priorities are rejected.
public class DtoValidationTests
{
    private static bool Valid(object dto) =>
        Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true);
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public void A_task_with_unset_ids_or_deadline_or_unknown_priority_is_rejected()
    {
        var ok = new CreateTaskRequest { ProjectId = 1, Title = "Sweep hall", Priority = "High", Deadline = new DateTime(2026, 10, 1) };
        Assert.True(Valid(ok));
        Assert.False(Valid(ok with { ProjectId = 0 }));
        Assert.False(Valid(ok with { Deadline = default }));
        Assert.False(Valid(ok with { Priority = "Whenever" }));
        Assert.False(Valid(new AssignTaskRequest()));
    }
//----------------------------------------------------------------------------------------------------------------------//
    [Fact]
    public void Unset_foreign_keys_and_dates_elsewhere_are_rejected()
    {
        Assert.False(Valid(new CreateMeetingRequest { Title = "Committee" }));
        Assert.False(Valid(new AddProjectMemberRequest()));
        Assert.False(Valid(new CreateUserRequest { FirstName = "A", Surname = "B", Email = "a@b.co", CellNo = "0821234567" }));
        Assert.True(Valid(new CreateUserRequest { FirstName = "A", Surname = "B", Email = "a@b.co", CellNo = "0821234567", RoleId = 2 }));
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
