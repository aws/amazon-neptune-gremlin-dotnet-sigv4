## Amazon Neptune Gremlin .NET SigV4

This project provides a custom library that extends the [Apache TinkerPop Gremlin.NET client](https://github.com/apache/tinkerpop/tree/master/gremlin-dotnet) to enable AWS IAM Signature Version 4 signing for establishing authenticated connections to [Amazon Neptune](https://aws.amazon.com/neptune/).

For example usage refer to:[NeptuneGremlinNETSigV4Example.cs](example/NeptuneGremlinNETSigV4Example.cs). This example shows how to leverage this library for establishing an authenticated connection to Neptune.

For general information on how to connect to Amazon Neptune using Gremlin and best practices, refer to the [documentation](https://docs.aws.amazon.com/neptune/latest/userguide/best-practices-gremlin.html).

## Usage

A snippet of the code from [NeptuneGremlinNETSigV4Example.cs](example/NeptuneGremlinNETSigV4Example.cs):

```
var neptune_host = "neptune-endpoint"; // ex: mycluster.cluster.us-east-1.neptune.amazonaws.com
var neptune_port = 8182;

var gremlinServer = new GremlinServer(neptune_host, neptune_port);
var gremlinClient = new GremlinClient(gremlinServer, 
    webSocketConfiguration: new SigV4RequestSigner().signRequest(neptune_host, neptune_port));
var remoteConnection = new DriverRemoteConnection(gremlinClient);
var g = Traversal().WithRemote(remoteConnection);
```

The `GremlinClient` library accepts both a `GremlinServer` object as well as a `webSocketConfiguration` object that contains a custom configuration set for establishing the WebSocket connection to Amazon Neptune.  The [SigV4RequestSigner](src/SigV4RequestSigner.cs) library fetchs IAM credentials using the `FallbackCredentialsFactory` API (which works similarly to the [Java Default Credential Provider Chain](https://docs.aws.amazon.com/sdk-for-java/v1/developer-guide/credentials.html)), performs the proper Signature Version 4 signing of an http request, and creates the proper WebSocket configuration based on this signed http request.  One can then pass this `webSocketConfiguration` to the `GremlinClient` to create the connection to Neptune.

### Using within Amazon EC2

To use this library in an application hosted on EC2, be sure to assign a role to the EC2 instance with the [proper permissions](https://docs.aws.amazon.com/neptune/latest/userguide/iam-auth-policy.html) to access Amazon Neptune.  This library will fetch the IAM role credentials from the [EC2 metadata store](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/instancedata-data-retrieval.html).  If an IAM role is not assigned to the instance, the library will look for the `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `SESSION_TOKEN` environment variables or look for an AWS CLI credentials file at `~/.aws/credentials`.

### Using within AWS Lambda

To use this library in an application hosted in a Lambda function, be sure to assign a role to the EC2 instance with the [proper permissions](https://docs.aws.amazon.com/neptune/latest/userguide/iam-auth-policy.html) to access Amazon Neptune.  Upon invocation, the Lambda function will import the IAM role's credentials into the following environment variables: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, and `AWS_SESSION_TOKEN`.  This library will use those environment variables to import the credentials and perform the request signing.

## Security

See [CONTRIBUTING](CONTRIBUTING.md#security-issue-notifications) for more information.

## License

This project is licensed under the Apache-2.0 License.

