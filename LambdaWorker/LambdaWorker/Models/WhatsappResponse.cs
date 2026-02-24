using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LambdaWorker.Models {
	internal class WhatsappResponse {
		[JsonPropertyName("messages")]
		public List<MessageInfo>? Messages { get; set; }
	}

	internal class MessageInfo {
		[JsonPropertyName("id")]
		public string? Id { get; set; }
	}
}
