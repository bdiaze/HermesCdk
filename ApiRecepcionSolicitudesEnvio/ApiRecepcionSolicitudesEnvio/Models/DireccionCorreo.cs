namespace ApiRecepcionSolicitudesEnvio.Models {
    public class DireccionCorreo {
        public string? Nombre { get; set; } 
        public required string Correo { get; set; }
        
        public override string ToString() {
            if (Nombre != null) return $"\"{Nombre}\" <{Correo}>";
            return Correo;
        }
    }
}
