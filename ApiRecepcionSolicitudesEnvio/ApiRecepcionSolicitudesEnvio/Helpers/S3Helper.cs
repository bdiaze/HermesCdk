using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using System.Net;

namespace ApiRecepcionSolicitudesEnvio.Helpers {
	public class S3Helper(IAmazonS3 amazonS3) {
		private readonly int PRE_SIGNED_URL_EXPIRATION_MINUTES = 5;

		public async Task<string> ObtenerGetPreSignedUrl(string bucketName, string bucketKey, string nombreArchivo) {
			GetPreSignedUrlRequest request = new() {
				BucketName = bucketName,
				Key = bucketKey,
				Verb = HttpVerb.GET,
				Expires = DateTime.UtcNow.AddMinutes(PRE_SIGNED_URL_EXPIRATION_MINUTES),
				ResponseHeaderOverrides = new ResponseHeaderOverrides {
					ContentDisposition = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(nombreArchivo)}"
				}
			};

			return await amazonS3.GetPreSignedURLAsync(request);
		}

		public async Task<bool> ExisteBucketObject(string bucketName, string bucketKey) {
			GetObjectMetadataRequest request = new() {
				BucketName = bucketName,
				Key = bucketKey,
			};

			bool existe = true;
			try {
				GetObjectMetadataResponse response = await amazonS3.GetObjectMetadataAsync(request);
			} catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NoSuchKey") {
				existe = false;
			}

			return existe;
		}

		public async Task PutObjectStream(string bucketName, string bucketKey, Stream stream, string contentType) {
			await amazonS3.PutObjectAsync(new PutObjectRequest {
				BucketName = bucketName,
				Key= bucketKey,
				InputStream = stream,
				ContentType = contentType
			});
		}
	}
}
