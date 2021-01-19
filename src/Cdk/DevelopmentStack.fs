namespace OpenQuiz.Cdk

open Amazon.CDK
open Amazon.CDK.AWS.DynamoDB
open Amazon.CDK.AWS.S3
open Amazon.CDK.AWS.SSM
open Amazon.CDK.AWS.Cognito

module Helpers =

    let createTable' stack env tableName partitionKeyName partitionKeyType sortKey ttl =
        Table(stack, tableName,
                TableProps(
                    TableName = sprintf "OpenQuiz-%s-%s" env tableName,
                    PartitionKey = Attribute (Name = partitionKeyName, Type = partitionKeyType),
                    SortKey = (match sortKey with Some (name,type') -> Attribute (Name = name, Type = type') | None -> null),
                    BillingMode = BillingMode.PAY_PER_REQUEST,
                    TimeToLiveAttribute = match ttl with Some name -> name | _ -> null
                )) |> ignore

    let createTable stack env tableName partitionKeyName partitionKeyType =
        createTable' stack env tableName partitionKeyName partitionKeyType None None

    let createTableWithTTL stack env tableName partitionKeyName partitionKeyType ttl =
        createTable' stack env tableName partitionKeyName partitionKeyType None (Some ttl)

    let createTableWithSortKey stack env tableName partitionKeyName partitionKeyType sortKeyName sortKeyType =
        createTable' stack env tableName partitionKeyName partitionKeyType (Some (sortKeyName,sortKeyType)) None

    let createParameter stack env name value' =
        let fullName = sprintf "/OpenQuiz/%s/%s" env name
        let props =
            StringParameterProps(
                ParameterName = fullName,
                StringValue = value'
            )

        StringParameter(stack, fullName, props) |> ignore

open Helpers

type DevelopmentStack(scope:Construct, id, props, globalId) as this =
    inherit Stack(scope, id, props)
    let env = "Development"

    // DynamoDB Tables
    do createTable this env "System" "Id" AttributeType.STRING
    do createTable this env "Experts" "Id" AttributeType.STRING
    do createTable this env "Packages" "Id" AttributeType.NUMBER
    do createTable this env "Quizzes" "Id" AttributeType.NUMBER
    do createTableWithSortKey this env "Teams" "QuizId" AttributeType.NUMBER "TeamId" AttributeType.NUMBER
    do createTableWithTTL this env "Tokens" "Token" AttributeType.STRING "TTL"

    // S3 Bucket for all static content
    let bucketProps =
        BucketProps(
            WebsiteIndexDocument = "index.html",
            WebsiteErrorDocument = "error.html",
            PublicReadAccess = true,
            Cors = [|CorsRule(AllowedMethods = [| HttpMethods.GET; HttpMethods.PUT |], AllowedOrigins = [| "*" |], AllowedHeaders  = [| "*" |])|])
    let bucket = Bucket(this, "Bucket", bucketProps)
    do createParameter this env "BucketName" bucket.BucketName
    do createParameter this env "BucketUrl" bucket.BucketWebsiteUrl

    // Cognito UserPool
    let userPool =
        UserPool(this, "UserPool",
            UserPoolProps(
                UserPoolName = sprintf "OpenQuiz-%s" env,
                SignInCaseSensitive = false,
                AutoVerify = AutoVerifiedAttrs(Email = true),
                SignInAliases = SignInAliases(Username = true, Email = true),
                StandardAttributes = StandardAttributes(Email = StandardAttribute(Required = true)),
                PasswordPolicy = PasswordPolicy(MinLength=6., RequireDigits=false, RequireLowercase=false, RequireSymbols=false, RequireUppercase=false),
                SelfSignUpEnabled = true ))

    let userPoolDomain =
        userPool.AddDomain("UserPoolDomain",
            UserPoolDomainOptions(
                CognitoDomain = CognitoDomainOptions(DomainPrefix = "openquiz-" + globalId)))

    let appClientProps =
        UserPoolClientProps(
            UserPool = userPool,
            AuthFlows = AuthFlow(UserPassword = true),
            OAuth = OAuthSettings(
                Flows = OAuthFlows(AuthorizationCodeGrant = true, ImplicitCodeGrant = true),
                Scopes = [|OAuthScope.EMAIL; OAuthScope.OPENID; OAuthScope.PROFILE|],
                CallbackUrls = [|"http://localhost:8080/app/"|]
                ),
            SupportedIdentityProviders = [|UserPoolClientIdentityProvider.COGNITO|],
            UserPoolClientName = sprintf "OpenQuiz-%s-Client" env )

    let appClient = UserPoolClient(this, "OpenQuiz", appClientProps)
    do createParameter this env "AppUrl" "http://localhost:8080/app/"
    do createParameter this env "LoginUrl" <| userPoolDomain.BaseUrl()
    do createParameter this env "UserPoolClientId" appClient.UserPoolClientId
