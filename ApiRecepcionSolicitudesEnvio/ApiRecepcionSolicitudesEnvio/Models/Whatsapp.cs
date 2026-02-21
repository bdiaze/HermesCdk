namespace ApiRecepcionSolicitudesEnvio.Models {
	public class Whatsapp {
		public string? De { get; set; }
		public required string Para { get; set; }
		public required string NombreTemplate { get; set; }
		public string? Lenguaje { get; set; } = "es_CL";
		public string[]? ParametrosTitulo { get; set; }
		public string[]? ParametrosCuerpo { get; set; }
		public string[]? ParametrosBoton { get; set; }
	}
}
