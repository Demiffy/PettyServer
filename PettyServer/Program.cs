using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PettyServer
{
    internal class Program
    {
        private static HttpListener listener;
        private static string usersDirectory = "Users";

        static void Main(string[] args)
        {
            StartHttpServer();
            Console.WriteLine("Server Running");
            Console.ReadLine();  // Keep the console open until a return is entered.
        }

        private static void StartHttpServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:7000/");
            listener.Start();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Listening for API requests on port 7000");
            Console.ResetColor();

            Task.Run(() => HandleRequests());
        }

        private static async Task HandleRequests()
        {
            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                try
                {
                    string[] segments = request.Url.Segments;
                    if (segments.Length >= 3 && segments[1].TrimEnd('/') == "register")
                    {
                        string username = segments[2].TrimEnd('/');
                        string password = segments.Length > 3 ? segments[3].TrimEnd('/') : "";
                        await HandleUserRegistration(username, password, response);
                    }
                    else if (segments.Length >= 5 && segments[1].TrimEnd('/') == "selectpet")
                    {
                        string username = segments[2].TrimEnd('/');
                        string species = segments[3].TrimEnd('/');
                        string gender = segments[4].TrimEnd('/');
                        string name = segments.Length > 5 ? segments[5].TrimEnd('/') : "";
                        await HandleSelectPet(username, species, gender, name, response);
                    }
                    else
                    {
                        response.StatusCode = 404;
                        byte[] buffer = Encoding.UTF8.GetBytes("Not Found");
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                finally
                {
                    response.Close();
                }
            }
        }

        private static async Task HandleUserRegistration(string username, string password, HttpListenerResponse response)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                response.StatusCode = 400; // Bad Request
                return;
            }

            string userFilePath = $"{username}.json";
            if (File.Exists(userFilePath))
            {
                response.StatusCode = 409; // Conflict
                return;
            }

            var user = new User { Username = username, Password = password };
            string json = JsonSerializer.Serialize(user);
            File.WriteAllText(userFilePath, json);

            response.StatusCode = 200; // OK
            byte[] buffer = Encoding.UTF8.GetBytes("User registered");
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static async Task HandleSelectPet(string username, string species, string gender, string name, HttpListenerResponse response)
        {
            string userFilePath = $"{username}.json";
            if (!File.Exists(userFilePath))
            {
                response.StatusCode = 404; // User not found
                return;
            }

            var user = JsonSerializer.Deserialize<User>(File.ReadAllText(userFilePath));
            user.Pet = new Pet { Species = species, Gender = gender, Name = name };
            string json = JsonSerializer.Serialize(user);
            File.WriteAllText(userFilePath, json);

            response.StatusCode = 200; // OK
            byte[] buffer = Encoding.UTF8.GetBytes("Pet selected");
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
    }

    public class User
    {
        public string Username { get; set; }
        public string Password { get; set; } // Storing passwords as plain text is insecure. Consider hashing.
        public Pet Pet { get; set; }
    }

    public class Pet
    {
        public string Species { get; set; }
        public string Gender { get; set; }
        public string Name { get; set; }
    }
}
