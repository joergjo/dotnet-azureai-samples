using System.Text.Json;
using Azure.AI.Agents.Persistent;

namespace FunctionsAgent;

public static class UserFunctions
{
    // Create a function to submit a support ticket
    public static string SubmitSupportTicket(string emailAddress, string description)
    {
        var ticketNumber = Guid.NewGuid().ToString("N")[..6];
        var fileName = $"ticket-{ticketNumber}.txt";
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        var text = $"Support ticket: {ticketNumber}\nSubmitted by: {emailAddress}\nDescription:\n{description}";
        File.WriteAllText(filePath, text);
        var messageJson = JsonSerializer.Serialize(
            new
            {
                message = $"Support ticket {ticketNumber} submitted. The ticket file is saved as {fileName}"
            });
        return messageJson;
    }

    // Define a set of callable functions
    public static FunctionToolDefinition SubmitSupportTicketDefinition => new(
        name: "submit_support_ticket",
        description: "Submit a support ticket with an email address and description.",
        parameters: BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    EmailAddress = new
                    {
                        Type = "string",
                        Description = "The email address of the person submitting the ticket.",
                    },
                    Description = new
                    {
                        Type = "string",
                        Description = "A description of the issue or request.",
                    },
                },
                Required = new[]
                {
                    "emailAddress", "description"
                }
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
}