using Microsoft.EntityFrameworkCore;
using QULOOBUL__MOE_MIENEEN_Backend.Data;
using QULOOBUL__MOE_MIENEEN_Backend.Models.IdentityAndAccess;
using QULOOBUL__MOE_MIENEEN_Shared.Directory;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>Admin user management: create accounts (SM-03 starts them Active) and list roles.</summary>
    public class UserService(AppDbContext db, AuthService auth, AuditService audit)
    {
        public async Task<UserDto> CreateAsync(CreateUserRequest req, int actorId)
        {
            var email = req.Email.Trim();
            if (await db.Users.AnyAsync(u => u.Email == email))
                throw new InvalidOperationException("An account with that email already exists.");
            var role = await db.Roles.FindAsync(req.RoleId)
                ?? throw new InvalidOperationException($"Unknown role {req.RoleId}.");

            var user = new User
            {
                RoleId = role.RoleId,
                FirstName = req.FirstName.Trim(),
                Surname = req.Surname.Trim(),
                Email = email,
                CellNo = req.CellNo.Trim(),
            };
            user.PasswordHash = auth.Hash(user, req.TempPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();                 // assigns user.UserId
            await audit.RecordAndSaveAsync(actorId, "user.create", nameof(User), user.UserId);

            return new UserDto(user.UserId, user.FirstName, user.Surname, user.Email, user.CellNo, role.RoleName, user.IsActive);
        }
//----------------------------------------------------------------------------------------------------------------------//
        public Task<List<RoleDto>> RolesAsync() =>
            db.Roles.OrderBy(r => r.HierarchyLevel).ThenBy(r => r.RoleName)
                .Select(r => new RoleDto(r.RoleId, r.RoleName, r.HierarchyLevel)).ToListAsync();
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//