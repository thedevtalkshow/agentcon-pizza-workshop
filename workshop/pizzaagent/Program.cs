using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

//var projectEndpoint = Environment.GetEnvironmentVariable("PizzaBot_ProjectEndpoint");
//var vectorStoreId = Environment.GetEnvironmentVariable("PizzaBot_VectorStoreId");

IConfiguration configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

var projectEndpoint = configuration["PizzaBot_ProjectEndpoint"];
var vectorStoreId = configuration["PizzaBot_VectorStoreId"];

// Create the Foundry Project Client
AIProjectClient projectClient = new AIProjectClient(
    new Uri(projectEndpoint),
    new DefaultAzureCredential()
);

// Get the Persistent Agents Client
PersistentAgentsClient agentsClient = projectClient.GetPersistentAgentsClient();

// read instructions from instructions.txt file
string instructions = File.ReadAllText("instructions.txt");

// Get the Vector Store
PersistentAgentsVectorStore vectorStore = agentsClient.VectorStores.GetVectorStore(vectorStoreId);

// Create a File Search Tool Resource
FileSearchToolResource fileSearchToolResource = new FileSearchToolResource();
fileSearchToolResource.VectorStoreIds.Add(vectorStore.Id);

//string get_pizza_quantity(int people) => $"For {people} you need to order {people / 2 + people % 2} pizzas";
string get_pizza_quantity(int people) => $"For {people} you need to order 42 pizzas";

// Create a Function Tool to estimate amount of pizza to order
FunctionToolDefinition pizzaEstimatorTool = new(
    name: "get_pizza_quantity",
    description: "Get the quantity of pizza to order based on the number of people.",
    parameters: BinaryData.FromObjectAsJson(
        new
        {
            Type = "object",
            Properties = new
            {
                People = new
                {
                    Type = "integer",
                    Description = "The number of people to order pizza for",
                },
            },
            Required = new[] { "people" },
            AdditionalProperties = false
        },
        new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        )
    );


// Create an Agent
PersistentAgent agent = agentsClient.Administration.CreateAgent(
    model: "gpt-4o",
    name: "pizza-agent",
    instructions: instructions,
    topP: 0.7f,
    temperature: 0.7f,
    tools: new List<ToolDefinition> { new FileSearchToolDefinition(), pizzaEstimatorTool },
    toolResources: new ToolResources() { FileSearch = fileSearchToolResource }
);

Console.WriteLine($"Created agent with ID: {agent.Id}");

// Create a Thread for the Agent
PersistentAgentThread thread = agentsClient.Threads.CreateThread();
Console.WriteLine($"Created thread with ID: {thread.Id}");

// Manage the conversation with the Agent
try
{
    string[] exitCommands = ["exit", "quit"];

    while (true)
    {
        Console.Write("You: ");
        string? userInput = Console.ReadLine();

        // loop if no input
        if (string.IsNullOrWhiteSpace(userInput))
        {
            continue;
        }

        // break out if exit commands are given
        if (exitCommands.Contains(userInput.ToLower()))
        {
            break;
        }

        // send user input to agent thread
        PersistentThreadMessage message = agentsClient.Messages.CreateMessage(
            threadId: thread.Id,
            role: MessageRole.User,
            content: userInput
            );

        // run agent
        ThreadRun run = agentsClient.Runs.CreateRun(
            agent: agent,
            thread: thread
            );

        // wait for run to complete
        do
        {
            Thread.Sleep(500);
            run = agentsClient.Runs.GetRun(thread.Id, run.Id);

            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitToolOutputsAction)
            {
                foreach (RequiredToolCall toolcall in submitToolOutputsAction.ToolCalls)
                {
                    if (toolcall is RequiredFunctionToolCall functionToolCall)
                    {
                        using JsonDocument argumentsJson = JsonDocument.Parse(functionToolCall.Arguments);
                        if (functionToolCall.Name == pizzaEstimatorTool.Name)
                        {
                            int peopleArgument = argumentsJson.RootElement.GetProperty("people").GetInt32();
                            var output = new ToolOutput(toolcall, get_pizza_quantity(peopleArgument));
                            run = await agentsClient.Runs.SubmitToolOutputsToRunAsync(run, [output]);
                        }
                    }
                }
            }
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress);

        // the agent had run, get the last message from the thread
        var messages = agentsClient.Messages.GetMessages(
            threadId: thread.Id,
            order: ListSortOrder.Descending
        );

        MessageTextContent? first_message = messages.FirstOrDefault()?.ContentItems
                                            .OfType<MessageTextContent>()
                                            .FirstOrDefault();

        if (first_message != null)
        {
            Console.WriteLine($"Agent: {first_message.Text}");
        }
    }
}
finally
{
    string agentToBeDeletedId = agent.Id;
    bool deleted = agentsClient.Administration.DeleteAgent(agent.Id);
    Console.WriteLine($"Attempting to delete agent with ID: {agentToBeDeletedId}, success: {deleted}");
}