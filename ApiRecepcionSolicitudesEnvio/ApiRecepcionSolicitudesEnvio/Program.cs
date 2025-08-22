using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

var app = builder.Build();

app.UseHttpsRedirection();

string parameterArnSqsQueueUrl;
if (builder.Environment.IsDevelopment()) {
    parameterArnSqsQueueUrl = builder.Configuration["VariableEntorno:PARAMETER_ARN_SQS_QUEUE_URL"] ?? throw new Exception("Debes agregar el atributo VariableEntorno > PARAMETER_ARN_SQS_QUEUE_URL en el archivo appsettings.Development.json para ejecutar localmente.");
} else {
    parameterArnSqsQueueUrl = Environment.GetEnvironmentVariable("PARAMETER_ARN_SQS_QUEUE_URL") ?? throw new Exception("No se ha configurado la variable de entorno PARAMETER_ARN_SQS_QUEUE_URL.");
}

string sqsQueueUrl = ParameterStoreHelper.ObtenerParametro(parameterArnSqsQueueUrl).Result;

AmazonSQSClient sqsClient = new();

var correoApi = app.MapGroup("/Correo");
correoApi.MapPost("/Enviar", async (Correo correo) => {
    Stopwatch stopwatch = Stopwatch.StartNew();

    try {
        string jsonCorreo = JsonSerializer.Serialize(correo, typeof(Correo), AppJsonSerializerContext.Default);

        SendMessageRequest request = new() {
            QueueUrl = sqsQueueUrl,
            MessageBody = jsonCorreo
        };

        SendMessageResponse response = await sqsClient.SendMessageAsync(request);
        Retorno salida = new(response.MessageId);

        LambdaLogger.Log(
            $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
            $"Correo ingresado exitosamente a la cola de envío - QueueMessageId: {salida.QueueMessageId}.");

        return Results.Ok(salida);
    } catch(Exception ex) {
        LambdaLogger.Log(
            $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
            $"Ocurrió un error al ingresar el correo a la cola de envío. " +
            $"{ex}");

        return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
    }
});

app.Run();
