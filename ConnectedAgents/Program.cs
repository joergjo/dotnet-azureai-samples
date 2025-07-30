using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using static ConnectedAgents.Agents;

// Clear the console
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

// Create the priority agent on the Azure AI agent service
PersistentAgent priorityAgent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: PriorityAgentName,
    instructions: PriorityAgentInstructions);

// Create a connected agent tool for the priority agent
var priorityAgentTool = new ConnectedAgentToolDefinition(
    new ConnectedAgentDetails(priorityAgent.Id, PriorityAgentName, "Assess the priority of a ticket"));

// Create the team agent and connected tool
PersistentAgent teamAgent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: TeamAgentName,
    instructions: TeamAgentInstructions);

var teamAgentTool = new ConnectedAgentToolDefinition(
    new ConnectedAgentDetails(teamAgent.Id, TeamAgentName, "Determines which team should take the ticket"));

// Create the effort agent and connected tool
PersistentAgent effortAgent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: EffortAgentName,
    instructions: EffortAgentInstructions);

var effortAgentTool = new ConnectedAgentToolDefinition(
    new ConnectedAgentDetails(effortAgent.Id, EffortAgentName,
        "Determines the effort required to complete the ticket"));

// Create a main agent with the Connected Agent tools
PersistentAgent triageAgent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: $"triage-agent-{Random.Shared.Next(1_000_000)}",
    instructions: TriageAgentInstructions);

// Create thread for the chat session
Console.WriteLine("Creating agent thread");
PersistentAgentThread thread = await agentClient.Threads.CreateThreadAsync();

// Create the ticket prompt
const string prompt = "Users can't reset their password from the mobile app.";
var defaultColor = Console.ForegroundColor;
Console.ForegroundColor = ConsoleColor.DarkBlue;
Console.WriteLine("USER: {0}", prompt);
Console.ForegroundColor = defaultColor;

// Send a prompt to the agent
await agentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, prompt);

// Create and process Agent run in thread with tools
Console.WriteLine("Processing agent thread. Please wait.");
defaultColor = Console.ForegroundColor;
await foreach (var streamingUpdate in agentClient.Runs.CreateRunStreamingAsync(thread.Id, triageAgent.Id))
{
    switch (streamingUpdate.UpdateKind)
    {
        case StreamingUpdateReason.RunCreated:
            Console.ForegroundColor = ConsoleColor.Green;
            break;
        case StreamingUpdateReason.RunCompleted:
            Console.ForegroundColor = defaultColor;
            Console.WriteLine();
            break;
        case StreamingUpdateReason.RunFailed:
            Console.ForegroundColor = defaultColor;
            var runUpdate = (RunUpdate)streamingUpdate;
            Console.WriteLine("Run failure: {0}", runUpdate.Value.LastError);
            break;
        case StreamingUpdateReason.MessageUpdated:
            var contentUpdate = (MessageContentUpdate)streamingUpdate;
            Console.Write(contentUpdate.Text);
            break;
    }
}

// Delete the agent when done
Console.WriteLine("Cleaning up agents:");
await agentClient.Administration.DeleteAgentAsync(triageAgent.Id);
Console.WriteLine("Deleted triage agent: {0}", triageAgent.Id);

// Delete the connected agents when done
await agentClient.Administration.DeleteAgentAsync(priorityAgent.Id);
Console.WriteLine("Deleted priority agent: {0}", priorityAgent.Id);

await agentClient.Administration.DeleteAgentAsync(teamAgent.Id);
Console.WriteLine("Deleted team agent: {0}", teamAgent.Id);

await agentClient.Administration.DeleteAgentAsync(effortAgent.Id);
Console.WriteLine("Deleted effort agent: {0}", effortAgent.Id);
