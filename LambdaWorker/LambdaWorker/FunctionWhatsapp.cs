using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SecretsManager;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Amazon.SimpleSystemsManagement;
using LambdaWorker.Enums.DynamoDB;
using LambdaWorker.Helpers;
using LambdaWorker.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using static Amazon.Lambda.SQSEvents.SQSBatchResponse;
using static Amazon.Lambda.SQSEvents.SQSEvent;

namespace LambdaWorker {
	public class FunctionWhatsapp {
		private readonly IServiceProvider serviceProvider;

		public FunctionWhatsapp() {
			var builder = Host.CreateDefaultBuilder();
			builder.ConfigureServices((context, services) => {
				#region Singleton AWS Services
				services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
				services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
				#endregion

				#region Singleton Helpers
				services.AddSingleton<VariableEntornoHelper>();
				services.AddSingleton<SecretManagerHelper>();
				services.AddSingleton<DynamoHelper>();
				services.AddSingleton<ConversacionHelper>();
				services.AddHttpClient<WhatsappHelper>(client => {
					client.BaseAddress = new Uri("https://graph.facebook.com/");
					client.Timeout = TimeSpan.FromSeconds(30);
				});
				#endregion
			});

			var app = builder.Build();

			serviceProvider = app.Services;
		}

		public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context) {
			List<BatchItemFailure> listaMensajesError = [];

			Stopwatch stopwatch = Stopwatch.StartNew();

			LambdaLogger.Log(
				$"[FunctionWhatsapp] - [FunctionHandler] - " +
				$"Iniciado worker de envio de Whatsapp.");

			VariableEntornoHelper variableEntorno = serviceProvider.GetRequiredService<VariableEntornoHelper>();
			DynamoHelper dynamo = serviceProvider.GetRequiredService<DynamoHelper>();
			WhatsappHelper whatsappHelper = serviceProvider.GetRequiredService<WhatsappHelper>();
			ConversacionHelper conversacionHelper = serviceProvider.GetRequiredService<ConversacionHelper>();

			LambdaLogger.Log(
				$"[FunctionWhatsapp] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
				$"Se obtendran los parametros necesarios para procesar los mensajes.");

			// Obteniendo URL de la cola de correos a procesar...
			string nombreAplicacion = variableEntorno.Obtener("APP_NAME");

			LambdaLogger.Log(
				$"[FunctionWhatsapp] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
				$"Se comienza el envio de {evnt.Records.Count} mensajes de Whatsapp");

			foreach (SQSMessage mensaje in evnt.Records) {
				string idMensaje = "";

				try {
					idMensaje = mensaje.Body;
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
						case "Whatsapp":
							Whatsapp whatsapp = JsonSerializer.Deserialize<Whatsapp>((string)contenido)!;

							(string idMensajeWhatsapp, object payload) = await whatsappHelper.Enviar(
								whatsapp.De, 
								whatsapp.Para, 
								whatsapp.NombreTemplate, 
								whatsapp.Lenguaje, 
								whatsapp.ParametrosTitulo, 
								whatsapp.ParametrosCuerpo, 
								whatsapp.ParametrosBoton
							);

							DateTime fechaEnvio = DateTime.UtcNow;

							// Se actualiza el ítem en DynamoDB...
							await dynamo.ActualizarCampos(
								variableEntorno.Obtener("DYNAMODB_TABLE_NAME"),
								new Dictionary<string, object?> { ["IdMensaje"] = (string)itemDynamo["IdMensaje"]! },
								"SET Estado = :Estado, FechaEnvio = :FechaEnvio, WhatsappMessageId = :WhatsappMessageId, IdSecundario = :IdSecundario",
								null,
								new Dictionary<string, object> {
									{ ":Estado", "WhatsappEnviado" },
									{ ":FechaEnvio", fechaEnvio.ToString("o", CultureInfo.InvariantCulture) },
									{ ":WhatsappMessageId", idMensajeWhatsapp },
									{ ":IdSecundario", idMensajeWhatsapp },
								}
							);

							// Y se registra mensaje en la conversación con el usuario...
							await conversacionHelper.RegistrarNuevoMensajeSalida(
								itemDynamo.TryGetValue("AppName", out object? appName) ? (string)appName! : "General",
								whatsapp.Para,
								idMensajeWhatsapp,
								TipoMensaje.Template,
								null,
								whatsapp.NombreTemplate,
								JsonSerializer.Serialize(payload),
								fechaEnvio
							);

							break;
						default:
							throw new Exception("Tipo de mensaje no soportado");
					}
				} catch (Exception ex) {
					LambdaLogger.Log(LogLevel.Error,
						$"[FunctionWhatsapp] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
						$"Ocurrio un error al procesar el mensaje de Whatsapp - ID Mensaje: {idMensaje} - Queue Message ID: {mensaje.MessageId}. " +
						$"{ex}");

					listaMensajesError.Add(new BatchItemFailure {
						ItemIdentifier = mensaje.MessageId,
					});
				}
			}

			LambdaLogger.Log(
				$"[FunctionWhatsapp] - [FunctionHandler] - [{stopwatch.ElapsedMilliseconds} ms] - " +
				$"Termino exitosamente el envio de correos - Casos con error: {listaMensajesError.Count}.");

			return new SQSBatchResponse {
				BatchItemFailures = listaMensajesError
			};
		}
	}
}
