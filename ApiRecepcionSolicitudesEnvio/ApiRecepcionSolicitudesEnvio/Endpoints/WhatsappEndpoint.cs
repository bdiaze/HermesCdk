using Amazon.Lambda.Core;
using Amazon.Runtime.Internal.Transform;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using LibreriaCompartida.Entities.DynamoDB;
using LibreriaCompartida.Enums.DynamoDB;
using LibreriaCompartida.Helpers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApiRecepcionSolicitudesEnvio.Endpoints {
	public static class WhatsappEndpoint {
		public static IEndpointRouteBuilder MapWhatsappEndpoints(this IEndpointRouteBuilder routes) {
			RouteGroupBuilder group = routes.MapGroup("/Whatsapp");
			group.MapEnviarEndpoint();
			group.MapObtenerMediaEndpoint();
			group.MapObtenerConversaciones();

			RouteGroupBuilder publicGroup = routes.MapGroup("/public/Whatsapp");
			publicGroup.MapWebhookGetEndpoint();
			publicGroup.MapWebhookPostEndpoint();

			return routes;
		}

		private static IEndpointRouteBuilder MapEnviarEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapPost("/Enviar", async (Whatsapp whatsapp, IAmazonSQS sqsClient, VariableEntornoHelper variableEntorno, DynamoHelper dynamo) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					// Se genera un ID único...
					string idMensaje = Guid.NewGuid().ToString();
					while ((await dynamo.Obtener(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), new Dictionary<string, object?> { ["IdMensaje"] = idMensaje })) != null) {
						idMensaje = Guid.NewGuid().ToString();
					}

					// Se serializa el contenido del mensaje...
					string jsonContenido = JsonSerializer.Serialize(whatsapp, AppJsonSerializerContext.Default.Whatsapp);

					// Se ingresa a DynamoDB...
					Dictionary<string, object?>? itemDynamo = new() {
						["IdMensaje"] = idMensaje,
						["TipoMensaje"] = "Whatsapp",
						["Estado"] = "Pendiente",
						["Contenido"] = jsonContenido,
						["FechaCreacion"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture),
					};

					await dynamo.Insertar(variableEntorno.Obtener("DYNAMODB_TABLE_NAME"), itemDynamo);

					// Se ingresa a cola de envío...
					SendMessageRequest request = new() {
						QueueUrl = variableEntorno.Obtener("WHATSAPP_SQS_QUEUE_URL"),
						MessageBody = (string)itemDynamo["IdMensaje"]!
					};
					SendMessageResponse response = await sqsClient.SendMessageAsync(request);

					// Se actualiza el ítem en DynamoDB...
					await dynamo.ActualizarCampos(
						variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
						new Dictionary<string, object?> { ["IdMensaje"] = (string)itemDynamo["IdMensaje"]! },
						"SET Estado = :Estado, QueueMessageId = :QueueMessageId",
						null,
						new Dictionary<string, object> {
							{ ":Estado", "InsertadoColaEnvio" },
							{ ":QueueMessageId", response.MessageId },
						}
					);

					Retorno salida = new() {
						IdMensaje = (string)itemDynamo["IdMensaje"]!,
					};

					LambdaLogger.Log(
						$"[POST] - [/Whatsapp/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Mensaje de Whatsapp ingresado exitosamente - ID Mensaje: {salida.IdMensaje}.");

					return Results.Ok(salida);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[POST] - [/Whatsapp/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al ingresar el mensaje de Whatsapp. " +
						$"{ex}");

					return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
				}
			});

			return routes;

		}

		private static IEndpointRouteBuilder MapObtenerMediaEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Media/{whatsappMessageId}", async (string whatsappMessageId, VariableEntornoHelper variableEntorno, ConversacionHelper conversacionHelper, WhatsappHelper whatsappHelper, S3Helper s3Helper) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					ConversacionMensaje? mensaje = await conversacionHelper.ObtenerMensajePorId(whatsappMessageId);
					if (mensaje == null || mensaje.RawPayload == null) {
						LambdaLogger.Log(
							$"[GET] - [/Whatsapp/Media] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
							$"Message ID de Whatsapp inválido.");

						return Results.BadRequest("Message ID de Whatsapp inválido.");
					}
					Models.Message raw = JsonSerializer.Deserialize(mensaje.RawPayload, AppJsonSerializerContext.Default.Message)!;

					MediaInfo mediaInfo = mensaje.Tipo switch {
						TipoMensaje.Imagen => raw.Image!,
						TipoMensaje.Video => raw.Video!,
						TipoMensaje.Audio => raw.Audio!,
						TipoMensaje.Documento => raw.Document!,
						TipoMensaje.Sticker => raw.Sticker!,
						_ => throw new Exception($"No se puede obtener media de un mensaje de tipo {mensaje.Tipo}")
					};

					string bucketName = variableEntorno.Obtener("BUCKET_NAME_WHATSAPP_MEDIA");

					if (!await s3Helper.ExisteBucketObject(bucketName, mediaInfo.Id)) {
						HttpResponseMessage responseGetMedia = await whatsappHelper.ObtenerMedia(mediaInfo.Id);
						Stream stream = await responseGetMedia.Content.ReadAsStreamAsync();
						string contentType = responseGetMedia.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
						try {
							await s3Helper.PutObjectStream(
								bucketName,
								mediaInfo.Id,
								stream,
								contentType
							);
						} finally {
							await stream.DisposeAsync();
							responseGetMedia.Dispose();
						}
					}

					string presignedUrl = await s3Helper.ObtenerGetPreSignedUrl(bucketName, mediaInfo.Id, mediaInfo.Filename ?? mediaInfo.Id);

					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/Media] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Media del mensaje de Whatsapp ID {whatsappMessageId} reenviado exitosamente.");

					return Results.Ok(new SalWhatsappMedia {
						Url = presignedUrl,
					});
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/Media] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al reenviar media del mensaje de Whatsapp - ID: {whatsappMessageId}. " +
						$"{ex}");

					return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
				}
			});

			return routes;

		}


		private static IEndpointRouteBuilder MapObtenerConversaciones(this IEndpointRouteBuilder routes) {
			routes.MapGet("/Conversaciones/{tenantId}/{desde?}/{hasta?}", async (string tenantId, DateTime? desde, DateTime? hasta, VariableEntornoHelper variableEntorno, ConversacionHelper conversacionHelper) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					List<ConversacionMetadata> retorno = await conversacionHelper.ObtenerConversacionesMetadata(tenantId, desde, hasta);

					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/ObtenerMetadataConversaciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Se obtiene exitosamente la metadata de las conversaciones de Whatsapp - Tenant ID: {tenantId} - Cant. Conversaciones: {retorno.Count}.");

					return Results.Ok(retorno);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/ObtenerMetadataConversaciones] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error al obtener la metadata de las conversaciones de Whatsapp - Tenant ID: {tenantId}. " +
						$"{ex}");

					return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
				}
			});

			return routes;

		}

		private static IEndpointRouteBuilder MapWebhookGetEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapGet("/webhook", async ([FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.verify_token")] string verifyToken, [FromQuery(Name = "hub.challenge")] string challenge, VariableEntornoHelper variableEntorno, SecretManagerHelper secretManagerHelper) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					Dictionary<string, string> secretApp = JsonSerializer.Deserialize(
						await secretManagerHelper.ObtenerSecreto(variableEntorno.Obtener("SECRET_ARN_APP")),
						AppJsonSerializerContext.Default.DictionaryStringString
					)!;

					if (mode != "subscribe" || verifyToken != secretApp["WhatsappVerifyToken"]) {
						LambdaLogger.Log(
							$"[GET] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status401Unauthorized}] - " +
							$"Verify Token inválido.");

						return Results.Unauthorized();
					}

					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Ejecución correcta del webhook de Whatsapp.");

					return Results.Text(challenge);
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[GET] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error en la ejecución del webhook de Whatsapp. " +
						$"{ex}");

					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static IEndpointRouteBuilder MapWebhookPostEndpoint(this IEndpointRouteBuilder routes) {
			routes.MapPost("/webhook", async (HttpRequest request, [FromHeader(Name = "X-Hub-Signature-256")] string xHubSignature256, VariableEntornoHelper variableEntorno, SecretManagerHelper secretManagerHelper, DynamoHelper dynamo, ConversacionHelper conversacionHelper) => {
				Stopwatch stopwatch = Stopwatch.StartNew();

				try {
					// Se valida que venga la cabecera con signature...
					if (string.IsNullOrEmpty(xHubSignature256)) {
						LambdaLogger.Log(
							$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status401Unauthorized}] - " +
							$"No se incluye header X-Hub-Signature-256.");
						
						return Results.Unauthorized();
					}

					Dictionary<string, string> secretApp = JsonSerializer.Deserialize(
						await secretManagerHelper.ObtenerSecreto(variableEntorno.Obtener("SECRET_ARN_APP")),
						AppJsonSerializerContext.Default.DictionaryStringString
					)!;

					request.EnableBuffering();
					using StreamReader reader = new(request.Body);
					string cuerpo = await reader.ReadToEndAsync();
					request.Body.Position = 0;

					// Se valida que la signature esté correcta...
					string expectedSignature = ComputeHmacSha256(cuerpo, secretApp["WhatsappAppSecret"]);
					string receivedSignature = xHubSignature256.Replace("sha256=", "");
					if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(receivedSignature))) {
						LambdaLogger.Log(
							$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status401Unauthorized}] - " +
							$"La signature no es válida.");

						return Results.Unauthorized();
					}

					WhatsappWebhook webhook = JsonSerializer.Deserialize(cuerpo, AppJsonSerializerContext.Default.WhatsappWebhook)!;

					foreach (Entry entry in webhook.Entry) {
						foreach (Change change in entry.Changes) {
							foreach (Models.Message message in change.Value.Messages ?? []) {
								// Se registra el mensaje recepcionado en la conversación...
								try {
									string numeroTelefono = message.From.Trim().Replace(" ", "");
									if (!numeroTelefono.StartsWith("+")) {
										numeroTelefono = $"+{numeroTelefono}";
									}

									await conversacionHelper.RegistrarNuevoMensajeEntrada(
										change.Value.Metadata.PhoneNumberId,
										numeroTelefono,
										message.Id,
										message.Type switch {
											"text" => TipoMensaje.Texto,
											"template" => TipoMensaje.Template,
											"image" => TipoMensaje.Imagen,
											"video" => TipoMensaje.Video,
											"audio" => TipoMensaje.Audio,
											"document" => TipoMensaje.Documento,
											"location" => TipoMensaje.Ubicacion,
											"contacts" => TipoMensaje.Contacto,
											"sticker" => TipoMensaje.Sticker,
											_ => TipoMensaje.Unknown
										},
										message.Type switch {
											"text" => message.Text?.Body,
											"image" => message.Image?.Caption,
											"video" => message.Video?.Caption,
											"document" => message.Document?.Caption,
											_ => null
										},
										JsonSerializer.Serialize(message, AppJsonSerializerContext.Default.Message),
										DateTimeOffset.FromUnixTimeSeconds(long.Parse(message.Timestamp)).UtcDateTime
									);
								} catch (Exception ex) {
									LambdaLogger.Log(
										$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - " +
										$"Ocurrió un error al ingresar el mensaje en conversación - Message ID: {message.Id}. ",
										$"{ex}");
								}
							}

							foreach (Estado status in change.Value.Statuses ?? []) {
								try {
									// Se actualiza el estado en los mensajes en la conversación...
									try {
										EstadoMensaje? nuevoEstado = status.Status switch {
											"sent" => EstadoMensaje.ConfirmacionEnvio,
											"delivered" => EstadoMensaje.Entregado,
											"read" => EstadoMensaje.Leido,
											"failed" => EstadoMensaje.Fallido,
											_ => null
										};
										if (nuevoEstado != null) {
											await conversacionHelper.ActualizarEstadoMensaje(status.Id, nuevoEstado.Value);
										}
									} catch (Exception ex) {
										LambdaLogger.Log(
											$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - " +
											$"Ocurrió un error al cambiar estado de mensaje en conversación - Status ID: {status.Id}. ",
											$"{ex}");
									}


									// Se actualiza estado del mensaje en trazas de salidas...
									DateTimeOffset fechaStatus = DateTimeOffset.FromUnixTimeSeconds(long.Parse(status.Timestamp));

									// Busco mensajes según el ID secundario...
									List<Dictionary<string, object?>> mensajes = await dynamo.ObtenerPorIndice(
										variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
										"PorIdSecundario",
										"IdSecundario",
										status.Id
									);

									foreach (Dictionary<string, object?> mensaje in mensajes.Where(m => m.TryGetValue("WhatsappMessageId", out object? whatsappMessageId) && whatsappMessageId != null && (string)whatsappMessageId == status.Id)) {
										if (status.Status == "sent") {
											await dynamo.ActualizarCampos(
												variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
												new Dictionary<string, object?> { ["IdMensaje"] = (string)mensaje["IdMensaje"]! },
												"SET FechaConfirmacionEnvio = :FechaConfirmacionEnvio",
												null,
												new Dictionary<string, object> {
												{ ":FechaConfirmacionEnvio", fechaStatus.ToString("o", CultureInfo.InvariantCulture) }
												}
											);
										} else if (status.Status == "delivered") {
											await dynamo.ActualizarCampos(
												variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
												new Dictionary<string, object?> { ["IdMensaje"] = (string)mensaje["IdMensaje"]! },
												"SET FechaEntrega = :FechaEntrega",
												null,
												new Dictionary<string, object> {
												{ ":FechaEntrega", fechaStatus.ToString("o", CultureInfo.InvariantCulture) }
												}
											);
										} else if (status.Status == "read") {
											await dynamo.ActualizarCampos(
												variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
												new Dictionary<string, object?> { ["IdMensaje"] = (string)mensaje["IdMensaje"]! },
												"SET FechaLectura = :FechaLectura",
												null,
												new Dictionary<string, object> {
												{ ":FechaLectura", fechaStatus.ToString("o", CultureInfo.InvariantCulture) }
												}
											);
										} else if (status.Status == "failed") {
											await dynamo.ActualizarCampos(
												variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
												new Dictionary<string, object?> { ["IdMensaje"] = (string)mensaje["IdMensaje"]! },
												"SET FechaFallo = :FechaFallo, Error = :Error",
												null,
												new Dictionary<string, object> {
													{ ":FechaFallo", fechaStatus.ToString("o", CultureInfo.InvariantCulture) },
													{ ":Error", JsonSerializer.Serialize(status.Errors ?? [], AppJsonSerializerContext.Default.ListError) }
												}
											);
										}
									}
								} catch (Exception ex) {
									LambdaLogger.Log(
										$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - " +
										$"Ocurrió un error al procesar Status ID: {status.Id}. ",
										$"{ex}");
								}
							}
						}
					}

					LambdaLogger.Log(
						$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
						$"Ejecución correcta del webhook de Whatsapp.");

					return Results.Ok();
				} catch (Exception ex) {
					LambdaLogger.Log(
						$"[POST] - [/Whatsapp/webhook] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
						$"Ocurrio un error en la ejecución del webhook de Whatsapp. " +
						$"{ex}");

					return Results.Problem("Ocurrió un error al procesar su solicitud.");
				}
			});

			return routes;
		}

		private static string ComputeHmacSha256(string data, string key) {
			byte[] keyBytes = Encoding.UTF8.GetBytes(key);
			byte[] dataBytes = Encoding.UTF8.GetBytes(data);

			using var hmac = new HMACSHA256(keyBytes);
			byte[] hash = hmac.ComputeHash(dataBytes);

			return BitConverter.ToString(hash).Replace("-", "").ToLower();
		}
	}
}
