using LibreriaCompartida.Enums.DynamoDB;

namespace ApiRecepcionSolicitudesEnvio.Models {
	public class SalWhatsappConversacion {
		public required string TenantId { get; set; }
		public required string NumeroTelefono { get; set; }
		public DateTime FechaUltimoMensaje { get; set; }
		public string? PreviewUltimoMensaje { get; set; }
		public int CantidadNoLeidos { get; set; }
		public required string Estado { get; set; }
		public DateTime? FechaUltimaEntrada { get; set; }
		public DateTime? PuedeResponderGratuitoHasta { get; set; }
	}
}
