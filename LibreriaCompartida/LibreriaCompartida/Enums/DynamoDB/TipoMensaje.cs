using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibreriaCompartida.Enums.DynamoDB {
	public enum TipoMensaje {
		Texto,
		Template,
		Imagen,
		Video, 
		Audio,
		Documento,
		Ubicacion,
		Contacto,
		Sticker,
		Unknown
	}
}
