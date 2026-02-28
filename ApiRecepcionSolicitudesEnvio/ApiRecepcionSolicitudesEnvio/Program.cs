using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using ApiRecepcionSolicitudesEnvio.Endpoints;
using ApiRecepcionSolicitudesEnvio.Helpers;
using ApiRecepcionSolicitudesEnvio.Models;
using LibreriaCompartida.Helpers;
using LibreriaCompartida.Interfaces;
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

#region Singleton AWS Services
builder.Services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
builder.Services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
#endregion

#region Singleton Helpers
builder.Services.AddSingleton<VariableEntornoHelper>();
builder.Services.AddSingleton<SecretManagerHelper>();
builder.Services.AddSingleton<DynamoHelper>();
builder.Services.AddSingleton<ConversacionHelper>();
builder.Services.AddSingleton<IJsonSerializer, AotJsonSerializer>();
builder.Services.AddSingleton<S3Helper>();
builder.Services.AddHttpClient<WhatsappHelper>(client => {
	client.BaseAddress = new Uri("https://graph.facebook.com/");
	client.Timeout = TimeSpan.FromSeconds(30);
});
#endregion

var app = builder.Build();

app.MapCorreoEndpoints();
app.MapWhatsappEndpoints();

app.Run();
