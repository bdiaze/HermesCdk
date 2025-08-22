using ApiRecepcionSolicitudesEnvio.Models;
using System.Text.Json.Serialization;

namespace ApiRecepcionSolicitudesEnvio.Helpers {

    [JsonSerializable(typeof(Correo))]
    [JsonSerializable(typeof(DireccionCorreo))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
