using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorkerEnvioCorreos.Models {
    internal class Correo {
        public DireccionCorreo? De { get; set; } = null;
        public required List<DireccionCorreo> Para { get; set; }
        public List<DireccionCorreo>? Cc { get; set; } = null;
        public List<DireccionCorreo>? Cco { get; set; } = null;
        public List<DireccionCorreo>? ResponderA { get; set; } = null;
        public required string Asunto { get; set; }
        public required string Cuerpo { get; set; }
        public List<Adjunto>? Adjuntos { get; set; } = null;
    }
}
