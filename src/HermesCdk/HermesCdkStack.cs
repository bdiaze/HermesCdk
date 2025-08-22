using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using System;
using System.Collections.Generic;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace HermesCdk {
    public class HermesCdkStack : Stack
    {
        internal HermesCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");

            string handler = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_HANDLER") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_HANDLER");
            string timeout = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_TIMEOUT");
            string memorySize = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE");
            string domainName = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME");
            string apiMappingKey = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_KEY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_KEY");


            // Creación de cola...
            Queue queue = new(this, $"{appName}Queue", new QueueProps {
                QueueName = $"{appName}Queue",
                RetentionPeriod = Duration.Days(14),
                VisibilityTimeout = Duration.Minutes(5),
                EnforceSSL = true,
            });

            StringParameter stringParameterQueueUrl = new(this, $"{appName}StringParameterQueueUrl", new StringParameterProps {
                ParameterName = $"{appName}/SQS/QueueUrl",
                Description = $"Queue URL de la aplicacion {appName}",
                StringValue = queue.QueueUrl,
                Tier = ParameterTier.STANDARD,
            });

            // Creación de log group lambda...
            LogGroup logGroup = new(this, $"{appName}APILogGroup", new LogGroupProps { 
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/logs",
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de role para la función lambda...
            IRole roleLambda = new Role(this, $"{appName}APILambdaRole", new RoleProps {
                RoleName = $"{appName}APILambdaRole",
                Description = $"Role para API Lambda de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}APILambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterQueueUrl.ParameterArn,
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToSQS",
                                    Actions = [
                                        "sqs:SendMessage"
                                    ],
                                    Resources = [
                                        queue.QueueArn,
                                    ],
                                })
                            ]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function function = new(this, $"{appName}APILambdaFunction", new FunctionProps {
                Runtime = Runtime.DOTNET_8,
                Handler = handler,
                Code = Code.FromAsset("../publish/publish.zip"),
                FunctionName = $"{appName}APILambdaFunction",
                Timeout = Duration.Seconds(double.Parse(timeout)),
                MemorySize = double.Parse(memorySize),
                Architecture = Architecture.X86_64,
                LogGroup = logGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                    { "PARAMETER_ARN_SQS_QUEUE_URL", stringParameterQueueUrl.ParameterArn },
                },
                Role = roleLambda,
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                RestApiName = $"{appName}APILambdaRestApi",
                Handler = function,
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    MetricsEnabled = true,
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                DefaultMethodOptions = new MethodOptions {
                    ApiKeyRequired = true,
                },
            });

            // Creación de la CfnApiMapping para el API Gateway...
            _ = new CfnApiMapping(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
                DomainName = domainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se crea Usage Plan para configurar API Key...
            UsagePlan usagePlan = new(this, $"{appName}APIUsagePlan", new UsagePlanProps {
                Name = $"{appName}APIUsagePlan",
                Description = $"Usage Plan de {appName} API",
                ApiStages = [
                    new UsagePlanPerApiStage() {
                        Api = lambdaRestApi,
                        Stage = lambdaRestApi.DeploymentStage
                    }
                ],
            });

            // Se crea API Key...
            ApiKey apiGatewayKey = new(this, $"{appName}APIAPIKey", new ApiKeyProps {
                ApiKeyName = $"{appName}APIAPIKey",
                Description = $"API Key de {appName} API",
            });
            usagePlan.AddApiKey(apiGatewayKey);

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
            Permission permission = new() {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}APIPermission", permission);
        }
    }
}
