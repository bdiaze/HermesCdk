using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LibreriaCompartida.Models {
	public class WhatsappResponse {
		[JsonPropertyName("messages")]
		public List<MessageInfo>? Messages { get; set; }
	}

	public class MessageInfo {
		[JsonPropertyName("id")]
		public string? Id { get; set; }
	}
}
