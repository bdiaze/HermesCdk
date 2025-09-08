namespace ApiRecepcionSolicitudesEnvio.Models {
    public class Correo {
        public DireccionCorreo? De { get; set; }
        public required List<DireccionCorreo> Para { get; set; }
        public List<DireccionCorreo>? Cc { get; set; }
        public List<DireccionCorreo>? Cco { get; set; } 
        public List<DireccionCorreo>? ResponderA { get; set; }
        public required string Asunto { get; set; }
        public required string Cuerpo { get; set; }
        public List<Adjunto>? Adjuntos { get; set; }
    }
}
