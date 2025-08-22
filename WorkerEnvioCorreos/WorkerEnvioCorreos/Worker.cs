using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using WorkerEnvioCorreos.Helpers;
using WorkerEnvioCorreos.Models;

namespace WorkerEnvioCorreos
{
    public class Worker(ILogger<Worker> logger, IAmazonSimpleEmailServiceV2 ses, IAmazonSQS sqs, VariableEntorno variableEntorno, ParameterStoreHelper parameterStore) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Iniciado worker de envio de correos");

            // Obteniendo URL de la cola de correos a procesar...
            string queueUrl = await parameterStore.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_SQS_QUEUE_URL"));
            
            logger.LogInformation("Se obtiene la URL de la cola de correos a procesar");

            // Obteniendo dirección de correos por defecto a usar como remitente...
            DireccionCorreo direccionDeDefecto = JsonSerializer.Deserialize<DireccionCorreo>(await parameterStore.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_DIRECCION_DE_DEFECTO")))!;

            logger.LogInformation("Se obtiene la direccion de correo por defecto a usar como remitente");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Obteniendo límites de SES para configurar cantidad de elementos a extraer de al cola y las esperas entre envíos de correos...
                GetAccountResponse accountResponse = await ses.GetAccountAsync(new GetAccountRequest(), stoppingToken);
                int maxDelayMs = (int) (1000 / accountResponse.SendQuota.MaxSendRate!);
                int cantEmailsDisponibles = (int) (accountResponse.SendQuota.Max24HourSend! - accountResponse.SendQuota.SentLast24Hours!);

                logger.LogInformation("Se obtiene la quota de SES");

                // Si no quedan correos por enviar, se espera una hora y se vuelve a validar si es posible mandarlos...
                if (cantEmailsDisponibles == 0) {
                    logger.LogInformation("Ya se uso la capacidad diaria de SES, se esperara una hora para validar nuevamente");
                    await Task.Delay(60 * 60 * 1000, stoppingToken);
                    continue;
                }

                ReceiveMessageResponse mensajes = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = Math.Min(10, cantEmailsDisponibles),
                    WaitTimeSeconds = 10,
                    VisibilityTimeout = 10,
                }, stoppingToken);

                if (mensajes.Messages == null || mensajes.Messages.Count == 0) {
                    logger.LogInformation("No hay mas correos que procesar, se esperara un minuto");
                    await Task.Delay(60 * 1000, stoppingToken);
                    continue;
                }

                logger.LogInformation("Se comienza el envio de {CantMensajes} correos", mensajes.Messages.Count);

                foreach (Amazon.SQS.Model.Message mensaje in mensajes.Messages) {
                    try {
                        stoppingToken.ThrowIfCancellationRequested();

                        Stopwatch stopwatch = Stopwatch.StartNew();

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

                        SendEmailResponse response = await ses.SendEmailAsync(request, stoppingToken);
                        if (response.HttpStatusCode != HttpStatusCode.OK) {
                            throw new Exception($"Error al enviar correo [SendEmailResponse - Message ID: {response.MessageId} - HttpStatusCode: {response.HttpStatusCode}]");
                        }

                        DeleteMessageResponse deleteResponse = await sqs.DeleteMessageAsync(queueUrl, mensaje.ReceiptHandle, stoppingToken);
                        if (deleteResponse.HttpStatusCode != HttpStatusCode.OK) {
                            throw new Exception($"Error al quitar mensaje de la cola [DeleteMessageResponse - Message ID: {mensaje.MessageId} - HttpStatusCode: {deleteResponse.HttpStatusCode}]");
                        }

                        if (stopwatch.ElapsedMilliseconds < maxDelayMs) {
                            int delayMs = maxDelayMs - (int)stopwatch.ElapsedMilliseconds;
                            await Task.Delay(delayMs, stoppingToken);
                        }
                    } catch(Exception ex) {
                        logger.LogError(ex, "Ocurrio un error al procesar correo {IdMensaje}", mensaje.MessageId);
                    }
                }
            }
        }
    }
}
