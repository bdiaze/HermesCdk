using LibreriaCompartida.Interfaces;
using LibreriaCompartida.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LambdaWorker.Helpers {
	public class DefaultJsonSerializer : IJsonSerializer {
		public Dictionary<string, object> DeserializeDictionaryStringObject(string json) {
			return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
		}

		public Dictionary<string, string> DeserializeDictionaryStringString(string json) {
			return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
		}

		public WhatsappResponse DeserializeWhatsappResponse(string json) {
			return JsonSerializer.Deserialize<WhatsappResponse>(json)!;
		}
	}
}
