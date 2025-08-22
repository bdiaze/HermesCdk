using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerEnvioCorreos.Helpers {
    public class ParameterStoreHelper(IAmazonSimpleSystemsManagement client) {
        public async Task<string> ObtenerParametro(string parameterArn) {
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
}
