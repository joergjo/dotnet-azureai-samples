using System.Text.Json;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using FunctionsAgent;
using Microsoft.Extensions.Configuration;

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

var submitSupportTicket = UserFunctions.SubmitSupportTicketDefinition;

// Define an agent that can use the custom functions
PersistentAgent agent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: $"support-agent-{Random.Shared.Next(1_000_000)}",
    instructions: """
                  You are a technical support agent.
                  When a user has a technical issue, you get their email address and a description of the issue.
                  Then you use those values to submit a support ticket using the function available to you.
                  If a file is saved, tell the user the file name.
                  """,
    tools: [submitSupportTicket]);

PersistentAgentThread thread = await agentClient.Threads.CreateThreadAsync();

Console.WriteLine("You are chatting with: {0} ({1})", agent.Name, agent.Id);

// Set up automatic function calling
var toolDelegates = new Dictionary<string, Delegate>()
{
    { submitSupportTicket.Name, new Func<string, string, string>(UserFunctions.SubmitSupportTicket) }
};
var callOptions = new AutoFunctionCallOptions(toolDelegates, 5);

// Loop until the user types 'quit'
while (true)
{
    // Get input text
    Console.Write("Enter a prompt (or type 'quit' to exit): ");
    var userPrompt = Console.ReadLine()?.Trim();
    if (userPrompt is null || userPrompt.Equals("quit", StringComparison.CurrentCultureIgnoreCase))
    {
        break;
    }

    if (userPrompt.Length == 0)
    {
        Console.WriteLine("Please enter a prompt.");
        continue;
    }

    // Send a prompt to the agent
    await agentClient.Messages.CreateMessageAsync(
        thread.Id,
        role: MessageRole.User,
        content: userPrompt);

    var runOptions = new CreateRunStreamingOptions
    {
        AutoFunctionCallOptions = callOptions
    };

    var defaultColor = Console.ForegroundColor;
    
    // Stream the agent's response
    await foreach (var streamingUpdate in agentClient.Runs.CreateRunStreamingAsync(thread.Id, agent.Id,
                       options: runOptions))
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
            case StreamingUpdateReason.MessageCompleted:
                var messageStatusUpdate = (MessageStatusUpdate)streamingUpdate;
                var contentItems = messageStatusUpdate.Value.ContentItems;
                foreach (var contentItem in contentItems)
                {
                    if (contentItem is MessageTextContent textContent)
                    {
                        Console.WriteLine(textContent.Text);
                    }
                }
                break;
        }
    }
}

// Clean up
await agentClient.Threads.DeleteThreadAsync(thread.Id);
await agentClient.Administration.DeleteAgentAsync(agent.Id);