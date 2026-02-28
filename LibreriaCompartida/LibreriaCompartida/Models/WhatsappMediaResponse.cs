using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LibreriaCompartida.Models {
	public class WhatsappMediaResponse {
		[JsonPropertyName("id")]
		public required string Id { get; set; }

		[JsonPropertyName("mime_type")]
		public required string MimeType { get; set; }

		[JsonPropertyName("sha256")]
		public required string Sha256 { get; set; }

		[JsonPropertyName("url")]
		public required string Url { get; set; }

		[JsonPropertyName("file_name")]
		public string? FileName { get; set; }

		[JsonPropertyName("file_size")]
		public long? FileSize { get; set; }
	}
}
