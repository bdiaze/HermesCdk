using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
    public class ParameterStoreHelper(IAmazonSimpleSystemsManagement client) {
        
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
