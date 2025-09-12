using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LambdaWorkerEnvioCorreos.Helpers {
    internal static class CaracteresEspeciales {
        public static string Limpiar(string texto) {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            StringBuilder sb = new();

            foreach (char ch in texto) {
                switch (ch) {
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(ch)) {
                            sb.Append($"\\x{(int)ch:X2}");
                        } else {
                            sb.Append(ch);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        public static string LimpiarExceptoSaltos(string texto) {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            StringBuilder sb = new();

            foreach (char ch in texto) {
                if (char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t') {
                    sb.Append($"\\x{(int)ch:X2}");
                }
                sb.Append(ch);
            }
            return sb.ToString();

        }

        public static string LimpiarBase64(string base64) {
            if (string.IsNullOrEmpty(base64)) return string.Empty;

            // Only keep A-Z, a-z, 0-9, +, /, =
            return Regex.Replace(base64, @"[^A-Za-z0-9+/=]", "");
        }
    }
}
