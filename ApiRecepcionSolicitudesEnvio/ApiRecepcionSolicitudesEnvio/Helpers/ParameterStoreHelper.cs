using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
    public class ParameterStoreHelper {
        public static async Task<string> ObtenerParametro(string parameterArn) {
            AmazonSimpleSystemsManagementClient client = new();
            GetParameterResponse response = await client.GetParameterAsync(new GetParameterRequest {
                Name = parameterArn
            });

            if (response == null || response.Parameter == null) {
                throw new Exception("No se pudo rescatar correctamente el parámetro");
            }

            return response.Parameter.Value;
        }
    }
}
