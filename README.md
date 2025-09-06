# Hermes - Envío Masivo de Correos

- [Hermes - Envío Masivo de Correos](#hermes---envío-masivo-de-correos)
  - [Introducción](#introducción)
    - [Diagrama Arquitectura](#diagrama-arquitectura)
  - [Recursos Requeridos](#recursos-requeridos)
    - [API Gateway Custom Domain Name](#api-gateway-custom-domain-name)
    - [SES](#ses)
  - [Recursos Creados](#recursos-creados)
    - [Sistema de Colas](#sistema-de-colas)
      - [SQS Queue y Dead Letter Queue](#sqs-queue-y-dead-letter-queue)
      - [SNS Topic y CloudWatch Alarm](#sns-topic-y-cloudwatch-alarm)
      - [Systems Manager String Parameter](#systems-manager-string-parameter)
    - [API Recepcion Solicitudes Envio](#api-recepcion-solicitudes-envio)
      - [Log Group e IAM Role](#log-group-e-iam-role)
      - [Lambda Function](#lambda-function)
      - [Access Log Group](#access-log-group)
      - [Lambda Rest API](#lambda-rest-api)
      - [API Mapping](#api-mapping)
      - [Usage Plan y API Key](#usage-plan-y-api-key)
      - [API Gateway Permission](#api-gateway-permission)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-1)
    - [Lambda Worker Envio Correos](#lambda-worker-envio-correos)
      - [Systems Manager String Parameter](#systems-manager-string-parameter-2)
      - [Log Group e IAM Role](#log-group-e-iam-role-1)
      - [Lambda Function (con Event Source)](#lambda-function-con-event-source)
  - [Lógica de Lambdas](#lógica-de-lambdas)
    - [API de Recepción de Solicitudes de Envío](#api-de-recepción-de-solicitudes-de-envío)
      - [Endpoint](#endpoint)
      - [Código](#código)
    - [Lambda Worker de Envío de Correos](#lambda-worker-de-envío-de-correos)
      - [Código](#código-1)
  - [Despliegue](#despliegue)
    - [Variables y Secretos de Entorno](#variables-y-secretos-de-entorno)

## Introducción

* Hermes es una herramienta para el envío masivo de correos.
* El siguiente repositorio es para desplegar Hermes, lo que incluye la creación de [Lambdas](https://aws.amazon.com/es/lambda/), [API Gateway](https://aws.amazon.com/es/api-gateway/), [SQS Queues](https://aws.amazon.com/es/sqs/), [CloudWatch Alarms](https://aws.amazon.com/es/cloudwatch/), [SNS Topics](https://aws.amazon.com/es/sns/).
* La infraestructura se despliega mediante IaC, usando [AWS CDK en .NET 8.0](https://docs.aws.amazon.com/cdk/api/v2/dotnet/api/).
* El despliegue CI/CD se lleva a cabo mediante  [GitHub Actions](https://github.com/features/actions).

### Diagrama Arquitectura

![Diagrama de Hermes](./images/ArquitecturaHermes.drawio.png)

## Recursos Requeridos

### API Gateway Custom Domain Name

Es necesario contar con un Custom Domain Name ya asociado a API Gateway, esto dado a que se usará para crear el API Mapping.

<ins>Código donde se usará Custom Domain Name</ins>

```csharp
using Amazon.CDK.AWS.Apigatewayv2;

// Creación de la CfnApiMapping para el API Gateway...
CfnApiMapping apiMapping = new(this, ..., new CfnApiMappingProps {
    DomainName = domainName,
    ApiMappingKey = ...,
    ApiId = ...,
    Stage = ...,
});
```

Para ver un ejemplo de como crear un Custom Domain Name: [BDiazEApiGatewayCDK](https://github.com/bdiaze/BDiazEApiGatewayCDK)

### SES

Además, es necesario contar con un SES Identity ya verificado, el cual será usado en el Worker de Envio de Correos para proceder con las notificaciones.

<ins>Código donde se enviará correo mediante SES</ins>

```csharp
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;

SendEmailRequest request = new() {
    FromEmailAddress = ...,
    Destination = ...,
    ReplyToAddresses = ...,
    Content = ...
};

SendEmailResponse response = await sesClient.SendEmailAsync(request);
if (response.HttpStatusCode != HttpStatusCode.OK) {
    throw new Exception(...);
}
```

Para ver un ejemplo de como crear un SES Identity: [BDiazESimpleEmailServiceCdk](https://github.com/bdiaze/BDiazESimpleEmailServiceCdk)

## Recursos Creados

### Sistema de Colas

Como parte de la solución, se cuenta con un sistema de colas para no saturar al servicio SES de AWS, el cual cuenta con una capacidad máxima de envío de correos por segundo.

#### SQS Queue y Dead Letter Queue

Primero se creará la cola que almacenerá los correos a enviar y que tiene como objetivo no saturar al servicio SES.

<ins>Código para crear SQS Queue y DLQ:</ins>

```csharp
using Amazon.CDK.AWS.SQS;

// Creación de cola...
Queue dlq = new(this, ..., new QueueProps {
    QueueName = ...,
    RetentionPeriod = Duration.Days(14),
    EnforceSSL = true,
});

Queue queue = new(this, ..., new QueueProps {
    QueueName = ...,
    RetentionPeriod = Duration.Days(14),
    VisibilityTimeout = Duration.Seconds(Math.Round(double.Parse(...) * 1.5)),
    EnforceSSL = true,
    DeadLetterQueue = new DeadLetterQueue {
        Queue = dlq,
        MaxReceiveCount = 3,
    },
});
```

> [!NOTE]
> Como se puede observar en el código de anterior, primero se crea la DLQ (Dead Letter Queue) y posteriormente se crea la Queue que almacenerá los correos a enviar. La DLQ se usará para almacenar los correos fallido para su posterior manejo.

#### SNS Topic y CloudWatch Alarm

Además, se creará un sistema de notificación que monitoree la DLQ y mande correos informando cuando ha fallado un envío, de tal manera que el equipo responsable pueda mitigar el error.

<ins>Código para crear SNS Topic y CloudWatch Alarm</ins>

```csharp
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;

// Se crea SNS topic para notificaciones asociadas a la instancia...
Topic topic = new(this, ..., new TopicProps {
    TopicName = ...,
});

foreach (string email in notificationEmails.Split(",")) {
    topic.AddSubscription(new EmailSubscription(email));
}

// Se crea alarma para enviar notificación cuando llegue un elemento al DLQ...
Alarm alarm = new(this, ..., new AlarmProps {
    AlarmName = ...,
    AlarmDescription = ...,
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
```

#### Systems Manager String Parameter

Por último, se crea un String Parameter que contendrá la URL de la Queue donde se dejarán los correos a enviar. Esta URL será usada en la API de recepción de solicitudes de envío.

<ins>Código para crear String Parameter</ins>

```csharp
using Amazon.CDK.AWS.SSM;
StringParameter stringParameterQueueUrl = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/SQS/QueueUrl",
    Description = ...,
    StringValue = queue.QueueUrl,
    Tier = ParameterTier.STANDARD,
});
```

### API Recepcion Solicitudes Envio

En segundo lugar, se creará la API de recepción de solicitudes de envío de correos. Esta API dejará todos los correos a enviar en la [cola previamente creada](#sqs-queue-y-dead-letter-queue) para su posterior procesamiento.

#### Log Group e IAM Role

Se comienza creando los recursos base de una Lambda, es decir, el Log Group y el Role.

<ins>Código para crear Log Group y Role</ins>

```csharp
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;

// Creación de log group lambda...
LogGroup logGroup = new(this, ..., new LogGroupProps { 
    LogGroupName = $"/aws/lambda/{...}APILambdaFunction/logs",
    RemovalPolicy = RemovalPolicy.DESTROY
});

// Creación de role para la función lambda...
IRole roleLambda = new Role(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
    ManagedPolicies = [
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
    ],
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "ssm:GetParameter"
                        ],
                        Resources = [
                            ...,
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "sqs:SendMessage"
                        ],
                        Resources = [
                            ...,
                        ],
                    })
                ]
            })
        }
    }
});
```

> [!NOTE]
> Destacar como el Role de la Lambda requiere acceso al [String Parameter creado anteriormente](#systems-manager-string-parameter) (que contiene la URL de la Queue donde se dejarán los emails a enviar), además requiere acceso para dejar mensajes en esa misma Queue.

#### Lambda Function

Se continua con la creación de la función Lambda correspondiente a la Minimal API AoT que recepcionará las solicitudes. Para entender como se publica y compila el código, dirigirse a la sección de [Despliegue](#despliegue).

<ins>Código para crear Lambda Function:</ins>

```csharp
using Amazon.CDK.AWS.Lambda;

// Creación de la función lambda...
Function function = new(this, ..., new FunctionProps {
    FunctionName = ...,
    Description = ...,
    Runtime = Runtime.DOTNET_8,
    Handler = handler,
    Code = Code.FromAsset($"{...}/publish/publish.zip"),
    Timeout = Duration.Seconds(double.Parse(timeout)),
    MemorySize = double.Parse(memorySize),
    Architecture = Architecture.X86_64,
    LogGroup = logGroup,
    Environment = new Dictionary<string, string> {
        { "APP_NAME", ... },
        { "PARAMETER_ARN_SQS_QUEUE_URL", ... },
    },
    Role = roleLambda,
});

```

#### Access Log Group

<ins>Código para crear Log Group:</ins>

Por otro lado, se crea el access log group a usar por API Gateway.

```csharp
using Amazon.CDK.AWS.Logs;

// Creación de access logs...
LogGroup logGroupAccessLogs = new(this, ..., new LogGroupProps {
    LogGroupName = $"/aws/lambda/{...}APILambdaFunction/access_logs",
    Retention = RetentionDays.ONE_MONTH,
    RemovalPolicy = RemovalPolicy.DESTROY
});
```

#### Lambda Rest API

Se configura los endpoints de API Gateway con LambdaRestApi. En esta parte es donde se configura el uso de API Key para autenticación.

<ins>Código para crear Lambda Rest Api:</ins>

```csharp
using Amazon.CDK.AWS.APIGateway;

// Creación de la LambdaRestApi...
LambdaRestApi lambdaRestApi = new(this, ..., new LambdaRestApiProps {
    RestApiName = ...,
    Handler = ...,
    DeployOptions = new StageOptions {
        AccessLogDestination = new LogGroupLogDestination(...),
        AccessLogFormat = AccessLogFormat.Custom("'{\"requestTime\":\"$context.requestTime\",\"requestId\":\"$context.requestId\",\"httpMethod\":\"$context.httpMethod\",\"path\":\"$context.path\",\"resourcePath\":\"$context.resourcePath\",\"status\":$context.status,\"responseLatency\":$context.responseLatency,\"xrayTraceId\":\"$context.xrayTraceId\",\"integrationRequestId\":\"$context.integration.requestId\",\"functionResponseStatus\":\"$context.integration.status\",\"integrationLatency\":\"$context.integration.latency\",\"integrationServiceStatus\":\"$context.integration.integrationStatus\",\"authorizeStatus\":\"$context.authorize.status\",\"authorizerStatus\":\"$context.authorizer.status\",\"authorizerLatency\":\"$context.authorizer.latency\",\"authorizerRequestId\":\"$context.authorizer.requestId\",\"ip\":\"$context.identity.sourceIp\",\"userAgent\":\"$context.identity.userAgent\",\"principalId\":\"$context.authorizer.principalId\"}'"),
        StageName = ...,
        Description = ...,
    },
    DefaultMethodOptions = new MethodOptions {
        ApiKeyRequired = true,                   
    },
});
```

> [!NOTE]
> Si deseas conocer más detalles del formato usado para los access logs, recomiendo la lectura de ["The Missing Guide to AWS API Gateway Access Logs"](https://www.alexdebrie.com/posts/api-gateway-access-logs/) por Alex DeBrie.

#### API Mapping

Se crea API Mapping para redireccionar solicitudes desde el Custom Domain de API Gateway a la API en cuestión.

<ins>Código para crear API Mapping:</ins>

```csharp
using Amazon.CDK.AWS.Apigatewayv2;

// Creación de la CfnApiMapping para el API Gateway...
CfnApiMapping apiMapping = new(this, ..., new CfnApiMappingProps {
    DomainName = ...,
    ApiMappingKey = ...,
    ApiId = ...,
    Stage = ...,
});
```

#### Usage Plan y API Key

Se crea Usage Plan y API Key para restringir el consumo del servicio.

<ins>Código para crear Usage Plan y API Key:</ins>

```csharp
using Amazon.CDK.AWS.APIGateway;

// Se crea Usage Plan para configurar API Key...
UsagePlan usagePlan = new(this, ..., new UsagePlanProps {
    Name = ...,
    Description = ...,
    ApiStages = [
        new UsagePlanPerApiStage() {
            Api = ...,
            Stage = ...
        }
    ],
});

// Se crea API Key...
ApiKey apiGatewayKey = new(this, ..., new ApiKeyProps {
    ApiKeyName = ...,
    Description = ...,
});
usagePlan.AddApiKey(apiGatewayKey);
```

#### API Gateway Permission

Se crea el permiso que permitirá a API Gateway invocar a la Lambda.

<ins>Código para crear API Gateway Permission:</ins>

```csharp
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;

// Se configura permisos para la ejecucíon de la Lambda desde el API Gateway...
ArnPrincipal arnPrincipal = new("apigateway.amazonaws.com");
Permission permission = new() {
    Scope = this,
    Action = "lambda:InvokeFunction",
    Principal = arnPrincipal,
    SourceArn = $"arn:aws:execute-api:{...}:{...}:{...}/*/*/*",
};
function.AddPermission(..., permission);

```

#### Systems Manager String Parameter

Se crean String Parameters para contener la URL y el ID de la API Key de la Minimal API AoT.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Api/Url",
    Description = ...,
    StringValue = $"https://{...}/{...}/",
    Tier = ParameterTier.STANDARD,
});

_ = new StringParameter(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/Api/KeyId",
    Description = ...,
    StringValue = $"{...}",
    Tier = ParameterTier.STANDARD,
});
```

### Lambda Worker Envio Correos

En tercer lugar, se creará una Lambda cuyo trabajo será procesar los [mensajes de la cola](#sqs-queue-y-dead-letter-queue) y enviar los correos respectivos.

#### Systems Manager String Parameter

Se comenzará creando un String Parameter que contendrá los valores por defecto para el remitente de los correos.

<ins>Código para crear String Parameter:</ins>

```csharp
using Amazon.CDK.AWS.SSM;

StringParameter stringParameterDireccionDeDefecto = new(this, ..., new StringParameterProps {
    ParameterName = $"/{...}/SES/DireccionDeDefecto",
    Description = ...,
    StringValue = JsonConvert.SerializeObject(new {
        Nombre = ...,
        Correo = ...
    }),
    Tier = ParameterTier.STANDARD,
});
```

#### Log Group e IAM Role

Se crea Log Group e IAM Role para Lambda Worker.

<ins>Código para crear Log Group y Role:</ins>

```csharp
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.IAM;

// Creación de log group lambda...
LogGroup workerLogGroup = new(this, ..., new LogGroupProps {
    LogGroupName = $"/aws/lambda/{...}WorkerEnvioCorreo/logs",
    RemovalPolicy = RemovalPolicy.DESTROY
});

Role roleWorkerLambda = new(this, ..., new RoleProps {
    RoleName = ...,
    Description = ...,
    AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
    ManagedPolicies = [
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"),
        ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
    ],
    InlinePolicies = new Dictionary<string, PolicyDocument> {
        {
            ...,
            new PolicyDocument(new PolicyDocumentProps {
                Statements = [
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
                        Actions = [
                            "ssm:GetParameter"
                        ],
                        Resources = [
                            ...,
                        ],
                    }),
                    new PolicyStatement(new PolicyStatementProps{
                        Sid = ...,
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
```
> [!NOTE]
> Se destaca que el Role requiere acceso al [String Parameter creado anteriormente](#systems-manager-string-parameter-2) y acceso para enviar correos y obtener quota de SES.

#### Lambda Function (con Event Source)

Se crea función Lambda para el Worker de Envio de Correos, y además se le añade SQS Event Source con la [queue ya creada](#sqs-queue-y-dead-letter-queue).

<ins>Código para crear Lambda con Event Source:</ins>

```csharp
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;

// Creación de la función lambda...
Function workerFunction = new(this, ..., new FunctionProps {
    FunctionName = ...,
    Description = ...,
    Runtime = Runtime.DOTNET_8,
    Handler = ...,
    Code = Code.FromAsset(...),
    Timeout = Duration.Seconds(double.Parse(...)),
    MemorySize = double.Parse(...),
    Architecture = Architecture.X86_64,
    LogGroup = ...,
    Environment = new Dictionary<string, string> {
        { "APP_NAME", ... },
    },
    Role = ...,
    ReservedConcurrentExecutions = 1
});

workerFunction.AddEventSource(new SqsEventSource(..., new SqsEventSourceProps {
    Enabled = true,
    BatchSize = Math.Round(double.Parse(...) * 5 * 0.5),
    MaxBatchingWindow = Duration.Seconds(15),
    ReportBatchItemFailures = true,
}));
```

## Lógica de Lambdas

### API de Recepción de Solicitudes de Envío

El principal proposito de la API es recepcionar la información del correo que se desea enviar y direccionar a la cola de procesamiento. Por este motivo la API solo contiene un endpoint:

#### Endpoint
| URL | Método | Cuerpo | Retorno |
|-----|--------|------------|---------|
| `/Correo/Enviar` | POST | <code>{\\n&nbsp;&nbsp;"de": {<br>&nbsp;&nbsp;&nbsp;&nbsp;"nombre": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;"correo": "..."<br>&nbsp;&nbsp;},<br>&nbsp;&nbsp;"para": [<br>&nbsp;&nbsp;&nbsp;&nbsp;{<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"nombre": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"correo": "..."<br>&nbsp;&nbsp;&nbsp;&nbsp;},<br>&nbsp;&nbsp;&nbsp;&nbsp;...<br>&nbsp;&nbsp;],<br>&nbsp;&nbsp;"cc": [<br>&nbsp;&nbsp;&nbsp;&nbsp;{<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"nombre": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"correo": "..."<br>&nbsp;&nbsp;&nbsp;&nbsp;},<br>&nbsp;&nbsp;&nbsp;&nbsp;...<br>&nbsp;&nbsp;],<br>&nbsp;&nbsp;"cco": [<br>&nbsp;&nbsp;&nbsp;&nbsp;{<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"nombre": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"correo": "..."<br>&nbsp;&nbsp;&nbsp;&nbsp;},<br>&nbsp;&nbsp;&nbsp;&nbsp;...<br>&nbsp;&nbsp;],<br>&nbsp;&nbsp;"responderA": [<br>&nbsp;&nbsp;&nbsp;&nbsp;{<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"nombre": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"correo": "..."<br>&nbsp;&nbsp;&nbsp;&nbsp;},<br>&nbsp;&nbsp;&nbsp;&nbsp;...<br>&nbsp;&nbsp;],<br>&nbsp;&nbsp;"asunto": "...",<br>&nbsp;&nbsp;"cuerpo": "...",<br>&nbsp;&nbsp;"adjuntos": [<br>&nbsp;&nbsp;&nbsp;&nbsp;{<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"nombreArchivo": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"tipoMime": "...",<br>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"contenidoBase64": "..."<br>&nbsp;&nbsp;&nbsp;&nbsp;},<br>&nbsp;&nbsp;&nbsp;&nbsp;...<br>&nbsp;&nbsp;]<br>}</code> | <code>{<br>&nbsp;&nbsp;"queueMessageId": "..."<br>}</code> |

#### Código

```csharp
var correoApi = app.MapGroup("/Correo");
correoApi.MapPost("/Enviar", async (Correo correo, IAmazonSQS sqsClient, IConfiguration config) => {
    Stopwatch stopwatch = Stopwatch.StartNew();

    try {
        string jsonCorreo = JsonSerializer.Serialize(correo, typeof(Correo), AppJsonSerializerContext.Default);

        SendMessageRequest request = new() {
            QueueUrl = ...,
            MessageBody = jsonCorreo
        };

        SendMessageResponse response = await sqsClient.SendMessageAsync(request);
        Retorno salida = new(response.MessageId);

        return Results.Ok(salida);
    } catch(Exception ex) {
        return Results.Problem(...);
    }
});
```

### Lambda Worker de Envío de Correos

Por otro lado, el principal proposito de la Lambda Worker es enviar los correos que se encuentran en la cola mediante SES.

#### Código

```csharp
public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context)
{
    List<BatchItemFailure> listaMensajesError = [];

    Stopwatch stopwatch = Stopwatch.StartNew();
    Stopwatch stopwatchDelay = Stopwatch.StartNew();

    VariableEntornoHelper variableEntorno = ...;
    ParameterStoreHelper parameterStore = ...;
    IAmazonSimpleEmailServiceV2 sesClient = ...;

    // Obteniendo dirección de correos por defecto a usar como remitente...
    DireccionCorreo direccionDeDefecto = JsonSerializer.Deserialize<DireccionCorreo>(await parameterStore.ObtenerParametro($"/{...}/SES/DireccionDeDefecto"))!;

    // Obteniendo límites de SES para configurar cantidad de elementos a extraer de al cola y las esperas entre envíos de correos...
    GetAccountResponse accountResponse = await sesClient.GetAccountAsync(new GetAccountRequest());
    int maxDelayMs = (int)(1000 / accountResponse.SendQuota.MaxSendRate!);
    int cantEmailsDisponibles = (int)(accountResponse.SendQuota.Max24HourSend! - accountResponse.SendQuota.SentLast24Hours!);

    // Si no quedan correos por enviar, se retornan todos los elementos para reprocesar...
    if (cantEmailsDisponibles == 0) {
        return new SQSBatchResponse {
            BatchItemFailures = [.. evnt.Records.Select(r => new BatchItemFailure { ItemIdentifier = r.MessageId })]
        };
    }

    foreach (SQSMessage mensaje in evnt.Records) {
        try {
            Correo correo = JsonSerializer.Deserialize<Correo>(mensaje.Body)!;
            correo.De ??= direccionDeDefecto;

            List<Attachment>? attachments = null;
            if (correo.Adjuntos != null && correo.Adjuntos.Count > 0) {
                attachments = [];
                foreach (Adjunto adjunto in correo.Adjuntos) {
                    attachments.Add(new Attachment { ... });
                }
            }

            SendEmailRequest request = new() {
                FromEmailAddress = ...,
                Destination = ...,
                ReplyToAddresses = ...,
                Content = ...
            };

            if (stopwatchDelay.ElapsedMilliseconds < maxDelayMs) {
                int delayMs = maxDelayMs - (int)stopwatchDelay.ElapsedMilliseconds;
                await Task.Delay(delayMs);
            }

            SendEmailResponse response = await sesClient.SendEmailAsync(request);
            if (response.HttpStatusCode != HttpStatusCode.OK) {
                throw new Exception(...);
            }

            stopwatchDelay = Stopwatch.StartNew();
        } catch (Exception ex) {
            listaMensajesError.Add(new BatchItemFailure {
                ItemIdentifier = mensaje.MessageId,
            });
        }
    }

    return new SQSBatchResponse {
        BatchItemFailures = listaMensajesError
    };
}
```

## Despliegue

El despliegue se lleva a cabo mediante GitHub Actions, para ello se configura la receta de despliegue con los siguientes pasos:

| Paso | Comando | Descripción |
|------|---------|-------------|
| Checkout Repositorio | `actions/checkout@v4` | Se descarga el repositorio en runner. |
| Instalar .NET | `actions/setup-dotnet@v4` | Se instala .NET en el runner. |
| Instalar Node.js | `actions/setup-node@v4` | Se instala Node.js en el runner. | 
| Instalar AWS CDK | `npm install -g aws-cdk` | Se instala aws-cdk con NPM. |
| Publish .NET AoT Minimal API | `docker run --rm -v ...:/src -w /src .../amazonlinux:2023 \bash -c "`<br> `yum install -y dotnet-sdk-8.0 gcc zlib-devel &&`<br> `dotnet publish /p:PublishAot=true -r linux-x64 --self-contained &&`<br> `cd ./publish &&`<br> `zip -r -T ./publish.zip ./*"`| Se publica y comprime el proyecto de la API AoT.<br> Por ser AoT, se publica usando docker con la imagen de Amazon Linux 2023. |
| Publish .NET Lambda | `dotnet publish /p:PublishReadyToRun=true -r linux-x64 --no-self-contained` | Se publica el proyecto de la Lambda Worker |
| Compress Publish Directory .NET Lambda | `zip -r -T ./publish.zip ./*` | Se comprime la publicación de la Lambda Worker |
| Configure AWS Credentials | `aws-actions/configure-aws-credentials` | Se configuran credenciales para despliegue en AWS. |
| CDK Synth | `cdk synth` | Se sintetiza la aplicación CDK. |
| CDK Diff | `cdk --app cdk.out diff` | Se obtienen las diferencias entre nueva versión y versión desplegada. |
| CDK Deploy | `cdk --app cdk.out deploy --require-approval never` | Se despliega la aplicación CDK. |

### Variables y Secretos de Entorno

A continuación se presentan las variables que se deben configurar en el Environment para el correcto despliegue:

| Variable de Entorno | Tipo | Descripción |
|---------------------|------|-------------|
| `VERSION_DOTNET` | Variable | Versión del .NET del CDK. Por ejemplo "8". |
| `VERSION_NODEJS` | Variable | Versión de Node.js. Por ejemplo "20". |
| `ARN_GITHUB_ROLE` | Variable | ARN del Rol en IAM que se usará para el despliegue. |
| `ACCOUNT_AWS` | Variable | ID de la cuenta AWS donde desplegar. |
| `REGION_AWS` | Variable | Región primaria donde desplegar. Por ejemplo "us-west-1". |
| `DIRECTORIO_CDK` | Variable | Directorio donde se encuentra archivo cdk.json. En este caso sería ".". |
| `APP_NAME` | Variable | El nombre de la aplicación a desplegar. Por ejemplo "Hermes" |
| `AOT_MINIMAL_API_DIRECTORY` | Variable | Directorio donde se encuentra el proyecto de la Minimal API AoT. Por ejemplo "./ApiRecepcionSolicitudesEnvio" |
| `AOT_MINIMAL_API_LAMBDA_HANDLER` | Variable | Handler de la Minimal API AoT. Por ejemplo "ApiRecepcionSolicitudesEnvio" |
| `AOT_MINIMAL_API_LAMBDA_MEMORY_SIZE` | Variable | Cantidad de memoria para la Lambda de la Minimal API AoT. Por ejemplo "256". |
| `AOT_MINIMAL_API_LAMBDA_TIMEOUT` | Variable | Tiempo en segundos de timeout para la Lambda de la Minimal API AoT. Por ejemplo "120". |
| `AOT_MINIMAL_API_MAPPING_DOMAIN_NAME` | Variable | El Custom Domain Name de API Gateway que se usará para la Minimal API AoT. |
| `AOT_MINIMAL_API_MAPPING_KEY` | Variable | Mapping a usar en el Custom Domain de API Gateway. Por ejemplo "hermes". |
| `SES_NOMBRE_DE_DEFECTO` | Variable | Nombre por defecto a usar como remitente de los correos. Por ejemplo "Hermes". |
| `SES_CORREO_DE_DEFECTO` | Variable | Correo por defecto a usar como remitente de los correos. Por ejemplo "hermes@ejemplo.cl". |
| `WORKER_DIRECTORY` | Variable | Directorio donde se encuentra el proyecto de la Lambda Worker. Por ejemplo "./LambdaWorkerEnvioCorreos". |
| `WORKER_LAMBDA_HANDLER` | Variable | Handler de la Lambda Worker. Por ejemplo "LambdaWorkerEnvioCorreos::LambdaWorkerEnvioCorreos.Function::FunctionHandler". |
| `WORKER_LAMBDA_MEMORY_SIZE` | Variable | Cantidad de memoria para la Lambda Worker. Por ejemplo "256". |
| `WORKER_LAMBDA_TIMEOUT` | Variable | Tiempo en segundos de timeout para la Lambda Worker. Por ejemplo "120". |
| `NOTIFICATION_EMAILS` | Variable | Emails a los que notificar cuando mensajes lleguen al DLQ (separados por ","). Por ejemplo "correo01@ejemplo.cl,correo02@ejemplo.cl". |