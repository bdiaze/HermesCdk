using Amazon.Lambda.APIGatewayEvents;
using ApiRecepcionSolicitudesEnvio.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace ApiRecepcionSolicitudesEnvio.Helpers {

    [JsonSerializable(typeof(APIGatewayProxyRequest))]
    [JsonSerializable(typeof(APIGatewayProxyResponse))]
    [JsonSerializable(typeof(ProblemDetails))]
    [JsonSerializable(typeof(Correo))]
    [JsonSerializable(typeof(DireccionCorreo))]
    [JsonSerializable(typeof(Adjunto))]
    [JsonSerializable(typeof(Retorno))]
	[JsonSerializable(typeof(Dictionary<string, object>))]
	internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
