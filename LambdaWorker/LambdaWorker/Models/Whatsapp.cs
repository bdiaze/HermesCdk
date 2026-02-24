using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorker.Models {
	internal class Whatsapp {
		public required string De { get; set; }
		public required string Para { get; set; }
		public required string NombreTemplate { get; set; }
		public required string Lenguaje { get; set; }
		public string[]? ParametrosTitulo { get; set; }
		public string[]? ParametrosCuerpo { get; set; }
		public string[]? ParametrosBoton { get; set; }
	}
}
