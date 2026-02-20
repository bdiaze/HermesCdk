using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.Batch;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SecretsManager;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;
using Secret = Amazon.CDK.AWS.SecretsManager.Secret;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;
using ThrottleSettings = Amazon.CDK.AWS.APIGateway.ThrottleSettings;

namespace HermesCdk {
    public class HermesCdkStack : Stack
    {
        internal HermesCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props) {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

            string apiDirectory = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_DIRECTORY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_DIRECTORY");
            string handler = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_HANDLER") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_HANDLER");
            string timeout = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_TIMEOUT");
            string memorySize = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE");
            string domainName = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME");
            string apiMappingKey = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_KEY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_KEY");

            string nombreDeDefecto = System.Environment.GetEnvironmentVariable("SES_NOMBRE_DE_DEFECTO") ?? throw new ArgumentNullException("SES_NOMBRE_DE_DEFECTO");
            string correoDeDefecto = System.Environment.GetEnvironmentVariable("SES_CORREO_DE_DEFECTO") ?? throw new ArgumentNullException("SES_CORREO_DE_DEFECTO");

            string workerDirectory = System.Environment.GetEnvironmentVariable("WORKER_DIRECTORY") ?? throw new ArgumentNullException("WORKER_DIRECTORY");
            string workerLambdaHandler = System.Environment.GetEnvironmentVariable("WORKER_LAMBDA_HANDLER") ?? throw new ArgumentNullException("WORKER_LAMBDA_HANDLER");
            string workerLambdaMemorySize = System.Environment.GetEnvironmentVariable("WORKER_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("WORKER_LAMBDA_MEMORY_SIZE");
            string workerLambdaTimeout = System.Environment.GetEnvironmentVariable("WORKER_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("WORKER_LAMBDA_TIMEOUT");

            string notificationEmails = System.Environment.GetEnvironmentVariable("NOTIFICATION_EMAILS") ?? throw new ArgumentNullException("NOTIFICATION_EMAILS");

            #region SNS Topic
            // Se crea SNS topic para notificaciones...
            Topic topic = new(this, $"{appName}NotificationSNSTopic", new TopicProps {
                TopicName = $"{appName}NotificationSNSTopic",
            });

            foreach (string email in notificationEmails.Split(",")) {
                topic.AddSubscription(new EmailSubscription(email));
            }
            #endregion

            #region SQS
            // Creación de cola...
            Queue dlq = new(this, $"{appName}DeadLetterQueue", new QueueProps {
                QueueName = $"{appName}DeadLetterQueue",
                RetentionPeriod = Duration.Days(14),
                EnforceSSL = true,
            });

            Queue queue = new(this, $"{appName}Queue", new QueueProps {
                QueueName = $"{appName}Queue",
                RetentionPeriod = Duration.Days(14),
                VisibilityTimeout = Duration.Seconds(Math.Round(double.Parse(workerLambdaTimeout) * 1.5)),
                EnforceSSL = true,
                DeadLetterQueue = new DeadLetterQueue {
                    Queue = dlq,
                    MaxReceiveCount = 3,
                },
            });

            // Se crea alarma para enviar notificación cuando llegue un elemento al DLQ...
            Alarm alarm = new(this, $"{appName}DeadLetterQueueAlarm", new AlarmProps {
                AlarmName = $"{appName}DeadLetterQueueAlarm",
                AlarmDescription = $"Alarma para notificar cuando llega algun elemento a la DLQ de {appName}",
                Metric = dlq.MetricApproximateNumberOfMessagesVisible(new MetricOptions {
                    Period = Duration.Minutes(5),
                    Statistic = Stats.MAXIMUM,
                }),
                Threshold = 1,
                EvaluationPeriods = 1,
                DatapointsToAlarm = 1,
                ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
                TreatMissingData = TreatMissingData.NOT_BREACHING,
            });
            alarm.AddAlarmAction(new SnsAction(topic));


            StringParameter stringParameterQueueUrl = new(this, $"{appName}StringParameterQueueUrl", new StringParameterProps {
                ParameterName = $"/{appName}/SQS/QueueUrl",
                Description = $"Queue URL de la aplicacion {appName}",
                StringValue = queue.QueueUrl,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region DynamoDB
            // Se crea tabla para registrar los mensajes que se envían...
            Table tablaMensajes = new(this, $"{appName}DynamoDBTableMensajes", new TableProps {
                TableName = $"{appName}Mensajes",
				PartitionKey = new Attribute {
					Name = "IdMensaje",
					Type = AttributeType.STRING
				},
				DeletionProtection = true,
				BillingMode = BillingMode.PAY_PER_REQUEST,
				RemovalPolicy = RemovalPolicy.DESTROY
			});
			#endregion

			#region API Gateway y Lambda
			// Creación de log group lambda...
			LogGroup logGroup = new(this, $"{appName}APILogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}API/logs",
                Retention = RetentionDays.ONE_MONTH,
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
                                }),
								new PolicyStatement(new PolicyStatementProps{
									Sid = $"{appName}AccessToDynamoDB",
									Actions = [
										"dynamodb:PutItem",
										"dynamodb:GetItem",
									],
									Resources = [
										tablaMensajes.TableArn,
									],
								})
							]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function function = new(this, $"{appName}APILambdaFunction", new FunctionProps {
                FunctionName = $"{appName}API",
                Description = $"API encargada de ingresar los correos a la cola de envio de la aplicacion {appName}",
                Runtime = Runtime.DOTNET_8,
                Handler = handler,
                Code = Code.FromAsset($"{apiDirectory}/publish/publish.zip"),
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
                LogGroupName = $"/aws/lambda/{appName}API/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                RestApiName = $"{appName}API",
                Handler = function,
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                DefaultMethodOptions = new MethodOptions {
                    ApiKeyRequired = true,                   
                },
            });

            // Creación de la CfnApiMapping para el API Gateway...
            CfnApiMapping apiMapping = new(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
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
                ApiKeyName = $"{appName}APIKey",
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

            // Se configuran parámetros para ser rescatados por consumidores...
            _ = new StringParameter(this, $"{appName}StringParameterApiUrl", new StringParameterProps {
                ParameterName = $"/{appName}/Api/Url",
                Description = $"API URL de la aplicacion {appName}",
                StringValue = $"https://{apiMapping.DomainName}/{apiMapping.ApiMappingKey}/",
                Tier = ParameterTier.STANDARD,
            });

            _ = new StringParameter(this, $"{appName}StringParameterApiKeyId", new StringParameterProps {
                ParameterName = $"/{appName}/Api/KeyId",
                Description = $"API Key ID de la aplicacion {appName}",
                StringValue = $"{apiGatewayKey.KeyId}",
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region Lambda Worker Envio Correo
            StringParameter stringParameterDireccionDeDefecto = new(this, $"{appName}StringParameterDireccionDeDefecto", new StringParameterProps {
                ParameterName = $"/{appName}/SES/DireccionDeDefecto",
                Description = $"Dirección por defecto para emitir correos de la aplicacion {appName}",
                StringValue = JsonConvert.SerializeObject(new {
                    Nombre = nombreDeDefecto,
                    Correo = correoDeDefecto
                }),
                Tier = ParameterTier.STANDARD,
            });

            // Creación de log group lambda...
            LogGroup workerLogGroup = new(this, $"{appName}WorkerLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}WorkerEnvioCorreo/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación de role para la función lambda...
            Role roleWorkerLambda = new(this, $"{appName}WorkerLambdaRole", new RoleProps {
                RoleName = $"{appName}WorkerEnvioCorreoLambdaRole",
                Description = $"Role para Lambda Worker Envio Correo de {appName}",
                AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
                ManagedPolicies = [
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ],
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}WorkerEnvioCorreoLambdaPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterDireccionDeDefecto.ParameterArn,
                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToSendEmail",
                                    Actions = [
                                        "ses:SendEmail",
                                        "ses:GetAccount"
                                    ],
                                    Resources = [
                                        $"*",
                                    ],
                                }),
								new PolicyStatement(new PolicyStatementProps{
									Sid = $"{appName}AccessToDynamoDB",
									Actions = [
										"dynamodb:PutItem",
										"dynamodb:GetItem",
									],
									Resources = [
										tablaMensajes.TableArn,
									],
								})
							]
                        })
                    }
                }
            });

            // Creación de la función lambda...
            Function workerFunction = new(this, $"{appName}WorkerLambdaFunction", new FunctionProps {
                FunctionName = $"{appName}WorkerEnvioCorreo",
                Description = $"Funcion worker encargada de enviar correos desde la cola de la aplicacion {appName}",
                Runtime = Runtime.DOTNET_8,
                Handler = workerLambdaHandler,
                Code = Code.FromAsset($"{workerDirectory}/publish/publish.zip"),
                Timeout = Duration.Seconds(double.Parse(workerLambdaTimeout)),
                MemorySize = double.Parse(workerLambdaMemorySize),
                Architecture = Architecture.X86_64,
                LogGroup = workerLogGroup,
                Environment = new Dictionary<string, string> {
                    { "APP_NAME", appName },
                },
                Role = roleWorkerLambda,
                ReservedConcurrentExecutions = 1
            });

            workerFunction.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps {
                Enabled = true,
                ReportBatchItemFailures = true,
            }));
            #endregion
        }
    }
}
