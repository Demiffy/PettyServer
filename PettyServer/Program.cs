using System;
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
            EnsureUsersDirectory();
            StartHttpServer();
            Console.ReadLine();
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

        private static void EnsureUsersDirectory()
        {
            if (!Directory.Exists(usersDirectory))
            {
                Directory.CreateDirectory(usersDirectory);
            }
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
                    if (segments.Length > 1)
                    {
                        switch (segments[1].TrimEnd('/'))
                        {
                            case "register":
                                if (segments.Length >= 4)
                                {
                                    string username = segments[2].TrimEnd('/');
                                    string password = segments[3].TrimEnd('/');
                                    await HandleUserRegistration(username, password, response);
                                }
                                break;
                            case "selectpet":
                                if (segments.Length >= 6)
                                {
                                    string username = segments[2].TrimEnd('/');
                                    string species = segments[3].TrimEnd('/');
                                    string gender = segments[4].TrimEnd('/');
                                    string name = segments[5].TrimEnd('/');
                                    await HandleSelectPet(username, species, gender, name, response);
                                }
                                break;
                            case "login":
                                if (segments.Length >= 4)
                                {
                                    string username = segments[2].TrimEnd('/');
                                    string password = segments[3].TrimEnd('/');
                                    await HandleLogin(username, password, response);
                                }
                                break;
                            default:
                                response.StatusCode = 404;
                                await SendResponse(response, "Not Found");
                                break;
                        }
                    }
                }
                finally
                {
                    response.Close();
                }
            }
        }

        private static async Task HandleLogin(string username, string password, HttpListenerResponse response)
        {
            string userFilePath = Path.Combine(usersDirectory, $"{username}.json");
            if (!File.Exists(userFilePath))
            {
                response.StatusCode = 404; // User not found
                await SendResponse(response, "User not found");
                return;
            }

            var user = JsonSerializer.Deserialize<User>(File.ReadAllText(userFilePath));
            if (user.Password != password)
            {
                response.StatusCode = 401;
                await SendResponse(response, "Unauthorized");
                return;
            }

            if (user.Pet == null)
            {
                response.StatusCode = 404;
                await SendResponse(response, "No pet found");
                return;
            }

            string json = JsonSerializer.Serialize(user.Pet);
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.StatusCode = 200; // OK
            await SendResponse(response, json);
        }

        private static async Task HandleUserRegistration(string username, string password, HttpListenerResponse response)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                response.StatusCode = 400; // Bad Request
                return;
            }

            string userFilePath = Path.Combine(usersDirectory, $"{username}.json");
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
            string userFilePath = Path.Combine(usersDirectory, $"{username}.json");
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

        private static async Task SendResponse(HttpListenerResponse response, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
    }

    public class User
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public Pet Pet { get; set; }
    }

    public class Pet
    {
        public string Species { get; set; }
        public string Gender { get; set; }
        public string Name { get; set; }
    }
}
