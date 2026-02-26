using Amazon.DynamoDBv2.Model;
using LambdaWorker.Enums.DynamoDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorker.Entities.DynamoDB {
	internal class ConversacionMensaje : Base {
		public required string TenantId { get; set; }
		public required string NumeroTelefono { get; set; }
		public required string WhatsappMessageId { get; set; }
		public DireccionMensaje Direccion { get; set; }
		public TipoMensaje Tipo {  get; set; }
		public string? Cuerpo { get; set; }
		public string? NombreTemplate { get; set; }
		public EstadoMensaje Estado { get; set; }
		public DateTime FechaCreacion { get; set; }
		public string? RawPayload { get; set; }

		public override string PK => $"TENANT#{TenantId}#CONVERSACION#{NumeroTelefono}";
		public override string SK => $"MENSAJE#{FechaCreacion.ToString("o", CultureInfo.InvariantCulture)}#{WhatsappMessageId}";

		public override string? GSI1PK => null;
		public override string? GSI1SK => null;

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(this.GSI1Attributes).Concat(
				new Dictionary<string, AttributeValue>() {
					{ "TenantId", new AttributeValue { S = $"{TenantId}" } },
					{ "NumeroTelefono", new AttributeValue { S = $"{NumeroTelefono}" } },
					{ "WhatsappMessageId", new AttributeValue { S = $"{WhatsappMessageId}" } },
					{ "Direccion", new AttributeValue { S = $"{Direccion}" } },
					{ "Tipo", new AttributeValue { S = $"{Tipo}" } },
					{ "Cuerpo", new AttributeValue { NULL = true } },
					{ "NombreTemplate", new AttributeValue { NULL = true } },
					{ "Estado", new AttributeValue { S = $"{Estado}" } },
					{ "FechaCreacion", new AttributeValue { S = $"{FechaCreacion.ToString("o", CultureInfo.InvariantCulture)}" } },
					{ "RawPayload", new AttributeValue { NULL = true } },
				}
			).ToDictionary();

			if (Cuerpo != null) {
				item["Cuerpo"] = new AttributeValue { S = $"{Cuerpo}" };
			}

			if (NombreTemplate != null) {
				item["NombreTemplate"] = new AttributeValue { S = $"{NombreTemplate}" };
			}

			if (RawPayload != null) {
				item["RawPayload"] = new AttributeValue { S = $"{RawPayload}" };
			}

			return item;
		}

		public static ConversacionMensaje FromItem(Dictionary<string, AttributeValue> item) {
			return new ConversacionMensaje() {
				TenantId = item["TenantId"].S,
				NumeroTelefono = item["NumeroTelefono"].S,
				WhatsappMessageId = item["WhatsappMessageId"].S,
				Direccion = Enum.Parse<DireccionMensaje>(item["Direccion"].S),
				Tipo = Enum.Parse<TipoMensaje>(item["Tipo"].S),
				Cuerpo = item["Cuerpo"].S,
				NombreTemplate = item["NombreTemplate"].S,
				Estado = Enum.Parse<EstadoMensaje>(item["Estado"].S),
				FechaCreacion = DateTime.ParseExact(item["FechaCreacion"].S, "o", CultureInfo.InvariantCulture),
				RawPayload = item["RawPayload"].S
			};
		}
	}
}
