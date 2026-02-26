using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiRecepcionSolicitudesEnvio.Enums.DynamoDB {
	public enum EstadoMensaje {
		Recibido = 1,
		Enviado = 2,
		ConfirmacionEnvio = 3,
		Entregado = 4,
		Leido = 5,
		Fallido = 6
	}
}
