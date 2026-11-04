using System.Security.Claims;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Antiforgery;
using AuroraGaleria.Services;

namespace AuroraGaleria.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            try
            {
                request.Email = EmailAddressValidator.Normalize(request.Email);
                request.Cpf = request.Cpf.Trim();
                request.Name = request.Name.Trim();

                if (!EmailAddressValidator.IsValid(request.Email))
                {
                    return BadRequest(new { message = EmailAddressValidator.ErrorMessage });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { message = "Preencha nome, CPF, e-mail e senha corretamente." });
                }

                if (await _db.Users.AnyAsync(user => user.Email == request.Email))
                {
                    return Conflict(new { message = "Este e-mail já está cadastrado." });
                }

                if (await _db.Users.AnyAsync(user => user.Cpf == request.Cpf))
                {
                    return Conflict(new { message = "Este CPF já está cadastrado." });
                }

                var user = new User
                {
                    Name = request.Name,
                    Cpf = request.Cpf,
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = UserRoles.Visitor,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                return Ok(new { message = "Cadastro concluído com sucesso." });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Não foi possível concluir o cadastro agora. Tente novamente mais tarde."
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            try
            {
                var email = EmailAddressValidator.Normalize(request.Email);
                var user = await _db.Users.SingleOrDefaultAsync(account => account.Email == email);

                if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "E-mail ou senha incorretos." });
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Name),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Role, user.Role)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });

                return Ok(new { name = user.Name, initials = GetInitials(user.Name), email = user.Email, role = user.Role });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Não foi possível validar o login agora. Tente novamente mais tarde."
                });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Você foi desconectado." });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var name = User.Identity?.Name ?? string.Empty;
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            return Ok(new { name, initials = GetInitials(name), email, role = User.FindFirstValue(ClaimTypes.Role) });
        }

        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        [HttpGet("csrf")]
        public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { token = tokens.RequestToken });
        }

        private static string GetInitials(string name)
        {
            return string.Join(
                string.Empty,
                name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(part => char.ToUpperInvariant(part[0])));
        }
    }
}
