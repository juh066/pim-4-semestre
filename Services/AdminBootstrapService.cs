using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public class AdminBootstrapService(AppDbContext db)
{
    public async Task<ServiceResult<int>> CreateFirstAsync(string name, string cpf, string email, string password)
    {
        if (await db.Users.AnyAsync(item => item.Role == UserRoles.Administrator))
        {
            return ServiceResult<int>.Fail("exists", "Já existe um administrador; o comando inicial foi bloqueado.");
        }
        if (!CpfValidator.IsValid(cpf) || password.Length < 12 || string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<int>.Fail("invalid", "Nome e CPF válido são obrigatórios; a senha deve ter ao menos 12 caracteres.");
        }

        email = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(item => item.Email == email || item.Cpf == cpf))
        {
            return ServiceResult<int>.Fail("duplicate", "Email ou CPF já cadastrado.");
        }

        var admin = new User
        {
            Name = name.Trim(), Cpf = cpf, Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = UserRoles.Administrator,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(admin.Id, "Primeiro administrador criado.");
    }
}
