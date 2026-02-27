using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime.Internal;
using Amazon.Runtime.Internal.Transform;
using LibreriaCompartida.Entities.DynamoDB;
using LibreriaCompartida.Enums.DynamoDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibreriaCompartida.Helpers {
	public class ConversacionHelper(VariableEntornoHelper variableEntorno, IAmazonDynamoDB client) {
		readonly string TABLE_NAME = variableEntorno.Obtener("DYNAMODB_TABLE_NAME_CONVERSACION");

		public async Task<ConversacionMetadata?> ObtenerMetadata(string tenantId, string numeroTelefono) {
			ConversacionMetadata auxiliar = new() {
				TenantId = tenantId,
				NumeroTelefono = numeroTelefono,
				FechaUltimoMensaje = DateTime.UtcNow,
				CantidadNoLeidos = 0,
				Estado = EstadoConversacion.Abierto,
			};

			GetItemResponse response = await client.GetItemAsync(new GetItemRequest {
				TableName = TABLE_NAME,
				Key = auxiliar.Key,
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener el ítem de Dynamo");
			}

			if (response.Item == null || response.Item.Count == 0) {
				return null;
			} else {
				return ConversacionMetadata.FromItem(response.Item);
			}
		}

		public async Task InsertarMetadata(ConversacionMetadata nuevo) {
			PutItemResponse response = await client.PutItemAsync(new PutItemRequest {
				TableName = TABLE_NAME,
				Item = nuevo.ToItem(),
				ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al insertar el ítem de Dynamo");
			}
		}

		public async Task InsertarMensaje(ConversacionMensaje nuevo) {
			PutItemResponse response = await client.PutItemAsync(new PutItemRequest {
				TableName = TABLE_NAME,
				Item = nuevo.ToItem(),
				ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)"
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al insertar el ítem de Dynamo");
			}
		}

		public async Task<ConversacionMetadata> ObtenerOCrearMetadata(string tenantId, string numeroTelefono, DateTime? fechaUltimoMensaje = null) {
			ConversacionMetadata? retorno = await ObtenerMetadata(tenantId, numeroTelefono);
			if (retorno != null) {
				return retorno;
			}

			ConversacionMetadata nuevo = new() {
				TenantId = tenantId,
				NumeroTelefono = numeroTelefono,
				FechaUltimoMensaje = fechaUltimoMensaje ?? DateTime.UtcNow,
				CantidadNoLeidos = 0,
				Estado = EstadoConversacion.Abierto,
			};

			await InsertarMetadata(nuevo);
			return nuevo;
		}

		public async Task ActualizarMetadataPosteriorAEnvio(ConversacionMetadata conversacion, string previewUltimoMensaje, DateTime fechaEnvio) {
			UpdateItemResponse response = await client.UpdateItemAsync(new UpdateItemRequest {
				TableName = TABLE_NAME,
				Key = conversacion.Key,
				UpdateExpression = $"SET " +
					$"{nameof(conversacion.FechaUltimoMensaje)} = :{nameof(conversacion.FechaUltimoMensaje)}, " +
					$"{nameof(conversacion.PreviewUltimoMensaje)} = :{nameof(conversacion.PreviewUltimoMensaje)}, " +
					$"{nameof(conversacion.GSI1SK)} = :{nameof(conversacion.GSI1SK)}",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ $":{nameof(conversacion.FechaUltimoMensaje)}", new AttributeValue { S = fechaEnvio.ToString("o", CultureInfo.InvariantCulture) } },
					{ $":{nameof(conversacion.PreviewUltimoMensaje)}", new AttributeValue { S = previewUltimoMensaje } },
					{ $":{nameof(conversacion.GSI1SK)}", new AttributeValue { S = fechaEnvio.ToString("o", CultureInfo.InvariantCulture) } },
				}
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al actualizar el ítem de Dynamo");
			}
		}

		public async Task RegistrarNuevoMensajeSalida(string tenantId, string numeroTelefono, string whatsappMessageId, TipoMensaje tipo, string? cuerpo, string? nombreTemplate, string? rawPayload, DateTime fechaMensaje) {
			ConversacionMetadata metadata = await ObtenerOCrearMetadata(tenantId, numeroTelefono, fechaMensaje);
			ConversacionMensaje mensaje = new() { 
				TenantId = tenantId,
				NumeroTelefono = numeroTelefono,
				WhatsappMessageId = whatsappMessageId,
				Direccion = DireccionMensaje.Salida,
				Tipo = tipo,
				Cuerpo = cuerpo,
				NombreTemplate = nombreTemplate,
				Estado = EstadoMensaje.Enviado,
				FechaCreacion = fechaMensaje,
				RawPayload = rawPayload,
			};

			await InsertarMensaje(mensaje);

			string previewMensaje;
			if (nombreTemplate != null) {
				previewMensaje = $"Template \"{nombreTemplate}\" enviado.";
			} else {
				if (cuerpo == null) {
					previewMensaje = "El mensaje no cuenta con una vista previa.";
				} else {
					const int MAX_LARGO = 40;
					previewMensaje = cuerpo[..Math.Min(MAX_LARGO, cuerpo.Length)];
					if (previewMensaje != cuerpo) {
						previewMensaje += "...";
					}
				}
			}
			await ActualizarMetadataPosteriorAEnvio(metadata, previewMensaje, fechaMensaje);
		}

		public async Task<ConversacionMensaje?> ObtenerMensajePorId(string whatsappMessageId) {
			QueryResponse response = await client.QueryAsync(new QueryRequest {
				TableName = TABLE_NAME,
				IndexName = "GSI2",
				KeyConditionExpression = $"GSI2PK = :WhatsappMessageId",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":WhatsappMessageId", new AttributeValue { S = $"WHATSAPPMESSAGEID#{whatsappMessageId}" } }
				},
				Limit = 1
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener el ítem de Dynamo");
			}

			if (response.Items == null || response.Items.Count == 0) {
				return null;
			} else {
				return ConversacionMensaje.FromItem(response.Items.First());
			}
		}

		public async Task ActualizarEstadoMensaje(string whatsappMessageId, EstadoMensaje nuevoEstado) {
			ConversacionMensaje? existente = await ObtenerMensajePorId(whatsappMessageId) ?? throw new Exception("No existe el mensaje para actualizar su estado.");
			if ((int)existente.Estado >= (int)nuevoEstado) {
				throw new Exception("El nuevo estado es previo al estado ya registrado.");
			}

			await client.UpdateItemAsync(new UpdateItemRequest {
				TableName = TABLE_NAME,
				Key = existente.Key,
				UpdateExpression = $"SET {nameof(existente.Estado)} = :{nameof(existente.Estado)}",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ $":{nameof(existente.Estado)}", new AttributeValue { S = $"{nuevoEstado}" } }
				}
			});
		}
	}
}
