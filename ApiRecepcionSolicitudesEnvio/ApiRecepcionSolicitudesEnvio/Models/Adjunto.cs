namespace ApiRecepcionSolicitudesEnvio.Models {
    public class Adjunto {
        public required string NombreArchivo { get; set; } 
        public required string TipoMime { get; set; } 
        public required string ContenidoBase64 { get; set; }            
    }
}
