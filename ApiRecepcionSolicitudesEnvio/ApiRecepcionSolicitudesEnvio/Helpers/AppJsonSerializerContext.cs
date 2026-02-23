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
	[JsonSerializable(typeof(Dictionary<string, string>))]
	[JsonSerializable(typeof(Whatsapp))]
    [JsonSerializable(typeof(WhatsappWebhook))]
	[JsonSerializable(typeof(Entry))]
	[JsonSerializable(typeof(Change))]
	[JsonSerializable(typeof(Value))]
	[JsonSerializable(typeof(Message))]
	[JsonSerializable(typeof(Text))]
	[JsonSerializable(typeof(Button))]
	[JsonSerializable(typeof(Estado))]
	[JsonSerializable(typeof(Error))]
	[JsonSerializable(typeof(List<Error>))]
	internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
