# Order Management

- [Getting started](#getting-started)
  - [Software requirements](#software-requirements)
  - [Running the solution in AWS](#running-the-solution-in-aws)
- [Solution](#solution)
  - [Design considerations](#design-considerations)
    - [DynamoDB Single-Table Design](#dynamodb-single-table-design)
    - [Amazon ECS on AWS Fargate](#amazon-ecs-on-aws-fargate)
  - [Services](#services)
    - [API Service](#api-service)
    - [Data Loader service](#data-loader-service)
  - [DynamoDB data store](#dynamodb-data-store)

---

![Architecture Diagram](./resources/architecture.drawio.png)

## Getting started

### Software requirements

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [AWS CLI](https://aws.amazon.com/cli/)
- [AWS CDK](https://aws.amazon.com/cdk/)
- [DOTNET SDK 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
- [NoSQL Workbench for DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/workbench.html)

### Running the solution in AWS

Please follow the below steps to deploy and run the solution in your AWS cloud account:

1. Bootstrap the CDK framework to your AWS account.

    ```bash
    cdk bootstrap --termination-protection true
    ```

2. Deploy the CI/CD stack.

    ```bash
    cdk deploy JPMC-OrderManagement-CiCdStack
    ```

3. Authenticate to the newly create ECR repository. Make sure to replace the placeholder for `AWS-REGION` and `AWS-ACCOUNT`.

    ```bash
    aws ecr get-login-password --region [AWS-REGION] | docker login --username AWS --password-stdin [AWS-ACCOUNT].dkr.ecr.[AWS-REGION].amazonaws.com
    ```

4. Build the API Docker image.

    ```bash
    dotnet publish ./src/JPMC.OrderManagement.API/ --os linux --arch x64 /t:PublishContainer
    ```

5. Tag the new image with the ECR repository URL. Make sure to replace the placeholder for `AWS-REGION` and `AWS-ACCOUNT`.

    ```bash
    docker tag jpmc-order-management-api:latest [AWS-ACCOUNT].dkr.ecr.[AWS-REGION].amazonaws.com/jpmc-order-management-api:latest
    ```

6. Push the image to ECR. Make sure to replace the placeholder for `AWS-REGION` and `AWS-ACCOUNT`.

    ```bash
    docker push [AWS-ACCOUNT].dkr.ecr.[AWS-REGION].amazonaws.com/jpmc-order-management-api:latest
    ```

7. Deploy the networking and the compute stacks.

    ```bash
    cdk deploy --all
    ```

## Solution

### Design considerations

#### DynamoDB Single-Table Design

The solution is using DynamoDB with the single-table design pattern as the data store. Using this pattern has several benefits including:

- **Reduced latency**: Latency is significantly reduced when employing the single-table design pattern as it directly leads to a reduction in the number of round-trips to DynamoDB.
- **Reduced operational overhead**: "*Even though DynamoDB is fully-managed and pretty hands-off compared to a relational database, you still need to configure alarms, monitor metrics, etc. If you have one table with all items in it rather than eight separate tables, you reduce the number of alarms and metrics to watch.*"
- **Reduced operational cost**: "*With each table you have, you need to provision read and write capacity units. Often you will do some back-of-the-envelope math on the traffic you expect, bump it up by X%, and convert it to RCUs and WCUs. If you have one or two entity types in your single table that are accessed much more frequently than the others, you can hide some of the extra capacity for less-frequently accessed items in the buffer for the other items.*"

More details regarding the single-table design pattern are available at [The What, Why, and When of Single-Table Design with DynamoDB](https://www.alexdebrie.com/posts/dynamodb-single-table/).

#### Amazon ECS on AWS Fargate

`Amazon ECS on AWS Fargate` has been chosen as the compute platform for the solution as it presents several advantages in the context of the requirements:

- **Reduced API latency**: Containers are always running and prepared to process requests. There is no service related latency or overhead.
- **Reduced operational overhead**: There are no instances to manage. The service automatically provisions the required underlying infrastructure to run the containers.
- **Scalability**: ECS automatically scales up and down the number of containers to satisfy the workload being placed on the service.

### Services

The solution consists of two services:

- **API Service**: This service is hosted using `Amazon ECS on AWS Fargate`. Instances of this service are continuously running and handling REST API requests from users.
- **Data Loader Service**: This service is running on demand using `AWS Batch on AWS Fargate`. The exact process is detailed in the next section.

Having two separate processes for handling API requests and Data Loading requests is important for several reasons:

- **Scalability**: The two types of services can scale independently of each other.
- **User experience**: If the pressure on one service is increasing then the remaining service is unaffected. For example, attempting to batch load a significant number of records should not affect the REST API users.

#### API Service

The API service hosts a REST API for managing orders, calculating trade a price, and for placing trades. It also hosts a [swagger](https://swagger.io/) endpoint for easily accessing and using the API in a local and development environment.

Endpoints:

- `GET /api/orders/{id}`: Retrieve the order with the specified `id`.

    Example return data:

    ```json
    {
        "id": 1,
        "symbol": "JPM",
        "side": 0,
        "amount": 20,
        "price": 20
    }
    ```

- `POST /api/orders/{id}`: Create an order with the specified `id` and payload.

    Example payload that can be used to create an order:

    ```json
    {
        "symbol": "JPM",
        "side": 0,
        "amount": 20,
        "price": 20
    }
    ```

- `PATCH /api/orders/{id}`: Modify an order with the specified `id` and payload.

    Example payload that can be used to amend an order:

    ```json
    {
        "amount": 25,
        "price": 20
    }
    ```

- `DELETE /api/orders/{id}`: Delete the order with the specified `id`.

- `POST /api/trade`: Create a trade with the specified payload.

    Example payload that can be used to create a trade:

    ```json
    {
        "symbol": "JPM",
        "side": 0,
        "amount": 22
    }
    ```

    Example return data:

    ```json
    {
        "timestamp": "2024-05-16T10:45:21.1486242Z",
        "successful": true,
        "reason": null
    }
    ```

    If a trade can't be constructed for any reason then the `successful` field is set to false and the `reason` field will contain details regarding the issue (e.g.: not enough orders to construct a trade).

- `POST /api/price`: Price a trade with the specified payload.

    Example payload that can be used to create a trade:

    ```json
    {
        "symbol": "JPM",
        "side": 0,
        "amount": 22
    }
    ```

    Example return data:

    ```json
    {
        "price": 442,
        "timestamp": "2024-05-16T10:49:31.169359Z",
        "successful": true,
        "reason": null
    }
    ```

    If a trade price can't be constructed for any reason then the `successful` field is set to false and the `reason` field will contain details regarding the issue (e.g.: not enough orders to price a trade).

#### Data Loader service

Batch loading orders process:

- **Data Upload to Amazon S3**: The user obtains a presigned Url pointing to an `Amazon S3` prefix. The user uploads the `orders` in CSV format to the presigned Url.
- **S3 to EventBridge**: Once the upload is complete, `Amazon S3` publishes an event to `Amazon EventBridge`.
- **Queue a data loader job**: `Amazon EventBridge` consumes the message and enqueues an `AWS Batch Job` for processing the CSV file.
- The `Data Loader service` follows a two stage process to load the data into the system:

    1. **Download data**: It first downloads the data locally in ephemeral storage. This is done to reduce access time to the data records and to increase reliability.
    2. **Batch Write to DynamoDB**: A batch write is initiated and the the data records are written to DynamoDB.

### DynamoDB data store

![Table Design](./resources/DynamoDB-Design-Table.png)

![Table Design GSI1](./resources/DynamoDB-Design-Table-Index-GSI1.png)

The DynamoDB table design has been authored using [NoSQL Workbench for DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/workbench.html) and is available to download from [here](./resources/DynamoDB-Design-NoSqlWorkbench.json).
