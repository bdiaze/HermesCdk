using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerEnvioCorreos.Models {
    public class DireccionCorreo {
        public string? Nombre { get; set; }
        public required string Correo { get; set; }
        public override string ToString() {
            if (Nombre != null) {
                return $"\"{Nombre}\" <{Correo}>";
            }
            return Correo;
        }
    }
}
