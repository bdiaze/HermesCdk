using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SQS;
using System.Net;
using System.Net.Mime;

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

		public async Task<(bool existe, string? contentType)> ExisteBucketObject(string bucketName, string bucketKey) {
			GetObjectMetadataRequest request = new() {
				BucketName = bucketName,
				Key = bucketKey,
			};

			try {
				GetObjectMetadataResponse response = await amazonS3.GetObjectMetadataAsync(request);
				return (true, response.ContentType);

			} catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound && ex.ErrorCode == "NotFound") {
				return (false, null);
			}
		}

		public async Task PutObjectStream(string bucketName, string bucketKey, Stream stream, string contentType) {
			TransferUtility transferUtility = new(amazonS3);
			await transferUtility.UploadAsync(new TransferUtilityUploadRequest() {
				BucketName = bucketName,
				Key = bucketKey,
				InputStream = stream,
				ContentType = contentType
			});
		}
	}
}
