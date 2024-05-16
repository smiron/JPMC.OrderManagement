# Order Management

- [Solution](#solution)
  - [Architecture](#architecture)
  - [Service](#service)
    - [DynamoDB Single-Table Design](#dynamodb-single-table-design)
    - [ECS](#ecs)
- [Getting started](#getting-started)
  - [Software requirements](#software-requirements)
- [Running the solution in AWS cloud](#running-the-solution-in-aws-cloud)

---

## Solution

### Architecture

![Architecture Diagram](./resources/architecture.drawio.png)

### Service

#### DynamoDB Single-Table Design

The solution is using DynamoDB with the single-table design pattern as the data store.

#### ECS

To reduce latency and cost.

## Getting started

### Software requirements

- Docker
- AWS CLI
- AWS CDK
- DOTNET SDK 8
- Visual Studio 2022
- [NoSQL Workbench for DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/workbench.html)

## Running the solution in AWS cloud

Please follow the below steps to deploy and run the solution in your AWS cloud account:

1. Bootstrap the CDK framework to your AWS account

    ```bash
    cdk bootstrap --termination-protection true
    ```

2. Deploy the CI/CD stack

    ```bash
    cdk deploy JPMC-OrderManagement-CiCdStack
    ```

3. Authenticate to the newly create ECR repository. Make sure to replace the placeholder for `AWS-REGION` and `AWS-ACCOUNT`.

    ```bash
    aws ecr get-login-password --region [AWS-REGION] | docker login --username AWS --password-stdin [AWS-ACCOUNT].dkr.ecr.[AWS-REGION].amazonaws.com
    ```

4. Build the API Docker image

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


TODO:

- Solution diagram
- Calculate availability
- Consider deployment scenarios (how do you update code, ECS, Redis)
- Consider DR strategy
- CI/CD
- Cost estimate
- Deletion protection where possible
- point-in-time recovery for the data store
- Use a Route 53 domain to host the system
- Consider adding AuthN and AuthZ (https://docs.aws.amazon.com/elasticloadbalancing/latest/application/listener-authenticate-users.html)
- document reasons for using an API Gateway with ECS (per user throttling logic, API keys etc) (https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-private-integration.html)
- Document what we've done ro reduce latency (ECS - eliminates lambda cold start + lambda service delay, ALB to load balance requests, single table design)
- Enable autoscaling
- Document usage of Single Table Design to reduce cost and latency
- Consider event sourcing
- Use NLB for even lower latencies
- Item lineage via DynamoDB streams in S3

Done:

- Configure CloudWatch Logs logging via configuration keys