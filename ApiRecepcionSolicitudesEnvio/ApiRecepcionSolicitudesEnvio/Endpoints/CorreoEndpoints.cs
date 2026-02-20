using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ApiRecepcionSolicitudesEnvio.Endpoints {
    public static class CorreoEndpoints {
        public static IEndpointRouteBuilder MapCorreoEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Correo");
            group.MapEnviarEndpoint();

            return routes;
        }

        private static IEndpointRouteBuilder MapEnviarEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/Enviar", async (Correo correo, IAmazonSQS sqsClient, VariableEntornoHelper variableEntorno, DynamoHelper dynamo) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
                    // Se genera un ID único...
                    string idMensaje = Guid.NewGuid().ToString();
                    while ((await dynamo.Obtener(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), new Dictionary<string, object?> { ["IdMensaje"] = idMensaje })) != null) {
						idMensaje = Guid.NewGuid().ToString();
					}

					// Se serializa el contenido del mensaje...
					string jsonCorreo = JsonSerializer.Serialize(correo, AppJsonSerializerContext.Default.Correo);

                    // Se ingresa a DynamoDB...
					Dictionary<string, object?>? itemDynamo = new() {
						["IdMensaje"] = idMensaje,
						["TipoMensaje"] = "Email",
						["Estado"] = "Pendiente",
						["Contenido"] = jsonCorreo,
						["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
					};
					await dynamo.Insertar(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), itemDynamo);

                    // Se ingresa a cola de envío...
					SendMessageRequest request = new() {
                        QueueUrl = variableEntorno.Obtener("SQS_QUEUE_URL"),
                        MessageBody = (string)itemDynamo["IdMensaje"]!
					};
					SendMessageResponse response = await sqsClient.SendMessageAsync(request);

                    // Se actualiza el ítem en DynamoDB...
                    itemDynamo["Estado"] = "InsertadoColaEnvio";
					itemDynamo.Add("QueueMessageId", response.MessageId);
					await dynamo.Insertar(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), itemDynamo);

					Retorno salida = new() { 
                        IdMensaje = (string)itemDynamo["IdMensaje"]!,
					};

                    LambdaLogger.Log(
                        $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Correo ingresado exitosamente a la cola de envio - QueueMessageId: {salida.QueueMessageId}.");

                    return Results.Ok(salida);
                } catch (Exception ex) {
                    LambdaLogger.Log(
                        $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                        $"Ocurrio un error al ingresar el correo a la cola de envio. " +
                        $"{ex}");

                    return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
                }
            });

            return routes;
        }
    }
}
