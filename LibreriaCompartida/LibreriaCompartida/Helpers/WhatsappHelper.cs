using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces;
using LibreriaCompartida.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LibreriaCompartida.Helpers {
	public class WhatsappHelper(VariableEntornoHelper variableEntorno, SecretManagerHelper secretManagerHelper, HttpClient httpClient, IJsonSerializer jsonSerializer) {
		public async Task<(string whatsappIdMessage, object payload)> Enviar(string idNumeroTelefono, string para, string nombreTemplate, string lenguaje, string[]? parametrosHeader, string[]? parametrosBody, string[]? parametrosButton) {
			List<object> componentes = [];
			if (parametrosHeader != null && parametrosHeader.Length > 0) {
				componentes.Add(new {
					type = "header",
					parameters = parametrosHeader.Select(h => new { type = "text", text = h }).ToArray()
				});
			}
			if (parametrosBody != null && parametrosBody.Length > 0) {
				componentes.Add(new {
					type = "body",
					parameters = parametrosBody.Select(b => new { type = "text", text = b }).ToArray()
				});
			}
			if (parametrosButton != null && parametrosButton.Length > 0) {
				componentes.AddRange(parametrosButton.Select(b => new { 
					type = "button",
					sub_type = "url",
					index = "0",
					parameters = new[] { 
						new { type = "text", text = b }
					}
				}).ToList());
			}

			object payload = new {
				messaging_product = "whatsapp",
				to = para,
				type = "template",
				template = new {
					name = nombreTemplate,
					language = new { code = lenguaje },
					components = componentes.ToArray()
				}
			};

			Dictionary<string, string> secretApp = jsonSerializer.DeserializeDictionaryStringString(await secretManagerHelper.ObtenerSecreto(variableEntorno.Obtener("SECRET_ARN_APP")))!;

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretApp["WhatsappToken"]);
			HttpResponseMessage response = await httpClient.PostAsJsonAsync($"v25.0/{idNumeroTelefono}/messages", payload);
			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Ocurrió un error con API de Whatsapp - Status Code: {response.StatusCode} - Content: {responseContent}");
			}

			WhatsappResponse? result = jsonSerializer.DeserializeWhatsappResponse(responseContent);
			return (result?.Messages?.FirstOrDefault()?.Id ?? throw new Exception("Whatsapp no retornó el ID del mensaje."), payload);
		}

		public async Task<(Stream stream, string contentType, string fileName)> ObtenerMedia(string mediaId) {
			string secret = await secretManagerHelper.ObtenerSecreto(variableEntorno.Obtener("SECRET_ARN_APP"));
			Dictionary<string, string> secretApp = jsonSerializer.DeserializeDictionaryStringString(secret)!;

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretApp["WhatsappToken"]);
			HttpResponseMessage response = await httpClient.GetAsync($"v25.0/{mediaId}");
			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Ocurrió un error al obtener URL de descarga de media desde API de Whatsapp - Status Code: {response.StatusCode} - Content: {responseContent}");
			}
			WhatsappMediaResponse mediaResponse = jsonSerializer.DeserializeWhatsappMediaResponse(responseContent)!;

			HttpResponseMessage responseGetMedia = await httpClient.GetAsync(mediaResponse.Url, HttpCompletionOption.ResponseHeadersRead);
			if (!responseGetMedia.IsSuccessStatusCode) {
				throw new Exception($"Ocurrió un error al descargar media desde API de Whatsapp - Status Code: {response.StatusCode} - Content: {responseContent}");
			}

			string fileName = mediaResponse.FileName ?? $"media_{mediaId}";
			string contentType = responseGetMedia.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
			Stream stream = await responseGetMedia.Content.ReadAsStreamAsync();
			return (stream, contentType, fileName);
		}
	}
}
