using LibreriaCompartida.Enums.DynamoDB;

namespace ApiRecepcionSolicitudesEnvio.Models {
	public class SalWhatsappMensaje {
		public required string TenantId { get; set; }
		public required string NumeroTelefono { get; set; }
		public required string WhatsappMessageId { get; set; }
		public DireccionMensaje Direccion { get; set; }
		public TipoMensaje Tipo { get; set; }
		public string? Cuerpo { get; set; }
		public string? NombreTemplate { get; set; }
		public EstadoMensaje Estado { get; set; }
		public DateTime FechaCreacion { get; set; }
		public string? RawPayload { get; set; }
	}
}
