using Microsoft.AspNetCore.Mvc;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using WhatsappAI.Config;
using WhatsappAI.Models;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var twilioConfig = builder.Configuration
                          .GetSection("Twilio")
                          .Get<TwilioConfiguration>();
builder.Services.AddSingleton(twilioConfig);
var azureOpenAiConfig = builder.Configuration
                          .GetSection("AzureOpenAi")
                          .Get<AzureOpenAiConfiguration>();
builder.Services.AddSingleton(azureOpenAiConfig);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () =>
{
    return "Up and running";
})
.WithName("Home")
.WithOpenApi();

app.MapPost("/api/message", async (HttpContext context, 
    TwilioConfiguration twilioConfig, 
    AzureOpenAiConfiguration openAiConfig,
    [FromForm] Message message) =>
{
   //ToDo: validate signature header from twilio to authorize request.
    //var headers = context.Request.Headers;

    //if (context.Request.HasFormContentType)
    //{
    //    // Read the form data
    //    var formData = await context.Request.ReadFormAsync();
    //}

    TwilioClient.Init(twilioConfig.AccountSid, twilioConfig.Token);

    string replyMessage = await GetOpenAIResponse(message.Body, message.From, openAiConfig);
    var reply = await MessageResource.CreateAsync(
        //body: $"Echo: {message.Body}",
        body: replyMessage,
        from: new Twilio.Types.PhoneNumber(twilioConfig.WhatsappNumber),
        to: new Twilio.Types.PhoneNumber(message.From));
    return Results.Ok();
})
.DisableAntiforgery()
.WithName("Message")
.WithOpenApi();

app.Run();


async Task<string> GetOpenAIResponse(string userMessage, string userId, AzureOpenAiConfiguration config)
{
    ApiKeyCredential credential = new ApiKeyCredential(config.Key);
    
    AzureOpenAIClient azureClient = new(new Uri(config.Endpoint), credential);
    ChatClient chatClient = azureClient.GetChatClient(config.Model);

    ChatCompletion completion = await chatClient.CompleteChatAsync(
      [
        new SystemChatMessage("You are an AI assistant that will run in whatsapp and will helps people find information about a specific catalog of technology products. The available products are the following:\nKeyboard which costs 150 usd.\nLaptop which costs 2700 usd.\nMouse which costs 70 usd.\n\nFor additional products they can be redirected to contact mejialuis28@gmail.com \nIf a user decides to order products you can reply that their order has been stored.\n\nTry to limit your answers to selling the products above. Tech Sales is the name of the store. \n"),
        new UserChatMessage(userMessage)
      ],
      new ChatCompletionOptions()
      {
          EndUserId = userId,
          Temperature = (float)0.7,
          MaxOutputTokenCount = 4000,
          FrequencyPenalty = (float)0,
          PresencePenalty = (float)0,
      }
    );
    return completion.Content.First().Text;
}



