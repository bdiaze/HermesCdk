using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using System.Diagnostics;
using System.Text.Json;

namespace ApiRecepcionSolicitudesEnvio.Endpoints {
    public static class CorreoEndpoints {
        public static IEndpointRouteBuilder MapCorreoEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Correo");
            group.MapEnviarEndpoint();

            return routes;
        }

        private static IEndpointRouteBuilder MapEnviarEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/Enviar", async (Correo correo, IAmazonSQS sqsClient, VariableEntornoHelper variableEntorno, ParameterStoreHelper parameterStore) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
                    string jsonCorreo = JsonSerializer.Serialize(correo, AppJsonSerializerContext.Default.Correo);

                    SendMessageRequest request = new() {
                        QueueUrl = variableEntorno.Obtener("SQS_QUEUE_URL"),
                        MessageBody = jsonCorreo
                    };

                    SendMessageResponse response = await sqsClient.SendMessageAsync(request);
                    Retorno salida = new() { 
                        QueueMessageId = response.MessageId 
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
