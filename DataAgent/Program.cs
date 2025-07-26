// Create a configuration stack

using Azure.AI.Agents.Persistent;
using Azure.Identity;
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

// Display the data to be analyzed
var cwd = Directory.GetCurrentDirectory();
var path = Path.Combine(cwd, "data.txt");
var data = await File.ReadAllTextAsync(path);
Console.WriteLine(data);

// Connect the agent client
var agentClient = new PersistentAgentsClient(endpoint,
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeEnvironmentCredential = true,
        ExcludeManagedIdentityCredential = true
    }));

PersistentAgentFileInfo file = await agentClient.Files.UploadFileAsync(path, PersistentAgentFilePurpose.Agents);
Console.WriteLine("Uploaded {0}", file.Filename);

var resources = new ToolResources
{
    CodeInterpreter = new CodeInterpreterToolResource
    {
        FileIds = { file.Id }
    }
};

PersistentAgent agent = await agentClient.Administration.CreateAgentAsync(
    deployment,
    name: $"data-agent-{Random.Shared.Next(1_000_000)}",
    instructions:
    """
    "You are an AI agent that analyzes the data in the file that has been uploaded. 
    If the user requests a chart, create it and save it as a .png file.
    """,
    tools: [new CodeInterpreterToolDefinition()],
    toolResources: resources);

Console.WriteLine("Using agent: {0}", agent.Name);

// Create a thread for the conversation
PersistentAgentThread thread = await agentClient.Threads.CreateThreadAsync();

// Loop until the user types 'quit'
var runId = string.Empty;
while (true)
{
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

    ThreadRun run = await agentClient.Runs.CreateRunAsync(
        thread.Id,
        agent.Id);
    do
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        run = agentClient.Runs.GetRun(thread.Id, run.Id);
    } while (run.Status == RunStatus.Queued ||
             run.Status == RunStatus.InProgress);

    if (run.Status == RunStatus.Failed)
    {
        Console.WriteLine("Run failed: {0}", run.LastError);
        continue;
    }

    // Show the latest response from the agent
    runId = run.Id;
    var threadMessages = agentClient.Messages.GetMessagesAsync(thread.Id, order: ListSortOrder.Descending);
    var shouldBreak = false;
    await foreach (var message in threadMessages)
    {
        if (message.Role == MessageRole.Agent)
        {
            foreach (var content in message.ContentItems)
            {
                switch (content)
                {
                    case MessageTextContent textContent:
                        shouldBreak = true;
                        Console.WriteLine("Last message: {0}", textContent.Text);
                        break;
                    case MessageImageFileContent imageFileContent:
                    {
                        shouldBreak = true;
                        // Get any generated files
                        var fileName = $"{imageFileContent.FileId}_image_file.png";
                        BinaryData fileContent = await agentClient.Files.GetFileContentAsync(imageFileContent.FileId);
                        await using var stream = File.Open(fileName, FileMode.Create);
                        await stream.WriteAsync(fileContent);
                        Console.WriteLine("Saved image file to {0}/{1}", cwd, fileName);
                        break;
                    }
                }
            }
        }

        if (shouldBreak)
        {
            break;
        }
    }
}

var messages = agentClient.Messages.GetMessagesAsync(
    threadId: thread.Id,
    runId: runId,
    order: ListSortOrder.Ascending);
await foreach (var message in messages)
{
    foreach (var content in message.ContentItems)
    {
        switch (content)
        {
            case MessageTextContent textContent:
                // Get the conversation history
                Console.WriteLine("{0}: {1}", message.Role, textContent.Text);
                break;
        }
    }
}

await agentClient.Threads.DeleteThreadAsync(thread.Id);
await agentClient.Administration.DeleteAgentAsync(agent.Id);