using Amazon.Lambda.APIGatewayEvents;
using ApiRecepcionSolicitudesEnvio.Models;
using System.Text.Json.Serialization;

namespace ApiRecepcionSolicitudesEnvio.Helpers {

    [JsonSerializable(typeof(Correo))]
    [JsonSerializable(typeof(DireccionCorreo))]
    [JsonSerializable(typeof(Retorno))]
    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
