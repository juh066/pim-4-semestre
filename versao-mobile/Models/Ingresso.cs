namespace AuroraGaleria.Models
{
    public class Ingresso
    {
        public int Id { get; set; }
        public string NomeVisitante { get; set; } = "";
        public int Quantidade { get; set; }
        public DateTime DataVisita { get; set; }
    }
}