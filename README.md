# Azure AI Foundry samples for .NET 9 and later

This repository contains samples for using Azure AI Foundry with .NET 9 and later. 
The samples have been ported from Microsoft Learn modules that existed only for Python at the time of writing.

## Table of Contents

| .NET Project                          | Module                                                                                                                                                         |
|---------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [DataAgent](./DataAgent/)             | [Develop an AI agent with Azure AI Foundry Agent Service](https://learn.microsoft.com/en-us/training/modules/develop-ai-agent-azure/)                          |
| [FunctionsAgent](./FunctionsAgent/)   | [Integrate custom tools into your agent](https://learn.microsoft.com/en-us/training/modules/build-agent-with-custom-tools/)                                    |
| [ConnectedAgents](./ConnectedAgents/) | [Develop a multi-agent solution with Azure AI Foundry Agent Service](https://learn.microsoft.com/en-us/training/modules/develop-multi-agent-azure-ai-foundry/) |

Follow the links in the table to access the corresponding Microsoft Learn modules for detailed instructions and explanations.

## Configuration

Every sample requires configuration to connect to Azure AI Foundry services.

### Environment Variables
Set or export the following environment variables in your terminal or IDE:
- `AzureAI__ProjectEndpoint`: The endpoint URL for your Azure AI Foundry project
- `AzureAI__ModelDeployment`: The name of the model deployment in your Azure AI Foundry project

### User Secrets
Add the following user secrets to your project:
```bash
dotnet user-secrets set "AzureAI:ProjectEndpoint" "<your_project_endpoint>"
dotnet user-secrets set "AzureAI:ModelDeployment" "<your_model_deployment>"
```

If you define these both as environment variables and user secrets, the environment variables will take precedence.