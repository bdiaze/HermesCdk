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
					$"{nameof(conversacion.FechaUltimoMensaje)} = :FECHAULTIMOMENSAJE, " +
					$"{nameof(conversacion.PreviewUltimoMensaje)} = :PREVIEWULTIMOMENSAJE, " +
					$"{nameof(conversacion.GSI1SK)} = :GSI1SK",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":FECHAULTIMOMENSAJE", new AttributeValue { S = fechaEnvio.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) } },
					{ ":PREVIEWULTIMOMENSAJE", new AttributeValue { S = previewUltimoMensaje } },
					{ ":GSI1SK", new AttributeValue { S = $"FECHAULTIMOMENSAJE#{fechaEnvio.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
				}
			});

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al actualizar el ítem de Dynamo");
			}
		}

		public async Task ActualizarMetadataPosteriorARecepcion(ConversacionMetadata conversacion, string previewUltimoMensaje, DateTime fechaRecepcion) {
			UpdateItemResponse response = await client.UpdateItemAsync(new UpdateItemRequest {
				TableName = TABLE_NAME,
				Key = conversacion.Key,
				UpdateExpression = $"SET " +
					$"{nameof(conversacion.FechaUltimoMensaje)} = :FECHAULTIMOMENSAJE, " +
					$"{nameof(conversacion.PreviewUltimoMensaje)} = :PREVIEWULTIMOMENSAJE, " +
					$"{nameof(conversacion.CantidadNoLeidos)} = if_not_exists({nameof(conversacion.CantidadNoLeidos)}, :CERO) + :UNIDAD, " +
					$"{nameof(conversacion.FechaUltimaEntrada)} = :FECHAULTIMAENTRADA, " +
					$"{nameof(conversacion.PuedeResponderGratuitoHasta)} = :PUEDERESPONDERGRATUITOHASTA, " +
					$"{nameof(conversacion.GSI1SK)} = :GSI1SK",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":FECHAULTIMOMENSAJE", new AttributeValue { S = fechaRecepcion.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) } },
					{ ":PREVIEWULTIMOMENSAJE", new AttributeValue { S = previewUltimoMensaje } },
					{ ":CERO", new AttributeValue { N = "0" } },
					{ ":UNIDAD", new AttributeValue { N = "1" } },
					{ ":FECHAULTIMAENTRADA", new AttributeValue { S = fechaRecepcion.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) } },
					{ ":PUEDERESPONDERGRATUITOHASTA", new AttributeValue { S = fechaRecepcion.AddHours(24).ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) } },
					{ ":GSI1SK", new AttributeValue { S = $"FECHAULTIMOMENSAJE#{fechaRecepcion.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
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
					int count = 0;
					previewMensaje = "";
					TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(cuerpo);
					while (enumerator.MoveNext() && count < MAX_LARGO) {
						previewMensaje += enumerator.GetTextElement();
						count++;
					}
					if (count < cuerpo.Length) {
						previewMensaje += "...";
					}
				}
			}
			await ActualizarMetadataPosteriorAEnvio(metadata, previewMensaje, fechaMensaje);
		}

		public async Task RegistrarNuevoMensajeEntrada(string tenantId, string numeroTelefono, string whatsappMessageId, TipoMensaje tipo, string? cuerpo, string? rawPayload, DateTime fechaMensaje) {
			ConversacionMetadata metadata = await ObtenerOCrearMetadata(tenantId, numeroTelefono, fechaMensaje);
			ConversacionMensaje mensaje = new() {
				TenantId = tenantId,
				NumeroTelefono = numeroTelefono,
				WhatsappMessageId = whatsappMessageId,
				Direccion = DireccionMensaje.Entrada,
				Tipo = tipo,
				Cuerpo = cuerpo,
				NombreTemplate = null,
				Estado = EstadoMensaje.Recibido,
				FechaCreacion = fechaMensaje,
				RawPayload = rawPayload,
			};

			await InsertarMensaje(mensaje);

			string previewMensaje;
			if (cuerpo == null) {
				previewMensaje = "El mensaje no cuenta con una vista previa.";
			} else {
				const int MAX_LARGO = 40;
				int count = 0;
				previewMensaje = "";
				TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(cuerpo);
				while (enumerator.MoveNext() && count < MAX_LARGO) {
					previewMensaje += enumerator.GetTextElement();
					count++;
				}
				if (count < cuerpo.Length) {
					previewMensaje += "...";
				}
			}

			await ActualizarMetadataPosteriorARecepcion(metadata, previewMensaje, fechaMensaje);
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

		public async Task<List<ConversacionMetadata>> ObtenerConversaciones(string tenantId, DateTime? desde = null, DateTime? hasta = null, int limit = 50) {
			QueryRequest request = new() {
				TableName = TABLE_NAME,
				IndexName = "GSI1",
				KeyConditionExpression = $"GSI1PK = :TenantId",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":TenantId", new AttributeValue { S = $"TENANT#{tenantId}" } }
				},
				ScanIndexForward = false,
				Limit = limit
			};

			if (desde.HasValue || hasta.HasValue) {
				string rangeExp = "";
				if (desde.HasValue && hasta.HasValue) {
					rangeExp = "GSI1SK BETWEEN :desde AND :hasta";
					request.ExpressionAttributeValues[":desde"] = new AttributeValue { S = $"FECHAULTIMOMENSAJE#{desde.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
					request.ExpressionAttributeValues[":hasta"] = new AttributeValue { S = $"FECHAULTIMOMENSAJE#{hasta.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				} else if (desde.HasValue) {
					rangeExp = "GSI1SK >= :desde";
					request.ExpressionAttributeValues[":desde"] = new AttributeValue { S = $"FECHAULTIMOMENSAJE#{desde.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				} else if (hasta.HasValue) {
					rangeExp = "GSI1SK <= :hasta";
					request.ExpressionAttributeValues[":hasta"] = new AttributeValue { S = $"FECHAULTIMOMENSAJE#{hasta.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				}
				request.FilterExpression = rangeExp;
			}

			QueryResponse response = await client.QueryAsync(request);
			return [.. response.Items.Select(item => ConversacionMetadata.FromItem(item))];
		}

		public async Task<List<ConversacionMensaje>> ObtenerMensajes(string tenantId, string numeroTelefono, DateTime? desde = null, DateTime? hasta = null, int limit = 50) {
			QueryRequest request = new() {
				TableName = TABLE_NAME,
				KeyConditionExpression = $"PK = :pk",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CONVERSACION#{numeroTelefono}" } }
				},
				ScanIndexForward = false,
				Limit = limit
			};

			if (desde.HasValue || hasta.HasValue) {
				string rangeExp = "";
				if (desde.HasValue && hasta.HasValue) {
					rangeExp = "SK BETWEEN :desde AND :hasta";
					request.ExpressionAttributeValues[":desde"] = new AttributeValue { S = $"MENSAJE#{desde.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
					request.ExpressionAttributeValues[":hasta"] = new AttributeValue { S = $"MENSAJE#{hasta.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				} else if (desde.HasValue) {
					rangeExp = "SK >= :desde";
					request.ExpressionAttributeValues[":desde"] = new AttributeValue { S = $"MENSAJE#{desde.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				} else if (hasta.HasValue) {
					rangeExp = "SK <= :hasta";
					request.ExpressionAttributeValues[":hasta"] = new AttributeValue { S = $"MENSAJE#{hasta.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
				}
				request.FilterExpression = rangeExp;
			}

			QueryResponse response = await client.QueryAsync(request);
			return [.. response.Items.Select(item => ConversacionMensaje.FromItem(item))];
		}
	}
}
