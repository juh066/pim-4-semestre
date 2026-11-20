using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public class AdminBootstrapService(AppDbContext db)
{
    public async Task<ServiceResult<int>> CreateFirstAsync(string name, string email, string password)
    {
        if (await db.Users.AnyAsync(item => item.Role == UserRoles.Administrator))
        {
            return ServiceResult<int>.Fail("exists", "Já existe um administrador; o comando inicial foi bloqueado.");
        }
        if (password.Length < 12 || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<int>.Fail("invalid", "Nome é obrigatório; a senha deve ter ao menos 12 caracteres.");
        }

        email = EmailAddressValidator.Normalize(email);
        if (!EmailAddressValidator.IsValid(email))
        {
            return ServiceResult<int>.Fail("invalid_email", EmailAddressValidator.ErrorMessage);
        }
        if (await db.Users.AnyAsync(item => item.Email == email))
        {
            return ServiceResult<int>.Fail("duplicate", "E-mail já cadastrado.");
        }

        var admin = new User
        {
            Name = name.Trim(), Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = UserRoles.Administrator,
            CreatedAt = DateTime.UtcNow, IsActive = true, SessionVersion = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(admin.Id, "Primeiro administrador criado.");
    }
}
