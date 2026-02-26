using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime.Internal.Transform;
using ApiRecepcionSolicitudesEnvio.Entities.DynamoDB;
using ApiRecepcionSolicitudesEnvio.Enums.DynamoDB;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
	public class ConversacionHelper(VariableEntornoHelper variableEntorno, IAmazonDynamoDB client) {
		readonly string TABLE_NAME = variableEntorno.Obtener("DYNAMODB_TABLE_NAME_CONVERSACION");

		public async Task<ConversacionMensaje?> ObtenerMensajePorId(string whatsappMessageId) {
			QueryResponse response = await client.QueryAsync(new QueryRequest {
				TableName = TABLE_NAME,
				IndexName = "GSI2",
				KeyConditionExpression = $"GSI2PK = :WhatsappMessageId",
				ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
					{ ":WhatsappMessageId", new AttributeValue { S = whatsappMessageId } }
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
