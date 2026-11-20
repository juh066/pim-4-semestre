using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class Exposicao
    {
        public int Id { get; set; }

        [Required, MaxLength(140)]
        public string Titulo { get; set; } = "";

        [MaxLength(1000)]
        public string Descricao { get; set; } = "";

        [Required, MaxLength(300)]
        public string ImagemUrl { get; set; } = "";

        [Required, MaxLength(80)]
        public string Periodo { get; set; } = "";

        public DateTime Inicio { get; set; }

        public DateTime Fim { get; set; }

        public bool Ativa { get; set; } = true;

        public ICollection<VisitSession> Sessoes { get; set; } = new List<VisitSession>();
    }
}
