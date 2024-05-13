# Order Management

## Bootstrap

```bash
cdk bootstrap --termination-protection true
```

```ps
dotnet run --launch-profile https --configuration Release --project .\src\JPMC.OrderManagement.API\
```

Consider the following options:

- Aurora serverless
- Redis
- RDS with ElastiCache

TODO:

- Solution diagram
- Calculate availability
- Consider deployment scenarios (how do you update code, ECS, Redis)
- Consider DR strategy
- CI/CD
- Cost estimate
- Deletion protection where possible
- point-in-time recovery for the data store
- Configure CloudWatch Logs logging via configuration keys
- Use a Route 53 domain to host the system
- Consider adding AuthN and AuthZ (https://docs.aws.amazon.com/elasticloadbalancing/latest/application/listener-authenticate-users.html)
- document reasons for using an API Gateway with ECS (per user throttling logic, API keys etc) (https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-private-integration.html)
- Document what we've done ro reduce latency (ECS - eliminates lambda cold start + lambda service delay, ALB to load balance requests, single table design)
- Enable autoscaling
- Single Table Design to reduce cost and latency
- Consider event sourcing
- Use NLB for even lower latencies