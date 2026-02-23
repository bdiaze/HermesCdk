using System.Text.Json.Serialization;

namespace ApiRecepcionSolicitudesEnvio.Models {
	public class WhatsappWebhook {
		public required string Object { get; set; }
		public required List<Entry> Entry { get; set; }
	}

	public class  Entry {
		public required string Id { get; set; }
		public required List<Change> Changes { get; set; }
	}

	public class  Change {
		public required string Field { get; set; }
		public required Value Value { get; set; }
	}

	public class Value {
		[JsonPropertyName("messaging_product")]
		public required string MessagingProduct { get; set; }
		public List<Message>? Messages { get; set; }
		public List<Estado>? Statuses { get; set; }
	}

	public class Message {
		public required string From { get; set; }
		public required string Id { get; set; }
		public required string Timestamp { get; set; }
		public required string Type { get; set; }
		public Text? Text { get; set; }
		public Button? Button { get; set; }
	}

	public class Text {
		public required string Body { get; set; }
	}

	public class Button {
		public required string Text { get; set; }
		public required string Payload { get; set; }
	}

	public class Estado {
		public required string Id { get; set; }
		public required string Status { get; set; }
		public required string Timestamp { get; set; }
		[JsonPropertyName("recipient_id")]
		public required string RecipientId { get; set; }
		public List<Error>? Errors { get; set; }
	}

	public class Error {
		public required int Code { get; set; }
		public required string Title { get; set; }
		public required string Message { get; set; }
	}
}
