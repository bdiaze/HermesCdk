using System;
using System.Collections.Generic;
using System.Text;

namespace LibreriaCompartida.Models {
	public class WhatsappRespuestaAutomatica {
		public required string TenantId { get; set; }
		public required string NombreTemplate { get; set; }
	}
}
