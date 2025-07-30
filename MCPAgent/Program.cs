// Clear the console

using Azure.AI.Agents.Persistent;
using Azure.Identity;
using MCPAgent;
using Microsoft.Extensions.Configuration;


Console.Clear();

// Load environment variables and user secrets 
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["AzureAI:ProjectEndpoint"];
var deployment = configuration["AzureAI:ModelDeployment"];

if (endpoint is not { Length: > 0 } || deployment is not { Length: > 0 })
{
    Console.WriteLine("Please set the following environment variables or user secrets:");
    Console.WriteLine("AzureAI:ProjectEndpoint");
    Console.WriteLine("AzureAI:ModelDeployment");
    Environment.Exit(1);
}

// Connect the agent client
var agentClient = new PersistentAgentsClient(endpoint,
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = true,
        ExcludeManagedIdentityCredential = true
    }));

// Initialize agent MCP tool
var mcpTool = new MCPToolDefinition(MCP.ServerLabel, MCP.ServerUrl);

// Create agent with MCP tool and process agent run
PersistentAgent agent = agentClient.Administration.CreateAgent(
    deployment,
    "my-mcp-agent",
    instructions: """
                  You have access to an MCP server called `microsoft.docs.mcp` - this tool allows you to 
                  search through Microsoft's latest official documentation. Use the available MCP tools 
                  to answer questions and perform tasks.
                  """,
    tools: [mcpTool]);

// Log info
Console.WriteLine("Created agent with ID {0}", agent.Id);
Console.WriteLine($"MCP server '{mcpTool.ServerLabel}' at '{mcpTool.ServerUrl}");

// Create thread for communication
PersistentAgentThread thread = await agentClient.Threads.CreateThreadAsync();
Console.WriteLine($"Created thread with ID {thread.Id}");

// Create a message on the thread
await agentClient.Messages.CreateMessageAsync(
    thread.Id,
    MessageRole.User,
    "Give me the Azure CLI commands to create an Azure Container App with a managed identity.");

// Update MCP tool headers
var mcpToolResource = new MCPToolResource(MCP.ServerLabel);
mcpToolResource.UpdateHeader(MCP.SecretHeaderName, MCP.SecretHeaderValue);

// Set approval mode
mcpToolResource.RequireApproval = MCPToolResourceRequireApproval.Never;

// Create and process agent run in thread with MCP tools
ThreadRun run = await agentClient.Runs.CreateRunAsync(thread, agent, mcpToolResource.ToToolResources());

while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
{
    await Task.Delay(TimeSpan.FromMilliseconds(1000));
    run = await agentClient.Runs.GetRunAsync(thread.Id, run.Id);

    // Since we set the approval mode to Never, the following code will not be executed.
    // However, if is required to handle tool approvals, so we leave it here.
    if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
    {
        List<ToolApproval> toolApprovals = [];
        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
        {
            if (toolCall is RequiredMcpToolCall mcpToolCall)
            {
                Console.WriteLine("Approving MCP tool call: {0}", mcpToolCall.Name);
                toolApprovals.Add(
                    new ToolApproval(mcpToolCall.Name, true)
                    {
                        Headers =
                        {
                            { MCP.SecretHeaderName, MCP.SecretHeaderValue }
                        }
                    });
            }
        }

        if (toolApprovals.Count > 0)
        {
            // Approve the MCP tool calls
            run = await agentClient.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolApprovals: toolApprovals);
        }
    }
}

if (run.Status == RunStatus.Failed)
{
    Console.WriteLine("Run failed: {0}", run.LastError);
    Environment.Exit(1);
}

var runSteps = agentClient.Runs.GetRunStepsAsync(run);

await foreach (var runStep in runSteps)
{
    Console.WriteLine("Step {0} status: {1}", runStep.Id, runStep.Status);

    if (runStep.StepDetails is RunStepToolCallDetails toolCalls)
    {
        Console.WriteLine("MCP tool calls:");
        foreach (var toolCall in toolCalls.ToolCalls)
        {
            if (toolCall is RunStepMcpToolCall mcpToolCall)
            {
                Console.WriteLine("    Tool Call ID: {0}", toolCall.Id);
                Console.WriteLine("    Type: MCP");
                Console.WriteLine("    Name: {0}", mcpToolCall.Name);
            }
        }

        Console.WriteLine();
    }
}

// Fetch and log all messages
var separator = new string('-', 50);

Console.WriteLine("Conversation:");
Console.WriteLine(separator);

var threadMessages = agentClient.Messages.GetMessagesAsync(thread.Id);
await foreach (var message in threadMessages)
{
    foreach (var content in message.ContentItems)
    {
        if (content is MessageTextContent textContent)
        {
            Console.WriteLine("{0}: {1}", message.Role, textContent.Text);
            Console.WriteLine(separator);
        }
    }
} 

await agentClient.Threads.DeleteThreadAsync(thread.Id);
await agentClient.Administration.DeleteAgentAsync(agent.Id);