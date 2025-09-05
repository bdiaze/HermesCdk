using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorkerEnvioCorreos.Helpers {
    internal class VariableEntornoHelper(IHostEnvironment env, IConfiguration config) {
        public string Obtener(string nombre) {
            if (env.IsDevelopment()) {
                return config[$"VariableEntorno:{nombre}"] ?? throw new Exception($"Debes agregar el atributo VariableEntorno > {nombre} en el archivo appsettings.Development.json para ejecutar localmente.");
            }
            return Environment.GetEnvironmentVariable(nombre) ?? throw new Exception($"No se ha configurado la variable de entorno {nombre}.");
        }
    }
}
