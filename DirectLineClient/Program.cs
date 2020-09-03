using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;
using WebSocketSharp;
using DirectLineClientConnection = Microsoft.Bot.Connector.DirectLine.DirectLineClient;

namespace DirectLineClient
{
    public class Program
    {
        #region Fields

        private static string _directLineSecret = ConfigurationManager.AppSettings["DirectLineSecret"];
        private static string _botId = ConfigurationManager.AppSettings["BotId"];
        private static string _directLineEndpoint = ConfigurationManager.AppSettings["BotDirectLineEndpoint"];

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
            var tokenClient = new DirectLineClientConnection(
                                            new Uri(_directLineEndpoint),
                                            new DirectLineClientCredentials(_directLineSecret));

            var conversation = await tokenClient.Tokens.GenerateTokenForNewConversationAsync();

            // Use token to create a new directline client specific for the conversation
            var directLineClient = new DirectLineClientConnection(
                                            new Uri(_directLineEndpoint),
                                            new DirectLineClientCredentials(conversation.Token));
    
            await startWebSocketConnectionAsync(directLineClient, conversation);
        }

        /// <summary>
        ///     Continues an existing bot conversation.
        /// </summary>
        /// <param name="conversationId">The ID of the conversation that we need to continue.</param>
        private static async Task continueExistingBotConversationAsync(string conversationId, string watermark = null)
        {
            // Initialize a DirectLineClient using the secret.
            var directLineClient = new DirectLineClientConnection(
                                            new Uri(_directLineEndpoint),
                                            new DirectLineClientCredentials(_directLineSecret));

            // Since we are trying to continue a previous conversation, we reconnect to it via conversation ID.
            var conversation = await directLineClient.Conversations.ReconnectToConversationAsync(conversationId);

            // We create a new connection specific for the conversation
            var convDirectLineClient = new DirectLineClientConnection(
                                            new Uri(_directLineEndpoint),
                                            new DirectLineClientCredentials(conversation.Token));

            // We start the web socket connection.
            await startWebSocketConnectionAsync(convDirectLineClient, conversation, watermark);
        }

        private static async Task startWebSocketConnectionAsync(DirectLineClientConnection directLineClient, Conversation conversation, string watermark = null)
        {
            // Establish the web sockets connection
            await directLineClient.StreamingConversations.ConnectAsync(
                conversation.ConversationId,
                ReceiveActivities);

            Console.WriteLine("");
            Console.WriteLine("- Successfully connected via WebSockets");
            Console.WriteLine("- Starting conversation - " + conversation.ConversationId);
            Console.WriteLine("");

            //// we wait for the sockets to receive and display initial messages.
            //if (watermark != null)
            //{
            //    var activityHistory = await directLineClient.Conversations.GetActivitiesAsync(conversation.ConversationId, watermark);
            //    ReceiveActivities(activityHistory);
            //}

            var userInput = string.Empty;

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

                    await directLineClient.StreamingConversations.PostActivityAsync(conversation.ConversationId, userMessage);
                }
            }
            while (userInput != _exitTriggerPhrase);

            //try
            //{
            //    using (var webSocketClient = new WebSocket(conversation.StreamUrl))
            //    {
            //        // You have to specify TLS version to 1.2 or connection will be failed in handshake.
            //        webSocketClient.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            //        webSocketClient.OnMessage += WebSocketClient_OnMessage;
            //        webSocketClient.Connect();

            //        var userInput = string.Empty;

            //        Console.WriteLine("");
            //        Console.WriteLine("- Successfully connected via WebSockets.");
            //        Console.WriteLine("- Starting conversation - " + conversation.ConversationId);
            //        Console.WriteLine("");

            //        // we wait for the sockets to receive and display initial messages.
            //        if (watermark != null)
            //        {
            //            var activityHistory = await directLineClient.Conversations.GetActivitiesAsync(conversation.ConversationId, watermark);
            //            displayActivity(activityHistory.Activities);
            //        }

            //        do
            //        {
            //            Console.Write("You: ");
            //            userInput = Console.ReadLine().Trim();

            //            if (userInput != _exitTriggerPhrase
            //                && userInput.Length > 0)
            //            {
            //                Activity userMessage = new Activity
            //                {
            //                    From = new ChannelAccount(_fromUser),
            //                    Text = userInput,
            //                    Type = ActivityTypes.Message
            //                };

            //                await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, userMessage);
            //            }

            //        } 
            //        while (userInput != _exitTriggerPhrase);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("There was a problem with the web socket connection.");
            //    Console.WriteLine(ex.Message);
            //}
        }

        public static void ReceiveActivities(ActivitySet activitySet)
        {
            if (activitySet != null)
            {
                foreach (var a in activitySet.Activities)
                {
                    if (a.Type == ActivityTypes.Message)
                    {
                        Console.WriteLine($"<Bot>: {a.Text}");
                    }
                }
            }
        }


        //private static void WebSocketClient_OnMessage(object sender, MessageEventArgs e)
        //{
        //    // Occasionally, the Direct Line service sends an empty message as a liveness ping. 
        //    // Ignore these messages.
        //    if (string.IsNullOrWhiteSpace(e.Data))
        //    {
        //        return;
        //    }

        //    var activitySet = JsonConvert.DeserializeObject<ActivitySet>(e.Data);
        //    //var activities = from x in activitySet.Activities
        //    //                 where x.From.Id == _botId
        //    //                 select x;

        //    displayActivity(activitySet.Activities);
        //}

        //private static void displayActivity(IList<Activity> activities)
        //{
        //    foreach (Activity activity in activities)
        //    {
        //        // Print out the message
        //        // format is {activity.Id} {activity.Text} for debugging.
        //        Console.WriteLine(activity.Id + "\t" + activity.Text);

        //    }
        //}

        #endregion
    }
}
