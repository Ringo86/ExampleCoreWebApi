# ExampleCoreWebApi
### This is a work in progress!
### This is an example asp.net 6.0 API
## Things not done:
### -jwt refresh token system
### -exception handling
### -code refactoring (loose literals and some code duplication)
### -asymmetric jwt encryption for microservice architechture
### -comprehensive security strategy for area authorizations
### -any non-account related features after logging in
## What it's got:
### -jwt authentication and claims authorization
### -asp.net role-based authorization annotations (read from valid jwt)
### -slow bcrypt password hashing with salt and pepper
### -properly hidden secrets (Azure keyvault loads in the cloud deployment)
### -email verification and password reset
### -code-first migrations with EF Core 7.0 (including many-to-many mapping in fluent)
### CI/CD pipeline automatically builds in Azure (manual release)
