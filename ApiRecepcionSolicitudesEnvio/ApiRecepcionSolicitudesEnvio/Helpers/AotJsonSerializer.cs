using LibreriaCompartida.Interfaces;
using LibreriaCompartida.Models;
using System.Text.Json;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
	public class AotJsonSerializer : IJsonSerializer {
		public Dictionary<string, object> DeserializeDictionaryStringObject(string json) {
			return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringObject)!;
		}

		public Dictionary<string, string> DeserializeDictionaryStringString(string json) {
			return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringString)!;
		}

		public WhatsappResponse DeserializeWhatsappResponse(string json) {
			return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.WhatsappResponse)!;
		}
	}
}
