using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiRecepcionSolicitudesEnvio.Enums.DynamoDB {
	public enum TipoMensaje {
		Texto = 1,
		Template = 2,
		Boton = 3,
		Imagen = 4,
		Documento = 5
	}
}
