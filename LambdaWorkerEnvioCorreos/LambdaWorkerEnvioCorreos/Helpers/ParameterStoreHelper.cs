using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorkerEnvioCorreos.Helpers {
    internal class ParameterStoreHelper(IAmazonSimpleSystemsManagement client) {
        private readonly Dictionary<string, string> parametersValues = [];

        public async Task<string> ObtenerParametro(string parameterArn) {
            if (!parametersValues.TryGetValue(parameterArn, out string? value)) {
                GetParameterResponse response = await client.GetParameterAsync(new GetParameterRequest {
                    Name = parameterArn
                });

                if (response == null || response.Parameter == null) {
                    throw new Exception("No se pudo rescatar correctamente el parámetro");
                }

                value = response.Parameter.Value!;
                parametersValues[parameterArn] = value;
            }

            return value;
        }
    }
}
