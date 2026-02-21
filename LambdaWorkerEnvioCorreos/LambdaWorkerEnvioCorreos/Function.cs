using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Amazon.SimpleSystemsManagement;
using LambdaWorkerEnvioCorreos.Helpers;
using LambdaWorkerEnvioCorreos.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using static Amazon.Lambda.SQSEvents.SQSBatchResponse;
using static Amazon.Lambda.SQSEvents.SQSEvent;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaWorkerEnvioCorreos;

public class Function
{
    private readonly IServiceProvider serviceProvider;

    public Function() {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices((context, services) => {
            #region Singleton AWS Services
            services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
            services.AddSingleton<IAmazonSimpleEmailServiceV2, AmazonSimpleEmailServiceV2Client>();
			services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();

			#endregion

			#region Singleton Helpers
			services.AddSingleton<VariableEntornoHelper>();
			services.AddSingleton<DynamoHelper>();
			#endregion
		});

        var app = builder.Build();

        serviceProvider = app.Services;
    }

    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        List<BatchItemFailure> listaMensajesError = [];

        Stopwatch stopwatch = Stopwatch.StartNew();
        Stopwatch stopwatchDelayCorreos = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Iniciado worker de envio de correos.");

        VariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<VariableEntornoHelper>();
		DynamoHelper dynamo = serviceProvider.GetRequiredService<DynamoHelper>();
		IAmazonSimpleEmailServiceV2 sesClient = serviceProvider.GetRequiredService<IAmazonSimpleEmailServiceV2>();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendran los parametros necesarios para procesar los mensajes.");

        // Obteniendo URL de la cola de correos a procesar...
        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

        // Obteniendo dirección de correos por defecto a usar como remitente...
        DireccionCorreo direccionDeDefecto = new() {
            Nombre = variableEntorno.Obtener("SES_NOMBRE_DE_DEFECTO"),
            Correo = variableEntorno.Obtener("SES_CORREO_DE_DEFECTO"),
        };

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendra la quota disponible de SES.");

        // Obteniendo límites de SES para configurar las esperas entre envíos de correos...
        GetAccountResponse accountResponse = await sesClient.GetAccountAsync(new GetAccountRequest());
        int maxDelayMsCorreos = (int)(1000 / accountResponse.SendQuota.MaxSendRate!);
        int cantEmailsDisponibles = (int)(accountResponse.SendQuota.Max24HourSend! - accountResponse.SendQuota.SentLast24Hours!);

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se comienza el envio de {evnt.Records.Count} correos");

        foreach (SQSMessage mensaje in evnt.Records) {
            try {
                string idMensaje = mensaje.Body;
                Dictionary<string, object?> itemDynamo = await dynamo.Obtener(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), new Dictionary<string, object?> {
                    ["IdMensaje"] = idMensaje
				}) ?? throw new Exception("No se encontró el mensaje");

                if (!itemDynamo.TryGetValue("Estado", out object? estado) || estado == null || (string)estado != "InsertadoColaEnvio") {
                    throw new Exception("El estado del mensaje es inválido");
                }

                if (!itemDynamo.TryGetValue("Contenido", out object? contenido) || contenido == null) {
                    throw new Exception("El mensaje no incluye un contenido");
                }

                if (!itemDynamo.TryGetValue("TipoMensaje", out object? tipoMensaje) || tipoMensaje == null) {
                    throw new Exception("El mensaje no incluye el tipo de mensaje");
                }

                switch ((string)tipoMensaje) {
                    case "Email":
                        if (cantEmailsDisponibles <= 0) {
                            throw new Exception("Ya se uso la capacidad diaria de SES");
                        }

						Correo correo = JsonSerializer.Deserialize<Correo>((string)contenido)!;
						correo.De ??= direccionDeDefecto;

						List<Attachment>? attachments = null;
						if (correo.Adjuntos != null && correo.Adjuntos.Count > 0) {
							attachments = [];
							foreach (Adjunto adjunto in correo.Adjuntos) {
								attachments.Add(new Attachment {
									FileName = CaracteresEspeciales.Limpiar(adjunto.NombreArchivo),
									ContentType = CaracteresEspeciales.Limpiar(adjunto.TipoMime),
									RawContent = new MemoryStream(Convert.FromBase64String(CaracteresEspeciales.LimpiarBase64(adjunto.ContenidoBase64)))
								});
							}
						}

						SendEmailRequest request = new() {
							FromEmailAddress = CaracteresEspeciales.Limpiar(correo.De.ToString()),
							Destination = new Destination {
								ToAddresses = [.. correo.Para.Select(c => CaracteresEspeciales.Limpiar(c.ToString()))],
								CcAddresses = correo.Cc?.Select(c => CaracteresEspeciales.Limpiar(c.ToString())).ToList(),
								BccAddresses = correo.Cco?.Select(c => CaracteresEspeciales.Limpiar(c.ToString())).ToList()
							},
							ReplyToAddresses = correo.ResponderA?.Select(c => CaracteresEspeciales.Limpiar(c.ToString())).ToList(),
							Content = new EmailContent {
								Simple = new Message {
									Subject = new Content {
										Charset = "UTF-8",
										Data = CaracteresEspeciales.Limpiar(correo.Asunto)
									},
									Body = new Body {
										Html = new Content {
											Charset = "UTF-8",
											Data = CaracteresEspeciales.LimpiarExceptoSaltos(correo.Cuerpo)
										}
									},
									Attachments = attachments
								}
							}
						};

						if (stopwatchDelayCorreos.ElapsedMilliseconds < maxDelayMsCorreos) {
							int delayMs = maxDelayMsCorreos - (int)stopwatchDelayCorreos.ElapsedMilliseconds;
							await Task.Delay(delayMs);
						}

						SendEmailResponse response = await sesClient.SendEmailAsync(request);
						if (response.HttpStatusCode != HttpStatusCode.OK) {
							throw new Exception($"Error al enviar correo [SendEmailResponse - Message ID: {response.MessageId} - HttpStatusCode: {response.HttpStatusCode}]");
						}

                        cantEmailsDisponibles--;
						stopwatchDelayCorreos = Stopwatch.StartNew();

						itemDynamo["Estado"] = "CorreoEnviado";
						itemDynamo.Add("FechaEnvio", DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture));
						itemDynamo.Add("SESMessageId", response.MessageId);
						await dynamo.Insertar(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), itemDynamo);

						break;
                    default:
                        throw new Exception("Tipo de mensaje no soportado");
				}
            } catch (Exception ex) {
                LambdaLogger.Log(LogLevel.Error,
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al procesar correo {mensaje.MessageId}. " +
                    $"{ex}");

                listaMensajesError.Add(new BatchItemFailure {
                    ItemIdentifier = mensaje.MessageId,
                });
            }
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Termino exitosamente el envio de correos - Casos con error: {listaMensajesError.Count}.");

        return new SQSBatchResponse {
            BatchItemFailures = listaMensajesError
        };
    }
}
