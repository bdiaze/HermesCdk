using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiRecepcionSolicitudesEnvio.Enums.DynamoDB {
	public enum EstadoConversacion {
		Abierto = 1,
		Cerrado = 2,
		Archivado = 3
	}
}
