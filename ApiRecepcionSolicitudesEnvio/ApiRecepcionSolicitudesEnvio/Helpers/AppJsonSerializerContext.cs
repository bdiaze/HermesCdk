using Amazon.Lambda.APIGatewayEvents;
using ApiRecepcionSolicitudesEnvio.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using LibreriaCompartida.Models;
using LibreriaCompartida.Entities.DynamoDB;

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
	[JsonSerializable(typeof(SalWhatsappMedia))]
	[JsonSerializable(typeof(WhatsappWebhook))]
	[JsonSerializable(typeof(WhatsappResponse))]
	[JsonSerializable(typeof(WhatsappMediaResponse))]
	[JsonSerializable(typeof(List<SalWhatsappConversacion>))]
	[JsonSerializable(typeof(List<SalWhatsappMensaje>))]
	[JsonSerializable(typeof(Entry))]
	[JsonSerializable(typeof(Change))]
	[JsonSerializable(typeof(Value))]
	[JsonSerializable(typeof(Metadata))]
	[JsonSerializable(typeof(Message))]
	[JsonSerializable(typeof(Text))]
	[JsonSerializable(typeof(MediaInfo))]
	[JsonSerializable(typeof(Location))]
	[JsonSerializable(typeof(Contact))]
	[JsonSerializable(typeof(ContactName))]
	[JsonSerializable(typeof(ContactPhone))]
	[JsonSerializable(typeof(Button))]
	[JsonSerializable(typeof(Estado))]
	[JsonSerializable(typeof(Error))]
	[JsonSerializable(typeof(List<Error>))]
	internal partial class AppJsonSerializerContext : JsonSerializerContext {

    }
}
