using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class Produto
    {
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string Nome { get; set; } = "";

        [Range(0.01, 999999)]
        public decimal Preco { get; set; }

        [Required, MaxLength(300)]
        public string ImagemUrl { get; set; } = "";

        public bool Ativo { get; set; } = true;
    }
}
