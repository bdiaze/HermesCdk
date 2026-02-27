using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using LibreriaCompartida.Enums.DynamoDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibreriaCompartida.Entities.DynamoDB {
	public class ConversacionMetadata : Base {
		public required string TenantId { get; set; }
		public required string NumeroTelefono { get; set; }
		public DateTime FechaUltimoMensaje { get; set; }
		public string? PreviewUltimoMensaje { get; set; }
		public int CantidadNoLeidos { get; set; }
		public EstadoConversacion Estado { get; set; }
		public DateTime? FechaUltimaEntrada { get; set; }
		public DateTime? PuedeResponderGratuitoHasta { get; set; }

		public override string PK => $"TENANT#{TenantId}#CONVERSACION#{NumeroTelefono}";
		public override string SK => $"METADATA";

		public override string? GSI1PK => $"TENANT#{TenantId}";
		public override string? GSI1SK => $"FECHAULTIMOMENSAJE#{FechaUltimoMensaje.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}";
		public override string? GSI2PK => null;

		public override Dictionary<string, AttributeValue> ToItem() {
			Dictionary<string, AttributeValue> item = this.Key.Concat(this.GSI1Attributes).Concat(this.GSI2Attributes).Concat(
				new Dictionary<string, AttributeValue>() {
					{ "TenantId", new AttributeValue { S = $"{TenantId}" } },
					{ "NumeroTelefono", new AttributeValue { S = $"{NumeroTelefono}" } },
					{ "FechaUltimoMensaje",new AttributeValue { S = $"{FechaUltimoMensaje.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" } },
					{ "PreviewUltimoMensaje", new AttributeValue { NULL = true } },
					{ "CantidadNoLeidos", new AttributeValue { N = $"{CantidadNoLeidos.ToString(CultureInfo.InvariantCulture)}" } },
					{ "Estado", new AttributeValue { S = $"{Estado}" } },
					{ "FechaUltimaEntrada", new AttributeValue { NULL = true } },
					{ "PuedeResponderGratuitoHasta", new AttributeValue { NULL = true } },
				}
			).ToDictionary();

			if (PreviewUltimoMensaje != null) {
				item["PreviewUltimoMensaje"] = new AttributeValue { S = $"{PreviewUltimoMensaje}" };
			}

			if (FechaUltimaEntrada != null) {
				item["FechaUltimaEntrada"] = new AttributeValue { S = $"{FechaUltimaEntrada.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
			}

			if (PuedeResponderGratuitoHasta != null) {
				item["PuedeResponderGratuitoHasta"] = new AttributeValue { S = $"{PuedeResponderGratuitoHasta.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}" };
			}

			return item;
		}

		public static ConversacionMetadata FromItem(Dictionary<string, AttributeValue> item) {
			return new ConversacionMetadata() {
				TenantId = item["TenantId"].S,
				NumeroTelefono = item["NumeroTelefono"].S,
				FechaUltimoMensaje = DateTime.ParseExact(item["FechaUltimoMensaje"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				PreviewUltimoMensaje = item["PreviewUltimoMensaje"].S,
				CantidadNoLeidos = int.Parse(item["CantidadNoLeidos"].N, CultureInfo.InvariantCulture),
				Estado = Enum.Parse<EstadoConversacion>(item["Estado"].S),
				FechaUltimaEntrada = item["FechaUltimaEntrada"].S == null ? null : DateTime.ParseExact(item["FechaUltimaEntrada"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
				PuedeResponderGratuitoHasta = item["PuedeResponderGratuitoHasta"].S == null ? null : DateTime.ParseExact(item["PuedeResponderGratuitoHasta"].S, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
			};
		}
	}
}
