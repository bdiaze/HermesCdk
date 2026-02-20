namespace ApiRecepcionSolicitudesEnvio.Models {
    public class Retorno { 
        public required string IdMensaje { get; set; }

        public string QueueMessageId { get; set; } = "";  
    }
}
