using LambdaWorker.Models;
using LibreriaCompartida.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LambdaWorker.Helpers {
	internal class WhatsappHelper(VariableEntornoHelper variableEntorno, SecretManagerHelper secretManagerHelper, HttpClient httpClient) {
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

			Dictionary<string, string> secretApp = JsonSerializer.Deserialize<Dictionary<string, string>>(await secretManagerHelper.ObtenerSecreto(variableEntorno.Obtener("SECRET_ARN_APP")))!;

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretApp["WhatsappToken"]);
			HttpResponseMessage response = await httpClient.PostAsJsonAsync($"v25.0/{idNumeroTelefono}/messages", payload);
			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Ocurrió un error con API de Whatsapp - Status Code: {response.StatusCode} - Content: {responseContent}");
			}

			WhatsappResponse? result = JsonSerializer.Deserialize<WhatsappResponse>(responseContent);
			return (result?.Messages?.FirstOrDefault()?.Id ?? throw new Exception("Whatsapp no retornó el ID del mensaje."), payload);
		}
	}
}
