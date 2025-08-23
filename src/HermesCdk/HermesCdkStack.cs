using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.Batch;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.SSM;
using Constructs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using StageOptions = Amazon.CDK.AWS.APIGateway.StageOptions;

namespace HermesCdk {
    public class HermesCdkStack : Stack
    {
        internal HermesCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

            #region SQS
            // Creación de cola...
            Queue queue = new(this, $"{appName}Queue", new QueueProps {
                QueueName = $"{appName}Queue",
                RetentionPeriod = Duration.Days(14),
                VisibilityTimeout = Duration.Minutes(5),
                EnforceSSL = true,
            });

            StringParameter stringParameterQueueUrl = new(this, $"{appName}StringParameterQueueUrl", new StringParameterProps {
                ParameterName = $"/{appName}/SQS/QueueUrl",
                Description = $"Queue URL de la aplicacion {appName}",
                StringValue = queue.QueueUrl,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region Cognito
            // Se crea el user pool...
            UserPool userPool = new(this, $"{appName}UserPool", new UserPoolProps {
                UserPoolName = $"{appName}UserPool",
                SelfSignUpEnabled = false,
                DeletionProtection = true,
                RemovalPolicy = RemovalPolicy.DESTROY,
            });

            // Se crean los scopes que permitirán acciones en la API...
            ResourceServerScope scopeEnviarCorreo = new(new ResourceServerScopeProps {
                ScopeName = "enviar_correo",
                ScopeDescription = "Permite ingresar solicitudes de envío de correo",
            });

            // Se crean resource server para los scopes...
            UserPoolResourceServer resourceServer = new(this, $"{appName}ResourceServer", new UserPoolResourceServerProps {
                UserPoolResourceServerName = $"{appName}ResourceServer",
                Identifier = "api",
                UserPool = userPool,
                Scopes = [ scopeEnviarCorreo ]
            });

            // Se crea el user pool client...
            UserPoolClient userPoolClient = new(this, $"{appName}UserPoolClient", new UserPoolClientProps {
                UserPoolClientName = $"{appName}PersonalUserPoolClient",
                UserPool = userPool,
                GenerateSecret = true,
                AuthFlows = new AuthFlow { 
                    UserSrp = true 
                },
                OAuth = new OAuthSettings {
                    Flows = new OAuthFlows {
                        ClientCredentials = true,
                    },
                    Scopes = [
                        OAuthScope.ResourceServer(resourceServer, scopeEnviarCorreo),
                    ],
                },
            });

            // Se crean parametros para poder ser rescatados desde la API...
            StringParameter stringParameterUserPoolId = new(this, $"{appName}StringParameterUserPoolId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolId",
                Description = $"User Pool ID de la aplicacion {appName}",
                StringValue = userPool.UserPoolId,
                Tier = ParameterTier.STANDARD,
            });

            StringParameter stringParameterUserPoolClientId = new(this, $"{appName}StringParameterUserPoolClientId", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/UserPoolClientId",
                Description = $"User Pool Client ID de la aplicacion {appName}",
                StringValue = userPoolClient.UserPoolClientId,
                Tier = ParameterTier.STANDARD,
            });

            StringParameter stringParameterRegion = new(this, $"{appName}StringParameterCognitoRegion", new StringParameterProps {
                ParameterName = $"/{appName}/Cognito/Region",
                Description = $"Cognito Region de la aplicacion {appName}",
                StringValue = region,
                Tier = ParameterTier.STANDARD,
            });
            #endregion

            #region API Gateway y Lambda
            string apiDirectory = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_DIRECTORY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_DIRECTORY");
            string handler = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_HANDLER") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_HANDLER");
            string timeout = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_TIMEOUT") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_TIMEOUT");
            string memorySize = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE") ?? throw new ArgumentNullException("AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE");
            string domainName = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_DOMAIN_NAME");
            string apiMappingKey = System.Environment.GetEnvironmentVariable("AOT_MINIMAL_API_MAPPING_KEY") ?? throw new ArgumentNullException("AOT_MINIMAL_API_MAPPING_KEY");


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
                                        stringParameterUserPoolId.ParameterArn,
                                        stringParameterUserPoolClientId.ParameterArn,
                                        stringParameterRegion.ParameterArn,
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
                FunctionName = $"{appName}APILambdaFunction",
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
                    { "PARAMETER_ARN_COGNITO_USER_POOL_ID", stringParameterUserPoolId.ParameterArn },
                    { "PARAMETER_ARN_COGNITO_USER_POOL_CLIENT_ID", stringParameterUserPoolClientId.ParameterArn },
                    { "PARAMETER_ARN_COGNITO_REGION", stringParameterRegion.ParameterArn },
                },
                Role = roleLambda,
            });

            // Creación de access logs...
            LogGroup logGroupAccessLogs = new(this, $"{appName}APILambdaFunctionLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/lambda/{appName}APILambdaFunction/access_logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            // Creación autorizer para el user pool creado...
            CognitoUserPoolsAuthorizer cognitoUserPoolsAuthorizer = new(this, $"{appName}APIAuthorizer", new CognitoUserPoolsAuthorizerProps {
                CognitoUserPools = [userPool],
                AuthorizerName = $"{appName}APIAuthorizer",
            });

            // Creación de la LambdaRestApi...
            LambdaRestApi lambdaRestApi = new(this, $"{appName}APILambdaRestApi", new LambdaRestApiProps {
                RestApiName = $"{appName}APILambdaRestApi",
                Handler = function,
                DeployOptions = new StageOptions {
                    AccessLogDestination = new LogGroupLogDestination(logGroupAccessLogs),
                    AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
                    StageName = "prod",
                    Description = $"Stage para produccion de la aplicacion {appName}",
                },
                DefaultMethodOptions = new MethodOptions {
                    AuthorizationType = AuthorizationType.COGNITO,
                    Authorizer = cognitoUserPoolsAuthorizer
                },
            });

            // Creación de la CfnApiMapping para el API Gateway...
            _ = new CfnApiMapping(this, $"{appName}APIApiMapping", new CfnApiMappingProps {
                DomainName = domainName,
                ApiMappingKey = apiMappingKey,
                ApiId = lambdaRestApi.RestApiId,
                Stage = lambdaRestApi.DeploymentStage.StageName,
            });

            // Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
            ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
            Permission permission = new() {
                Scope = this,
                Action = "lambda:InvokeFunction",
                Principal = arnPrincipal,
                SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{lambdaRestApi.RestApiId}/*/*/*",
            };
            function.AddPermission($"{appName}APIPermission", permission);
            #endregion

            #region ECS y Fargate
            string vpcId = System.Environment.GetEnvironmentVariable("VPC_ID") ?? throw new ArgumentNullException("VPC_ID");
            string subnetId1 = System.Environment.GetEnvironmentVariable("SUBNET_ID_1") ?? throw new ArgumentNullException("SUBNET_ID_1");
            string subnetId2 = System.Environment.GetEnvironmentVariable("SUBNET_ID_2") ?? throw new ArgumentNullException("SUBNET_ID_2");
            double fargateCpu = double.Parse(System.Environment.GetEnvironmentVariable("FARGATE_CPU") ?? throw new ArgumentNullException("FARGATE_CPU"));
            double fargateMemory = double.Parse(System.Environment.GetEnvironmentVariable("FARGATE_MEMORY") ?? throw new ArgumentNullException("FARGATE_MEMORY"));
            string fargateDirectory = System.Environment.GetEnvironmentVariable("FARGATE_DIRECTORY") ?? throw new ArgumentNullException("FARGATE_DIRECTORY");
            string nombreDeDefecto = System.Environment.GetEnvironmentVariable("SES_NOMBRE_DE_DEFECTO") ?? throw new ArgumentNullException("SES_NOMBRE_DE_DEFECTO");
            string correoDeDefecto = System.Environment.GetEnvironmentVariable("SES_CORREO_DE_DEFECTO") ?? throw new ArgumentNullException("SES_CORREO_DE_DEFECTO");

            StringParameter stringParameterDireccionDeDefecto = new(this, $"{appName}StringParameterDireccionDeDefecto", new StringParameterProps {
                ParameterName = $"/{appName}/SES/DireccionDeDefecto",
                Description = $"Dirección por defecto para emitir correos de la aplicacion {appName}",
                StringValue = JsonConvert.SerializeObject(new {
                    Nombre = nombreDeDefecto,
                    Correo = correoDeDefecto
                }),
                Tier = ParameterTier.STANDARD,
            });

            IVpc vpc = Vpc.FromLookup(this, $"{appName}Vpc", new VpcLookupOptions {
                VpcId = vpcId
            });

            Cluster cluster = new(this, $"{appName}ECSCluster", new ClusterProps {
                ClusterName = $"{appName}ECSCluster",
                Vpc = vpc,
            });

            Role taskRole = new(this, $"{appName}ECSTaskRole", new RoleProps {
                RoleName = $"{appName}ECSTaskRole",
                Description = $"Role para ECS Task de {appName}",
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
                InlinePolicies = new Dictionary<string, PolicyDocument> {
                    {
                        $"{appName}ECSTaskPolicy",
                        new PolicyDocument(new PolicyDocumentProps {
                            Statements = [
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToParameterStore",
                                    Actions = [
                                        "ssm:GetParameter"
                                    ],
                                    Resources = [
                                        stringParameterQueueUrl.ParameterArn,
                                        stringParameterDireccionDeDefecto.ParameterArn,

                                    ],
                                }),
                                new PolicyStatement(new PolicyStatementProps{
                                    Sid = $"{appName}AccessToQueue",
                                    Actions = [
                                        "sqs:ReceiveMessage",
                                        "sqs:DeleteMessage",

                                    ],
                                    Resources = [
                                        queue.QueueArn,
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
                            ]
                        })
                    }
                }
            });

            FargateTaskDefinition taskDefinition = new(this, $"{appName}FargateTaskDefinition", new FargateTaskDefinitionProps {
                Cpu = fargateCpu,
                MemoryLimitMiB = fargateMemory,
                TaskRole = taskRole,
            });

            LogGroup logGroupContainer = new(this, $"{appName}ContainerLogGroup", new LogGroupProps {
                LogGroupName = $"/aws/ecs/{appName}/logs",
                Retention = RetentionDays.ONE_MONTH,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            taskDefinition.AddContainer($"{appName}TaskContainer", new ContainerDefinitionOptions {
                ContainerName = $"{appName}TaskContainer",
                Image = ContainerImage.FromAsset(fargateDirectory, new AssetImageProps {
                    AssetName = $"{appName}Image",
                }),
                Logging = LogDriver.AwsLogs(new AwsLogDriverProps {
                    StreamPrefix = appName,
                    LogGroup = logGroupContainer,
                }),
                Environment = new Dictionary<string, string> {
                    { "PARAMETER_ARN_SQS_QUEUE_URL", stringParameterQueueUrl.ParameterArn },
                    { "PARAMETER_ARN_DIRECCION_DE_DEFECTO", stringParameterDireccionDeDefecto.ParameterArn }
                },
            });

            // Fargate Service se ejecutarán en red privada con acceso a internet, para poder descargar imagen y acceder a otros servicios públicos...
            ISubnet subnetPrivateWithInternet1 = Subnet.FromSubnetId(this, $"{appName}Subnet1", subnetId1);
            ISubnet subnetPrivateWithInternet2 = Subnet.FromSubnetId(this, $"{appName}Subnet2", subnetId2);

            SecurityGroup securityGroup = new(this, $"{appName}FargateServiceSecurityGroup", new SecurityGroupProps {
                Vpc = vpc,
                SecurityGroupName = $"{appName}FargateServiceSecurityGroup",
                Description = $"{appName} Fargate Service Security Group",
                AllowAllOutbound = true,
            });

            FargateService fargateService = new(this, $"{appName}FargateService", new FargateServiceProps {
                ServiceName = $"{appName}FargateService",
                Cluster = cluster,
                TaskDefinition = taskDefinition,
                DesiredCount = 0,
                VpcSubnets = new SubnetSelection { 
                    Subnets = [ subnetPrivateWithInternet1, subnetPrivateWithInternet2]
                },
                SecurityGroups = [ securityGroup ]
            });

            // Se crea capacidad de escalar según cantidad de elementos en la cola, si no hay elementos entonces no se ejecuta el task...
            ScalableTaskCount scalableTaskCount = fargateService.AutoScaleTaskCount(new EnableScalingProps {
                MinCapacity = 0,
                MaxCapacity = 1
            });

            scalableTaskCount.ScaleOnMetric($"{appName}ScaleOnMetric", new BasicStepScalingPolicyProps {
                Metric = queue.MetricApproximateNumberOfMessagesVisible(new MetricOptions {
                    Period = Duration.Minutes(1),
                }),
                ScalingSteps = [ 
                    new ScalingInterval { Upper = 0, Change = 0 },
                    new ScalingInterval { Lower = 1, Change = 1 },
                ],
                AdjustmentType = AdjustmentType.EXACT_CAPACITY,
            });
            #endregion
        }
    }
}
