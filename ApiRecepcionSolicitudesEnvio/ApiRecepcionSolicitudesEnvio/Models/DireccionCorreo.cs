namespace ApiRecepcionSolicitudesEnvio.Models {
    public record DireccionCorreo(string? Nombre, string Correo) {
        public override string ToString() {
            if (Nombre != null) return $"\"{Nombre}\" <{Correo}>";
            return Correo;
        }
    };
}
