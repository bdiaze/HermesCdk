using System.Text.Json.Serialization;

namespace ApiRecepcionSolicitudesEnvio.Models {
	public class WhatsappWebhook {
		[JsonPropertyName("object")]
		public required string Object { get; set; }

		[JsonPropertyName("entry")]
		public required List<Entry> Entry { get; set; }
	}

	public class  Entry {
		[JsonPropertyName("id")]
		public required string Id { get; set; }

		[JsonPropertyName("changes")]
		public required List<Change> Changes { get; set; }
	}

	public class  Change {
		[JsonPropertyName("field")]
		public required string Field { get; set; }

		[JsonPropertyName("value")]
		public required Value Value { get; set; }
	}

	public class Value {
		[JsonPropertyName("messaging_product")]
		public required string MessagingProduct { get; set; }

		[JsonPropertyName("messages")]
		public List<Message>? Messages { get; set; }

		[JsonPropertyName("statuses")]
		public List<Estado>? Statuses { get; set; }
	}

	public class Message {
		[JsonPropertyName("from")]
		public required string From { get; set; }

		[JsonPropertyName("id")]
		public required string Id { get; set; }

		[JsonPropertyName("timestamp")]
		public required string Timestamp { get; set; }

		[JsonPropertyName("type")]
		public required string Type { get; set; }

		[JsonPropertyName("text")]
		public Text? Text { get; set; }

		[JsonPropertyName("button")]
		public Button? Button { get; set; }
	}

	public class Text {
		[JsonPropertyName("body")]
		public required string Body { get; set; }
	}

	public class Button {
		[JsonPropertyName("text")]
		public required string Text { get; set; }

		[JsonPropertyName("payload")]
		public required string Payload { get; set; }
	}

	public class Estado {
		[JsonPropertyName("id")]
		public required string Id { get; set; }

		[JsonPropertyName("status")]
		public required string Status { get; set; }

		[JsonPropertyName("timestamp")]
		public required string Timestamp { get; set; }

		[JsonPropertyName("recipient_id")]
		public required string RecipientId { get; set; }

		[JsonPropertyName("errors")]
		public List<Error>? Errors { get; set; }
	}

	public class Error {
		[JsonPropertyName("code")]
		public required int Code { get; set; }

		[JsonPropertyName("title")]
		public required string Title { get; set; }

		[JsonPropertyName("message")]
		public required string Message { get; set; }
	}
}
