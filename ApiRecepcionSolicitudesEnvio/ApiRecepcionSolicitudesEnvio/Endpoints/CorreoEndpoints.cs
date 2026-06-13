using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiRecepcionSolicitudesEnvio.Endpoints {
    public static partial class CorreoEndpoints {
		private static readonly Regex EmailValidator = EmailRegex();

		public static IEndpointRouteBuilder MapCorreoEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/Correo");
            group.MapEnviarEndpoint();

            return routes;
        }

		private static IEndpointRouteBuilder MapEnviarEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapPost("/Enviar", async (Correo correo, HttpContext httpContext, IAmazonSQS sqsClient, VariableEntornoHelper variableEntorno, DynamoHelper dynamo) => {
                Stopwatch stopwatch = Stopwatch.StartNew();

                try {
					#region Validaciones
					Dictionary<string, string[]> errores = [];

					// Se valida que los correos vengan con formato correcto...
                    if (correo.De != null && !EmailValidator.IsMatch(correo.De.Correo)) {
						errores.Add(nameof(correo.De), ["Correo 'De' es inválido"]);
                    }

                    foreach (DireccionCorreo para in correo.Para) {
                        if (!EmailValidator.IsMatch(para.Correo)) {
							errores.Add(nameof(correo.Para), ["Correo 'Para' es inválido"]);
						}
                    }

                    if (correo.Cc != null) {
                        foreach (DireccionCorreo cc in correo.Cc) {
							if (!EmailValidator.IsMatch(cc.Correo)) {
								errores.Add(nameof(correo.Cc), ["Correo 'Cc' es inválido"]);
							}
						}
                    }

					if (correo.Cco != null) {
						foreach (DireccionCorreo cco in correo.Cco) {
							if (!EmailValidator.IsMatch(cco.Correo)) {
								errores.Add(nameof(correo.Cco), ["Correo 'Cco' es inválido"]);
							}
						}
					}

					if (correo.ResponderA != null) {
						foreach (DireccionCorreo responderA in correo.ResponderA) {
							if (!EmailValidator.IsMatch(responderA.Correo)) {
								errores.Add(nameof(correo.ResponderA), ["Correo 'ResponderA' es inválido"]);
							}
						}
					}

					if (errores.Count > 0) {
						LambdaLogger.Log(
							$"[POST] - [/Correo/Enviar] - [{Activity.Current?.Id}] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
							$"Entrada inválida - Entrada: {JsonSerializer.Serialize(correo, AppJsonSerializerContext.Default.Correo)} - " +
							$"Errores {JsonSerializer.Serialize(errores, AppJsonSerializerContext.Default.DictionaryStringStringArray)}");

						return Results.ValidationProblem(errores);
					}
					#endregion

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
                        QueueUrl = variableEntorno.Obtener("EMAIL_SQS_QUEUE_URL"),
                        MessageBody = (string)itemDynamo["IdMensaje"]!
					};
					SendMessageResponse response = await sqsClient.SendMessageAsync(request);

					// Se actualiza el ítem en DynamoDB...
					await dynamo.ActualizarCampos(
						variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
						new Dictionary<string, object?> { ["IdMensaje"] = (string)itemDynamo["IdMensaje"]! },
						"SET Estado = :Estado, QueueMessageId = :QueueMessageId",
						"attribute_exists(IdMensaje)",
						new Dictionary<string, object> {
							{ ":Estado", "InsertadoColaEnvio" },
							{ ":QueueMessageId", response.MessageId },
						}
					);

					Retorno salida = new() { 
                        IdMensaje = (string)itemDynamo["IdMensaje"]!,
					};

                    LambdaLogger.Log(
                        $"[POST] - [/Correo/Enviar] - [{Activity.Current?.Id}] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                        $"Correo ingresado exitosamente - ID Mensaje: {salida.IdMensaje}.");

                    return Results.Ok(salida);
                } catch (Exception ex) {
                    LambdaLogger.Log(
                        $"[POST] - [/Correo/Enviar] - [{Activity.Current?.Id}] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                        $"Ocurrio un error al ingresar el correo. " +
                        $"{ex}");

                    return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
                }
            });

            return routes;
        }

		[GeneratedRegex(@"^[a-zA-Z0-9.+\-_']+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
		private static partial Regex EmailRegex();
	}
}
