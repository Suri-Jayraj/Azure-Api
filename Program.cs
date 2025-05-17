using Azure;
using Azure.AI.Inference;
using System;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Register ChatCompletionsClient as singleton
builder.Services.AddSingleton<ChatCompletionsClient>(sp =>
{
    var endpoint = new Uri("https://surib-mahswkk6-eastus2.openai.azure.com/openai/deployments/gpt-4.1 ");
    var credential = new AzureKeyCredential("DOdq0mcnp8nAP8gRSAT50XRwfgSo9FnXgygrok0ZdH3pmzKVEAp7JQQJ99BEACHYHv6XJ3w3AAAAACOGP24m");
    return new ChatCompletionsClient(endpoint, credential);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policyBuilder =>
    {
        policyBuilder.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll"); // <-- Add this line

app.MapPost("/chat", async (HttpContext context, ChatRequest request, ChatCompletionsClient client) =>
{
    // System prompt for Grok 3
    var systemPrompt = @"You'd like to set up your Assistant with the same guidelines as Claude, as described in the image. Here's a tailored version of those guidelines for me, Grok 3, updated for the current date of May 12, 2025: --- The assistant is Grok 3, created by xAI. The current date is May 12, 2025. Grok 3's knowledge base is continuously updated with no strict cutoff. It answers questions about events prior to and after May 2025 the way a highly informed individual in May 2025 would if they were talking to someone from the above date, and can let the human know when relevant. It should give concise responses to very simple questions, but provide thorough responses to more complex and open-ended questions. If it is asked to assist with tasks involving the expression of views held by a significant number of people, Grok 3 provides assistance with the task even if it personally disagrees with the views being expressed, but follows this with a discussion of broader perspectives. Grok 3 doesn't engage in stereotyping, including the negative stereotyping of majority groups. If asked about controversial topics, Grok 3 tries to provide careful thoughts and objective information without downplaying its harmful content or implying that there are reasonable perspectives on both sides. It is happy to help with writing, analysis, question answering, math, coding, and all sorts of other tasks. It uses markdown for coding. It does not mention this information about itself unless the information is directly pertinent to the human's query.";

    // Initialize messages list with system prompt and user input
    var messages = new List<ChatRequestMessage>
    {
        new ChatRequestSystemMessage(systemPrompt),
        new ChatRequestUserMessage(request.UserInput)
    };

    // Add history messages if provided
    if (request.History != null)
    {
        foreach (var msg in request.History)
        {
            messages.Add(msg.Type.ToLower() switch
            {
                "user" => new ChatRequestUserMessage(msg.Content),
                "assistant" => new ChatRequestAssistantMessage(msg.Content), // Ensure Content is passed
                _ => throw new ArgumentException($"Unknown message type: {msg.Type}")
            });
        }
    }

    // Configure chat completion options
    var requestOptions = new ChatCompletionsOptions(messages)
    {
        MaxTokens = 800,
        Temperature = 1.0f,
        NucleusSamplingFactor = 1.0f,
        FrequencyPenalty = 0.0f,
        PresencePenalty = 0.0f,
    };

    // Set response content type for streaming
    context.Response.ContentType = "text/event-stream";

    try
    {
        using var response = await client.CompleteStreamingAsync(requestOptions);
        await foreach (var chatUpdate in response)
        {
            if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
            {
                await context.Response.WriteAsync($"data: {chatUpdate.ContentUpdate}\n\n");
                await context.Response.Body.FlushAsync();

            }
        }
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync($"data: [ERROR] {ex.Message}\n\n");
    }
})
.WithName("Chat")
.Accepts<ChatRequest>("application/json");

app.Run();
public record ChatRequest(string UserInput, List<HistoryMessage>? History);

public record HistoryMessage(string Type, string Content);