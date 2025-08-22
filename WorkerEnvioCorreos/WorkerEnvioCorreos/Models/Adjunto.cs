using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerEnvioCorreos.Models {
    public class Adjunto {
        public required string NombreArchivo { get; set; }
        public required string TipoMime { get; set; }
        public required string ContenidoBase64 { get; set; }
    }
}
