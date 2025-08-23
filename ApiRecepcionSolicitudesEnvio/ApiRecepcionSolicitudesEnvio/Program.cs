using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, new SourceGeneratorLambdaJsonSerializer<AppJsonSerializerContext>());

builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddSingleton<VariableEntorno, VariableEntorno>();

VariableEntorno variableEntorno = new(builder.Environment, builder.Configuration);
string cognitoBaseUrl = ParameterStoreHelper.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_COGNITO_BASE_URL")).Result;
string cognitoUserPoolId = ParameterStoreHelper.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_COGNITO_USER_POOL_ID")).Result;
string cognitoUserPoolClientId = ParameterStoreHelper.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID")).Result;
string cognitoRegion = ParameterStoreHelper.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_COGNITO_REGION")).Result;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
        options.Authority = cognitoBaseUrl;
        options.MetadataAddress = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}/.well-known/openid-configuration";
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = cognitoBaseUrl,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });
builder.Services.AddAuthorization(options => {
    options.AddPolicy("RequiereScopeEnviarCorreo", policy => policy.RequireClaim("scope", "api/enviar_correo"));
});


var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

builder.Configuration["SQS_QUEUE_URL"] = ParameterStoreHelper.ObtenerParametro(variableEntorno.Obtener("PARAMETER_ARN_SQS_QUEUE_URL")).Result;

var correoApi = app.MapGroup("/Correo");
correoApi.MapPost("/Enviar", async (Correo correo, IAmazonSQS sqsClient, IConfiguration config, ClaimsPrincipal user) => {
    Stopwatch stopwatch = Stopwatch.StartNew();

    try {
        string jsonCorreo = JsonSerializer.Serialize(correo, typeof(Correo), AppJsonSerializerContext.Default);

        SendMessageRequest request = new() {
            MessageGroupId = user.Identity!.Name,
            QueueUrl = config["SQS_QUEUE_URL"],
            MessageBody = jsonCorreo
        };

        SendMessageResponse response = await sqsClient.SendMessageAsync(request);
        Retorno salida = new(response.MessageId);

        LambdaLogger.Log(
            $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
            $"Correo ingresado exitosamente a la cola de envio - QueueMessageId: {salida.QueueMessageId}.");

        return Results.Ok(salida);
    } catch(Exception ex) {
        LambdaLogger.Log(
            $"[POST] - [/Correo/Enviar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
            $"Ocurrio un error al ingresar el correo a la cola de envio. " +
            $"{ex}");

        return Results.Problem("Ocurrió un error al procesar su solicitud de envío de correo.");
    }
}).RequireAuthorization("RequiereScopeEnviarCorreo");

app.Run();
