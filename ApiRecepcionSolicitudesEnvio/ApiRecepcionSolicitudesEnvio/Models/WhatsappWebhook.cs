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

		[JsonPropertyName("metadata")]
		public required Metadata Metadata { get; set; }

		[JsonPropertyName("messages")]
		public List<Message>? Messages { get; set; }

		[JsonPropertyName("statuses")]
		public List<Estado>? Statuses { get; set; }
	}

	public class Metadata {
		[JsonPropertyName("display_phone_number")]
		public required string DisplayPhoneNumber { get; set; }

		[JsonPropertyName("phone_number_id")]
		public required string PhoneNumberId { get; set; }
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

		[JsonPropertyName("image")]
		public MediaInfo? Image { get; set; }

		[JsonPropertyName("video")]
		public MediaInfo? Video { get; set; }

		[JsonPropertyName("audio")]
		public MediaInfo? Audio { get; set; }

		[JsonPropertyName("document")]
		public MediaInfo? Document { get; set; }

		[JsonPropertyName("sticker")]
		public MediaInfo? Sticker { get; set; }

		[JsonPropertyName("location")]
		public Location? Location { get; set; }

		[JsonPropertyName("contacts")]
		public List<Contact>? Contacts { get; set; }

		[JsonPropertyName("button")]
		public Button? Button { get; set; }
	}

	public class Text {
		[JsonPropertyName("body")]
		public required string Body { get; set; }
	}

	public class MediaInfo {
		[JsonPropertyName("id")]
		public required string Id { get; set; }

		[JsonPropertyName("mime_type")]
		public required string MimeType { get; set; }

		[JsonPropertyName("sha256")]
		public required string Sha256 { get; set; }

		[JsonPropertyName("caption")]
		public string? Caption { get; set; }
	}

	public class Location {
		[JsonPropertyName("latitude")]
		public required double Latitude { get; set; }

		[JsonPropertyName("longitude")]
		public required double Longitude { get; set; }

		[JsonPropertyName("name")]
		public string? Name { get; set; }

		[JsonPropertyName("address")]
		public string? Address { get; set; }
	}

	public class Contact {
		[JsonPropertyName("name")]
		public required ContactName Name { get; set; }

		[JsonPropertyName("phones")]
		public required List<ContactPhone> Phones { get; set; }

	}

	public class ContactName {
		[JsonPropertyName("formatted_name")]
		public required string FormattedName { get; set; }
	}

	public class ContactPhone {
		[JsonPropertyName("phone")]
		public required string Phone { get; set; }

		[JsonPropertyName("type")]
		public string? Type { get; set; }
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
