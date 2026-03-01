using LibreriaCompartida.Enums.DynamoDB;

namespace ApiRecepcionSolicitudesEnvio.Models {
	public class SalWhatsappMensaje {
		public required string TenantId { get; set; }
		public required string NumeroTelefono { get; set; }
		public required string WhatsappMessageId { get; set; }
		public required string Direccion { get; set; }
		public required string Tipo { get; set; }
		public string? Cuerpo { get; set; }
		public string? NombreTemplate { get; set; }
		public required string Estado { get; set; }
		public required DateTime FechaCreacion { get; set; }
		public string? RawPayload { get; set; }
	}
}
