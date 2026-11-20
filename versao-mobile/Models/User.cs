using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = UserRoles.Visitor;

        public bool IsActive { get; set; } = true;

        [Required, MaxLength(64)]
        public string SessionVersion { get; set; } = Guid.NewGuid().ToString("N");
    }

    public static class UserRoles
    {
        public const string Visitor = "Visitor";
        public const string Administrator = "Administrator";
    }
}
