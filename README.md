# E-Commerce Microservices (.NET 8)

A distributed e-commerce application built with ASP.NET Core, API Gateway, Service Discovery, Centralized Logging, and Database-per-Service architecture[cite: 1].

## System Architecture
- **API Gateway**: Ocelot (:5000)[cite: 1]
- **User Service**: ASP.NET Core Web API + PostgreSQL (:5001)[cite: 1]
- **Product Service**: ASP.NET Core Web API + PostgreSQL (:5002)[cite: 1]
- **Order Service**: ASP.NET Core Web API + PostgreSQL (:5003)[cite: 1]
- **Service Registry**: Consul (:8500)[cite: 1]
- **Centralized Logging**: Seq (:5341)[cite: 1]

## Tech Stack
- .NET 8 SDK[cite: 1]
- Entity Framework Core[cite: 1]
- PostgreSQL[cite: 1]
- Docker & Docker Compose[cite: 1]
