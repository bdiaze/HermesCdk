namespace ApiRecepcionSolicitudesEnvio.Models {
    public record Correo(
        DireccionCorreo? De, 
        List<DireccionCorreo> Para, 
        List<DireccionCorreo> Cc, 
        List<DireccionCorreo>? Cco, 
        List<DireccionCorreo>? ResponderA, 
        string Asunto, 
        string Cuerpo,
        List<Adjunto>? Adjuntos
    );
}
