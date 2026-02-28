using LibreriaCompartida.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibreriaCompartida.Interfaces {
	public interface IJsonSerializer {
		Dictionary<string, string> DeserializeDictionaryStringString(string json);
		Dictionary<string, object> DeserializeDictionaryStringObject(string json);
		WhatsappResponse DeserializeWhatsappResponse(string json);
	}
}
