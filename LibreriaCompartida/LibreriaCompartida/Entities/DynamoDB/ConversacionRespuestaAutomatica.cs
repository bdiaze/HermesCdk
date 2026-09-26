using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LibreriaCompartida.Entities.DynamoDB {
	public class ConversacionRespuestaAutomatica : Base {
		public required string TenantId { get; set; }
		public required string NombreTemplate { get; set; }
		public DateTime FechaCreacion { get; set; }

		public override string PK => $"TENANT#{TenantId}";

		public override string SK => $"RESPUESTAAUTOMATICA";

		public override string? GSI1PK => null;
		public override string? GSI1SK => null;
		public override string? GSI2PK => null;

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(this.GSI1Attributes).Concat(this.GSI2Attributes).Concat(
				new Dictionary<string, AttributeValue>() {
					{ "TenantId", new AttributeValue { S = $"{TenantId}" } },
					{ "NombreTemplate", new AttributeValue { S = $"{NombreTemplate}" } },
					{ "FechaCreacion", new AttributeValue { S = $"{FechaCreacion.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
				}
			).ToDictionary();

			return item;
		}

		public static ConversacionRespuestaAutomatica FromItem(Dictionary<string, AttributeValue> item) {
			return new ConversacionRespuestaAutomatica() { 
				TenantId = item["TenantId"].S,
				NombreTemplate = item["NombreTemplate"].S,
				FechaCreacion = DateTime.ParseExact(item["FechaCreacion"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
			};
		}
	}
}
