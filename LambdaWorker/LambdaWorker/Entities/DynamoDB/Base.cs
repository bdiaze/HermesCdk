using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWorker.Entities.DynamoDB {
	internal abstract class Base {
		public abstract string PK { get; }
		public abstract string SK { get; }
		public abstract string? GSI1PK { get; }
		public abstract string? GSI1SK { get; }

		public Dictionary<string, AttributeValue> Key {
			get {
				return new Dictionary<string, AttributeValue> {
					{ "PK", new AttributeValue() { S = PK } },
					{ "SK", new AttributeValue() { S = SK } }
				};
			}
		}

		public Dictionary<string, AttributeValue> GSI1Attributes {
			get {
				Dictionary<string, AttributeValue> gsi1key = [];
				if (GSI1PK != null) {
					gsi1key["GSI1PK"] = new AttributeValue() { S = GSI1PK };
				}
				if (GSI1SK != null) {
					gsi1key["GSI1SK"] = new AttributeValue() { S = GSI1SK };
				}
				return gsi1key;
			}
		}

		public abstract Dictionary<string, AttributeValue> ToItem();
	}
}
