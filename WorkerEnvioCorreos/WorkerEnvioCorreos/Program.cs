using Amazon.SimpleEmailV2;
using Amazon.SimpleSystemsManagement;
using Amazon.SQS;
using WorkerEnvioCorreos;
using WorkerEnvioCorreos.Helpers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2, AmazonSimpleEmailServiceV2Client>();

builder.Services.AddSingleton<VariableEntorno, VariableEntorno>();
builder.Services.AddSingleton<ParameterStoreHelper, ParameterStoreHelper>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
