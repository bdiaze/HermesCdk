using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using LambdaWorkerEnvioCorreos.Helpers;
using LambdaWorkerEnvioCorreos.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
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
            services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
            services.AddSingleton<IAmazonSimpleEmailServiceV2, AmazonSimpleEmailServiceV2Client>();

            #endregion

            #region Singleton Helpers
            services.AddSingleton<VariableEntornoHelper>();
            services.AddSingleton<ParameterStoreHelper>();
            #endregion
        });

        var app = builder.Build();

        serviceProvider = app.Services;
    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Stopwatch stopwatchDelay = Stopwatch.StartNew();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - " +
            $"Iniciado worker de envio de correos.");

        VariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<VariableEntornoHelper>();
        ParameterStoreHelper parameterStore = serviceProvider.GetRequiredService<ParameterStoreHelper>();
        IAmazonSQS sqsClient = serviceProvider.GetRequiredService<IAmazonSQS>();
        IAmazonSimpleEmailServiceV2 sesClient = serviceProvider.GetRequiredService<IAmazonSimpleEmailServiceV2>();

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendran los parametros necesarios para procesar los mensajes.");

        // Obteniendo URL de la cola de correos a procesar...
        string nombreAplicacion = variableEntorno.Obtener("APP_NAME");
        string queueUrl = await parameterStore.ObtenerParametro($"/{nombreAplicacion}/SQS/QueueUrl");

        // Obteniendo dirección de correos por defecto a usar como remitente...
        DireccionCorreo direccionDeDefecto = JsonSerializer.Deserialize<DireccionCorreo>(await parameterStore.ObtenerParametro($"/{nombreAplicacion}/SES/DireccionDeDefecto"))!;

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se obtendra la quota disponible de SES.");

        // Obteniendo límites de SES para configurar cantidad de elementos a extraer de al cola y las esperas entre envíos de correos...
        GetAccountResponse accountResponse = await sesClient.GetAccountAsync(new GetAccountRequest());
        int maxDelayMs = (int)(1000 / accountResponse.SendQuota.MaxSendRate!);
        int cantEmailsDisponibles = (int)(accountResponse.SendQuota.Max24HourSend! - accountResponse.SendQuota.SentLast24Hours!);

        // Si no quedan correos por enviar, se espera una hora y se vuelve a validar si es posible mandarlos...
        if (cantEmailsDisponibles == 0) {
            LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Ya se uso la capacidad diaria de SES, no se procesarán los mensajes.");

            return;
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Se comienza el envio de {evnt.Records.Count} correos");

        foreach (SQSMessage mensaje in evnt.Records) {
            try {
                Correo correo = JsonSerializer.Deserialize<Correo>(mensaje.Body)!;
                correo.De ??= direccionDeDefecto;

                List<Attachment>? attachments = null;
                if (correo.Adjuntos != null && correo.Adjuntos.Count > 0) {
                    attachments = [];
                    foreach (Adjunto adjunto in correo.Adjuntos) {
                        attachments.Add(new Attachment {
                            FileName = adjunto.NombreArchivo,
                            ContentType = adjunto.TipoMime,
                            RawContent = new MemoryStream(Convert.FromBase64String(adjunto.ContenidoBase64))
                        });
                    }
                }

                SendEmailRequest request = new() {
                    FromEmailAddress = correo.De.ToString(),
                    Destination = new Destination {
                        ToAddresses = [.. correo.Para.Select(c => c.ToString())],
                        CcAddresses = correo.Cc?.Select(c => c.ToString()).ToList(),
                        BccAddresses = correo.Cco?.Select(c => c.ToString()).ToList()
                    },
                    ReplyToAddresses = correo.ResponderA?.Select(c => c.ToString()).ToList(),
                    Content = new EmailContent {
                        Simple = new Amazon.SimpleEmailV2.Model.Message {
                            Subject = new Content {
                                Charset = "UTF-8",
                                Data = correo.Asunto
                            },
                            Body = new Body {
                                Html = new Content {
                                    Charset = "UTF-8",
                                    Data = correo.Cuerpo
                                }
                            },
                            Attachments = attachments
                        }
                    }
                };

                if (stopwatchDelay.ElapsedMilliseconds < maxDelayMs) {
                    int delayMs = maxDelayMs - (int)stopwatchDelay.ElapsedMilliseconds;
                    await Task.Delay(delayMs);
                }

                SendEmailResponse response = await sesClient.SendEmailAsync(request);
                if (response.HttpStatusCode != HttpStatusCode.OK) {
                    throw new Exception($"Error al enviar correo [SendEmailResponse - Message ID: {response.MessageId} - HttpStatusCode: {response.HttpStatusCode}]");
                }

                stopwatchDelay = Stopwatch.StartNew();

                DeleteMessageResponse deleteResponse = await sqsClient.DeleteMessageAsync(queueUrl, mensaje.ReceiptHandle);
                if (deleteResponse.HttpStatusCode != HttpStatusCode.OK) {
                    throw new Exception($"Error al quitar mensaje de la cola [DeleteMessageResponse - Message ID: {mensaje.MessageId} - HttpStatusCode: {deleteResponse.HttpStatusCode}]");
                }
            } catch (Exception ex) {
                LambdaLogger.Log(LogLevel.Error,
                    $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
                    $"Ocurrio un error al procesar correo {mensaje.MessageId}. " +
                    $"{ex}");
            }
        }

        LambdaLogger.Log(
            $"[Function] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
            $"Termino exitosamente el envio de correos.");

    }
}
