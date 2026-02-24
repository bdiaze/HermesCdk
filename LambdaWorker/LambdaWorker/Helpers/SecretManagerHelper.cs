using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorker.Helpers {
	internal class SecretManagerHelper(IAmazonSecretsManager client) {
		private readonly Dictionary<string, string> secretsValues = [];

		public async Task<string> ObtenerSecreto(string secretArn) {
			if (!secretsValues.TryGetValue(secretArn, out string? value)) {
				GetSecretValueResponse response = await client.GetSecretValueAsync(new GetSecretValueRequest {
					SecretId = secretArn
				});

				if (response == null || response.SecretString == null) {
					throw new Exception("No se pudo rescatar correctamente el secreto");
				}

				value = response.SecretString;
				secretsValues[secretArn] = value;
			}

			return value;
		}
	}
}
