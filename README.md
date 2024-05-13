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