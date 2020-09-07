using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;
using WebSocketSharp;

namespace DirectLineConsole
{
    public class Program
    {
        #region Fields

        private static string _directLineSecret = ConfigurationManager.AppSettings["DirectLineSecret"];
        private static string _botId = ConfigurationManager.AppSettings["BotId"];

        // fromUser is the field that identifies which user is sending activities to the Direct Line service.
        // Because this value is created and sent within your Direct Line client, your bot should not
        // trust the value for any security-sensitive operations. Instead, have the user log in and
        // store any sign-in tokens against the Conversation or Private state fields. Those fields
        // are secured by the conversation ID, which is protected with a signature.
        private static string _fromUser = "DirectLine Console App";

        private static string _exitTriggerPhrase = "bye";

        #endregion

        #region Methods

        public static void Main(string[] args)
        {
            Console.WriteLine("DirectLine Console App using WebSockets");
            Console.WriteLine("=======================================\n");

            Console.WriteLine("Please select from the following:");
            Console.WriteLine("[1] Start a new conversation");
            Console.WriteLine("[2] Continue a conversation");

            Console.Write("\nChoice: ");
            var selectedChoice = Console.ReadLine().Trim();

            if (selectedChoice == "1")
            {
                startBotConversationAsync().Wait();
            }
            else if (selectedChoice == "2")
            {
                Console.WriteLine("\nPlease provide the conversation ID and watermark:");
                Console.Write("Conversation ID: ");
                var conversationId = Console.ReadLine().Trim();

                Console.Write("Watermark: ");
                var watermark = Console.ReadLine().Trim();

                continueExistingBotConversationAsync(conversationId, watermark).Wait();
            }

            // comment above code and uncomment below to go directly to a hardcoded conversation and watermark 

            //var conversationId = "2TJQMTx4QD990Mn71v0PIB-l";
            //continueExistingBotConversationAsync(conversationId, "0").Wait();
        }

        private static async Task startBotConversationAsync()
        {
            // Obtain a token using the Direct Line secret
            var tokenResponse = await new DirectLineClient(_directLineSecret).Tokens.GenerateTokenForNewConversationAsync();

            // Use token to create conversation
            var directLineClient = new DirectLineClient(tokenResponse.Token);
            var conversation = await directLineClient.Conversations.StartConversationAsync();

            await startWebSocketConnectionAsync(directLineClient, conversation);
        }

        /// <summary>
        ///     Continues an existing bot conversation.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation that we need to continue.</param>
        private static async Task continueExistingBotConversationAsync(string conversationId, string watermark = null)
        {
            // Initialize a DirectLineClient using the secret.
            var directLineClient = new DirectLineClient(_directLineSecret);

            // Since we are trying to continue a previous conversation, we reconnect to it via conversation ID.
            // If we pass a watermark, it would retrieve all the messages starting from the watermark
            // and send it to us via OnMessage Event.
            var conversation = await directLineClient.Conversations.ReconnectToConversationAsync(conversationId);

            // We create a connection specific for the conversation
            var convDirectLineClient = new DirectLineClient(conversation.Token);

            // We start the web socket connection.
            await startWebSocketConnectionAsync(convDirectLineClient, conversation, watermark);
        }

        private static async Task startWebSocketConnectionAsync(DirectLineClient directLineClient, Conversation conversation, string watermark = null)
        {
            try
            {
                using (var webSocketClient = new WebSocket(conversation.StreamUrl))
                {
                    // You have to specify TLS version to 1.2 or connection will be failed in handshake.
                    webSocketClient.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                    webSocketClient.OnMessage += WebSocketClient_OnMessage;
                    webSocketClient.Connect();

                    var userInput = string.Empty;

                    Console.WriteLine("");
                    Console.WriteLine("- Successfully connected via WebSockets.");
                    Console.WriteLine("- Starting conversation - " + conversation.ConversationId);
                    Console.WriteLine("");

                    // we wait for the sockets to receive and display initial messages.
                    if (watermark != null)
                    {
                        var activityHistory = await directLineClient.Conversations.GetActivitiesAsync(conversation.ConversationId, watermark);
                        displayActivity(activityHistory);
                    }

                    do
                    {
                        Console.Write("You: ");
                        userInput = Console.ReadLine().Trim();

                        if (userInput != _exitTriggerPhrase
                            && userInput.Length > 0)
                        {
                            Activity userMessage = new Activity
                            {
                                From = new ChannelAccount(_fromUser),
                                Text = userInput,
                                Type = ActivityTypes.Message
                            };

                            await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, userMessage);
                        }

                    } 
                    while (userInput != _exitTriggerPhrase);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem with the web socket connection.");
                Console.WriteLine(ex.Message);
            }
        }

        private static void WebSocketClient_OnMessage(object sender, MessageEventArgs e)
        {
            // Occasionally, the Direct Line service sends an empty message as a liveness ping. 
            // Ignore these messages.
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            var activitySet = JsonConvert.DeserializeObject<ActivitySet>(e.Data);

            // filter only activities from the bot
            //var activities = from x in activitySet.Activities
            //                 where x.From.Id == _botId
            //                 select x;

            displayActivity(activitySet);
        }

        private static void displayActivity(ActivitySet activitySet)
        {
            if (activitySet?.Activities == null)
            {
                return;
            }

            foreach (Activity activity in activitySet.Activities)
            {
                // Print out the message
                // format is {activity.Id} {activity.Text} for debugging.
                Console.WriteLine(activity.Id + "\t" + activity.Text);

            }
        }

        #endregion
    }
}
