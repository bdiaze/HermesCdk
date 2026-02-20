using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Globalization;
using System.Text.Json;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
	public class DynamoHelper(IAmazonDynamoDB client) {

		public async Task<Dictionary<string, object?>?> Insertar(string nombreTabla, Dictionary<string, object?> item) {
			PutItemRequest request = new() {
				TableName = nombreTabla,
				Item = ToAttributeMap(item)
			};

			PutItemResponse response = await client.PutItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al insertar el ítem de Dynamo");
			}

			return item;
		}

		public async Task<Dictionary<string, object?>?> Obtener(string nombreTabla, Dictionary<string, object?> key) {
			GetItemRequest request = new() {
				TableName = nombreTabla,
				Key = ToAttributeMap(key)
			};

			GetItemResponse response = await client.GetItemAsync(request);

			if (response.HttpStatusCode != System.Net.HttpStatusCode.OK) {
				throw new Exception("Ocurrió un error al obtener el ítem de Dynamo");
			}

			if (response.Item == null || response.Item.Count == 0) {
				return null;
			}

			return ToDict(response.Item);
		}

		public static string ToJson(Dictionary<string, AttributeValue> item) {
			Dictionary<string, object?> dict = [];

			foreach (KeyValuePair<string, AttributeValue> kvp in item) {
				dict[kvp.Key] = ConvertAttributeValue(kvp.Value);
			}

			return JsonSerializer.Serialize(dict!, AppJsonSerializerContext.Default.IDictionaryStringObject);
		}

		private static Dictionary<string, object?>? ToDict(Dictionary<string, AttributeValue> item) {
			if (item == null) return null;

			Dictionary<string, object?> dict = [];

			foreach (KeyValuePair<string, AttributeValue> kvp in item) {
				dict[kvp.Key] = ConvertAttributeValue(kvp.Value);
			}

			return dict;
		}

		private static object? ConvertAttributeValue(AttributeValue av) {
			if (av.S != null) return av.S;

			if (av.N != null) {
				if (long.TryParse(av.N, out var intValue)) {
					return intValue;
				}
				if (double.TryParse(av.N, out var doubleValue)) {
					return doubleValue;
				}
				return av.N;
			}

			if (av.BOOL != null) return av.BOOL.Value;

			if (av.L != null) {
				List<object?> list = [];
				foreach (AttributeValue item in av.L) {
					list.Add(ConvertAttributeValue(item));
				}
				return list;
			}

			if (av.M != null) {
				Dictionary<string, object?> map = [];
				foreach (KeyValuePair<string, AttributeValue> kv in av.M) {
					map[kv.Key] = ConvertAttributeValue(kv.Value);
				}
				return map;
			}

			return null;
		}

		private static Dictionary<string, AttributeValue> ToAttributeMap(Dictionary<string, object?> dict) {
			Dictionary<string, AttributeValue> result = [];

			foreach (KeyValuePair<string, object?> kvp in dict) {
				result[kvp.Key] = ToAttributeValue(kvp.Value);
			}

			return result;
		}

		private static AttributeValue ToAttributeValue(object? value) {
			return value switch {
				null => new AttributeValue { NULL = true },
				string s => new AttributeValue { S = s },
				int i => new AttributeValue { N = i.ToString() },
				long l => new AttributeValue { N = l.ToString() },
				double d => new AttributeValue { N = d.ToString(CultureInfo.InvariantCulture) },
				float f => new AttributeValue { N = f.ToString(CultureInfo.InvariantCulture) },
				bool b => new AttributeValue { BOOL = b },
				IEnumerable<object> list => new AttributeValue { L = [.. list.Select(ToAttributeValue)] },
				Dictionary<string, object?> map => new AttributeValue { M = ToAttributeMap(map) },
				_ => new AttributeValue { S = value.ToString() }
			};
		}
	}
}
